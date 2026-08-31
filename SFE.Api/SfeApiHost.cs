using Makaretu.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Licensing.Local;
using SFE.Domain.Enums;

namespace SFE.Api;

public sealed class SfeApiHost(IServiceProvider appServices, int port = 5005)
{
    private WebApplication? _app;
    private ServiceDiscovery? _sd;
    private MulticastService? _mdns;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

        _app = builder.Build();

        // ══ Global error → JSON ══
        _app.Use(async (ctx, next) =>
        {
            try { await next(); }
            catch (Exception ex)
            {
                var log = ctx.RequestServices.GetRequiredService<ILogger<SfeApiHost>>();
                log.LogError(ex, "Unhandled exception in {Method} {Path}",
                    ctx.Request.Method, ctx.Request.Path);

                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.Clear();
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        error = ex.GetType().Name,
                        message = ex.Message,
                        inner = ex.InnerException?.Message,
                        path = ctx.Request.Path.Value
                    });
                }
            }
        });

        // ══ Pairing gate ══
        _app.Use(async (ctx, next) =>
        {
            // These endpoints are available before terminal pairing.
            if (ctx.Request.Path.StartsWithSegments("/health") ||
                ctx.Request.Path.StartsWithSegments("/license/status"))
            {
                await next();
                return;
            }

            using var scope = appServices.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var s = await settings.LoadSettingsAsync();

            if (!s.SunmiEnabled || string.IsNullOrWhiteSpace(s.SunmiTerminalId))
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsync("no_terminal_paired");
                return;
            }

            var provided = ctx.Request.Headers["X-Terminal-Id"].ToString();
            if (!string.Equals(provided, s.SunmiTerminalId, StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("unpaired_terminal");
                return;
            }

            await next();
        });

        _app.MapGet("/health", () => Results.Ok(new { ok = true }));

        _app.MapGet("/license/status", () =>
        {
            var guard = appServices.GetRequiredService<ILicenseGuard>();
            var s = guard.Current;
            return Results.Ok(new
            {
                status = s.Status.ToString(),
                allowsInvoicing = s.AllowsInvoicing,
                reason = s.Reason,
                expiresAt = s.Claims?.ExpiresAt,
                daysUntilExpiry = s.DaysUntilExpiry,
                edition = s.Claims?.Edition,
                features = s.Claims?.Features
            });
        });

        _app.MapGet("/orders", async (CancellationToken ct) =>
        {
            using var scope = appServices.CreateScope();
            var orders = await scope.ServiceProvider
                .GetRequiredService<IPendingOrderProvider>().GetPendingAsync(ct);
            return Results.Ok(orders);
        });

        _app.MapDelete("/orders/{orderId}", async (string orderId, CancellationToken ct) =>
        {
            using var scope = appServices.CreateScope();
            var pending = scope.ServiceProvider.GetRequiredService<IPendingOrderProvider>();
            var removed = await pending.RemoveAsync(orderId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        // ══════════════════════════════════════════════════════
        //  RECEIPT ENDPOINTS  (Sunmi optimised)
        // ══════════════════════════════════════════════════════
        const int SUNMI_PAPER_WIDTH_MM = 58; // <-- Set to 80 if your Sunmi uses 80 mm rolls

        // {**id} catch-all MUST be the last segment, so type comes first.
        _app.MapGet("/receipts/proforma/{**id}", async (
            string id,
            CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);

            using var scope = appServices.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<InMemoryPendingOrderStore>();
            var draft = store.GetDraftFor(id);
            if (draft is null)
                return Results.NotFound(new { error = "no_draft", orderId = id });

            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var time = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

            var company = await uow.Companies.GetCurrentCompanyAsync();
            if (company is null)
                return Results.Problem("Company not configured.");

            var pos = draft.PointOfSaleId > 0
                ? await uow.PointsOfSale.GetByIdAsync(draft.PointOfSaleId)
                : null;

            var bytes = EscPosReceiptBuilder.Build(
                invoice: draft,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: draft.CurrencyRate,
                isDuplicate: false,
                asProforma: true,
                overridePaperWidthMm: SUNMI_PAPER_WIDTH_MM);

            return Results.File(bytes, "application/vnd.escpos");
        });

        _app.MapGet("/receipts/fiscal/{**id}", async (
            string id,
            CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);

            using var scope = appServices.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var time = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

            var inv = await uow.Invoices.GetByInvoiceNumberAsync(id);
            if (inv is null || string.IsNullOrEmpty(inv.CodeDEFDGI))
                return Results.NotFound(new { error = "not_fiscalized_yet", orderId = id });

            var company = await uow.Companies.GetCurrentCompanyAsync();
            if (company is null)
                return Results.Problem("Company not configured.");

            var pos = inv.PointOfSaleId > 0
                ? await uow.PointsOfSale.GetByIdAsync(inv.PointOfSaleId)
                : null;

            var bytes = EscPosReceiptBuilder.Build(
                invoice: inv,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: inv.CurrencyRate,
                isDuplicate: false,
                asProforma: false,
                overridePaperWidthMm: SUNMI_PAPER_WIDTH_MM);

            return Results.File(bytes, "application/vnd.escpos");
        });

        // ══════════════════════════════════════════════════════
        //  RECEIPT JSON (Sunmi terminal rendering)
        // ══════════════════════════════════════════════════════

        _app.MapGet("/receipts/json/proforma/{**id}", async (
            string id,
            int? copies,
            CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);

            using var scope = appServices.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<InMemoryPendingOrderStore>();
            var draft = store.GetDraftFor(id);
            if (draft is null)
                return Results.NotFound(new { error = "no_draft", orderId = id });

            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var time = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

            var company = await uow.Companies.GetCurrentCompanyAsync();
            if (company is null)
                return Results.Problem("Company not configured.");

            var pos = draft.PointOfSaleId > 0
                ? await uow.PointsOfSale.GetByIdAsync(draft.PointOfSaleId)
                : null;

            var printCopies = copies ?? 1;
            var docs = new List<object>();

            // ORIGINAL
            var docOriginal = ReceiptJsonBuilder.Build(
                invoice: draft,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: draft.CurrencyRate,
                isDuplicate: false,
                asProforma: true,
                paperWidthMm: SUNMI_PAPER_WIDTH_MM);
            docs.Add(docOriginal);

            // DUPLICATA (if copies >= 2)
            if (printCopies >= 2)
            {
                var docDuplicate = ReceiptJsonBuilder.Build(
                    invoice: draft,
                    company: company,
                    pos: pos,
                    time: time,
                    exchangeRate: draft.CurrencyRate,
                    isDuplicate: true,
                    asProforma: true,
                    paperWidthMm: SUNMI_PAPER_WIDTH_MM);
                docs.Add(docDuplicate);
            }

            return Results.Ok(new { documents = docs });
        });

        _app.MapGet("/receipts/json/fiscal/{**id}", async (
            string id,
            int? copies,
            CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);

            using var scope = appServices.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var time = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

            var inv = await uow.Invoices.GetByInvoiceNumberAsync(id);
            if (inv is null || string.IsNullOrEmpty(inv.CodeDEFDGI))
                return Results.NotFound(new { error = "not_fiscalized_yet", orderId = id });

            var company = await uow.Companies.GetCurrentCompanyAsync();
            if (company is null)
                return Results.Problem("Company not configured.");

            var pos = inv.PointOfSaleId > 0
                ? await uow.PointsOfSale.GetByIdAsync(inv.PointOfSaleId)
                : null;

            var printCopies = copies ?? 1;
            var docs = new List<object>();

            // ORIGINAL
            var docOriginal = ReceiptJsonBuilder.Build(
                invoice: inv,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: inv.CurrencyRate,
                isDuplicate: false,
                asProforma: false,
                paperWidthMm: SUNMI_PAPER_WIDTH_MM);
            docs.Add(docOriginal);

            // DUPLICATA (if copies >= 2)
            if (printCopies >= 2)
            {
                var docDuplicate = ReceiptJsonBuilder.Build(
                    invoice: inv,
                    company: company,
                    pos: pos,
                    time: time,
                    exchangeRate: inv.CurrencyRate,
                    isDuplicate: true,
                    asProforma: false,
                    paperWidthMm: SUNMI_PAPER_WIDTH_MM);
                docs.Add(docDuplicate);
            }

            return Results.Ok(new { documents = docs });
        });

        // ══════════════════════════════════════════════════════
        //  PAYMENTS (unchanged)
        // ══════════════════════════════════════════════════════

        _app.MapPost("/payments", async (InitiatePaymentRequest req, CancellationToken ct) =>
        {
            using var scope = appServices.CreateScope();
            var log = scope.ServiceProvider.GetRequiredService<ILogger<SfeApiHost>>();
            log.LogInformation(
                "POST /payments key={Key} order={Order} amount={Amount} method={Method}",
                req.IdempotencyKey, req.OrderId, req.Amount, req.Method);

            var svc = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var tx = await svc.InitiateAsync(req, ct);
            return Results.Ok(PaymentService.ToDto(tx));
        });

        _app.MapGet("/payments/{id}", async (string id, CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);
            using var scope = appServices.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var tx = await svc.GetAsync(id, ct);
            return tx is null ? Results.NotFound() : Results.Ok(PaymentService.ToDto(tx));
        });

        _app.MapPost("/payments/{id}/result", async (
            string id,
            PaymentResultReport report,
            CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);
            using var scope = appServices.CreateScope();
            var log = scope.ServiceProvider.GetRequiredService<ILogger<SfeApiHost>>();
            log.LogInformation("Result received for {Id}: {Status}", id, report.Status);

            var svc = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var tx = await svc.ReportResultAsync(id, report, ct);

            var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
            if (provider is MockPaymentProvider mock)
                mock.ReportResult(id, report.Status, report.ProviderRef, report.Reason);

            return Results.Ok(new
            {
                accepted = true,
                orderId = id,
                status = report.Status.ToString(),
                echoed = tx is null ? null : PaymentService.ToDto(tx)
            });
        });

        _app.MapPost("/payments/{id}/reconcile", async (string id, CancellationToken ct) =>
        {
            id = Uri.UnescapeDataString(id);
            using var scope = appServices.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var tx = await svc.ReconcileAsync(id, ct);
            return tx is null ? Results.NotFound() : Results.Ok(PaymentService.ToDto(tx));
        });

        _app.MapGet("/orders/{orderId}/qr", async (string orderId, CancellationToken ct) =>
        {
            orderId = Uri.UnescapeDataString(orderId);

            using var scope = appServices.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<OfflineQrResolver>();

            var result = await resolver.ResolveAsync(orderId, ct);

            return result.Outcome switch
            {
                OfflineQrOutcome.Ok => Results.Ok(new
                {
                    token = result.Token,
                    kind = result.Kind.ToString(),
                    order = result.Order
                }),

                OfflineQrOutcome.NotFound => Results.NotFound(new
                {
                    error = "order_not_found",
                    orderId
                }),

                OfflineQrOutcome.NothingDue => Results.Conflict(new
                {
                    error = "nothing_due",
                    orderId
                }),

                _ => Results.Problem("Unable to generate QR.")
            };
        });

        await _app.StartAsync();
        AdvertiseMdns();
    }

    private void AdvertiseMdns()
    {
        _mdns = new MulticastService();
        _sd = new ServiceDiscovery(_mdns);
        var profile = new ServiceProfile("sfe-caisse", "_sfepay._tcp", (ushort)port);
        profile.AddProperty("path", "/");
        _sd.Advertise(profile);
        _mdns.Start();
    }

    public async Task StopAsync()
    {
        _sd?.Dispose();
        _mdns?.Dispose();
        if (_app is not null) await _app.StopAsync();
    }
}