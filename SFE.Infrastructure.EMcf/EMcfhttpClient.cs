using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SFE.Application.Interfaces;

namespace SFE.Infrastructure.EMcf;

/// <summary>
/// Client e-MCF — API REST DGI.
/// Production : https://edef.dgirdc.cd
/// Test       : https://developper.dgirdc.cd/edef
///
/// CONTRACT: Public methods NEVER throw. They always return a structured
/// result with Success=false and a populated ErrorMessage on failure.
/// This is required so the resolver can reliably trigger fallback.
///
/// CONNECTION POOL HARDENING:
///   - PooledConnectionLifetime = 60s : sockets are recycled, so a network
///     drop cannot permanently poison the pool with half-open TCP connections.
///   - PooledConnectionIdleTimeout = 15s : idle sockets are dropped quickly.
///   - ConnectTimeout = 5s : we discover an unreachable host fast, instead of
///     hanging for the 30s end-to-end timeout.
///   - Overall Timeout reduced from 30s → 15s for faster fallback.
/// </summary>
public class EMcfHttpClient : IFiscalDeviceService, IDisposable
{
    private readonly HttpClient _http;
    private readonly SocketsHttpHandler _handler;
    private readonly string _invoiceUrl;
    private readonly string _infoUrl;
    private readonly string _nif;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EMcfHttpClient(string baseUrl, string token, string nif)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("URL API e-MCF manquante", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token e-MCF manquant", nameof(token));

        _nif = nif ?? "";
        var trimmed = baseUrl.TrimEnd('/');
        _invoiceUrl = $"{trimmed}/api/invoice";
        _infoUrl = $"{trimmed}/api/info";

        _handler = new SocketsHttpHandler
        {
            // Recycle every connection at most every 60s — guarantees that
            // a half-open socket left over from a network drop is replaced
            // within one minute even if the OS doesn't notice it's dead.
            PooledConnectionLifetime = TimeSpan.FromSeconds(60),

            // Drop idle sockets aggressively so we don't reuse stale ones
            // after a brief network blip.
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),

            // Don't wait 30s to discover the host is unreachable.
            ConnectTimeout = TimeSpan.FromSeconds(5),

            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,

            // Honor system DNS — re-resolved naturally on each new connection.
            UseProxy = true,
        };

        _http = new HttpClient(_handler, disposeHandler: false)
        {
            // Tighter end-to-end timeout: failures should surface quickly so
            // the resolver can fall back without freezing the UI.
            Timeout = TimeSpan.FromSeconds(15)
        };

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        Debug.WriteLine($"[e-MCF] Client built for {trimmed} (timeout=15s, connect=5s, pool-lifetime=60s)");
    }

    private static string DescribeException(Exception ex) => ex switch
    {
        TaskCanceledException tce when tce.InnerException is TimeoutException
            => "Timeout e-MCF (>15s)",
        TaskCanceledException => "Timeout e-MCF (>15s)",
        OperationCanceledException => "Opération e-MCF annulée",
        HttpRequestException hre => $"Connexion e-MCF échouée: {hre.Message}",
        JsonException je => $"Réponse e-MCF invalide: {je.Message}",
        _ => $"Erreur e-MCF: {ex.Message}"
    };

    // ══════════════════════════════════════
    // GET /api/invoice/  → Status
    // ══════════════════════════════════════

    public async Task<FiscalStatusResult> GetStatusAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<StatusResponseDto>($"{_invoiceUrl}/", JsonOpts);

            if (resp == null)
                return new FiscalStatusResult { Success = false, ErrorMessage = "Réponse vide e-MCF" };

            var pendingList = resp.PendingRequestsList?
                .Select(p => new PendingInvoiceInfo { Uid = p.Uid, Date = p.Date })
                .ToList() ?? new List<PendingInvoiceInfo>();

            return new FiscalStatusResult
            {
                Success = resp.Status,
                NIM = resp.Nim,
                NIF = resp.Nif,
                ErrorMessage = resp.Status ? null : "API e-MCF non opérationnelle",
                PendingCount = resp.PendingRequestsCount,
                PendingInvoices = pendingList
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] GetStatus failed: {ex.GetType().Name}: {ex.Message}");
            return new FiscalStatusResult
            {
                Success = false,
                ErrorMessage = DescribeException(ex),
                PendingCount = 0,
                PendingInvoices = new List<PendingInvoiceInfo>()
            };
        }
    }

    // ══════════════════════════════════════
    // POST /api/invoice/  → Submit
    // ══════════════════════════════════════

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        try
        {
            var dto = MapToDto(request);

            var postResp = await _http.PostAsJsonAsync($"{_invoiceUrl}/", dto, JsonOpts);
            var rawJson = await postResp.Content.ReadAsStringAsync();
            Debug.WriteLine($"[e-MCF SubmitInvoice] {(int)postResp.StatusCode} | {rawJson}");

            if (!postResp.IsSuccessStatusCode)
            {
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = ((int)postResp.StatusCode).ToString(),
                    ErrorMessage = $"HTTP {postResp.StatusCode}: {rawJson}"
                };
            }

            InvoiceResponseDataDto? invoiceResp;
            try
            {
                invoiceResp = JsonSerializer.Deserialize<InvoiceResponseDataDto>(rawJson, JsonOpts);
            }
            catch (JsonException jex)
            {
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorMessage = $"Réponse e-MCF illisible: {jex.Message}"
                };
            }

            if (invoiceResp == null)
                return new FiscalSubmitResult { Success = false, ErrorMessage = "Réponse vide du serveur e-MCF" };

            if (!string.IsNullOrEmpty(invoiceResp.ErrorCode))
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = invoiceResp.ErrorCode,
                    ErrorMessage = invoiceResp.ErrorDesc
                };

            return new FiscalSubmitResult
            {
                Success = true,
                Uid = invoiceResp.Uid,
                TotalTTC = invoiceResp.Total,
                TotalTVA = invoiceResp.Vtotal
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] SubmitInvoice failed: {ex.GetType().Name}: {ex.Message}");
            return new FiscalSubmitResult { Success = false, ErrorMessage = DescribeException(ex) };
        }
    }

    // ══════════════════════════════════════
    // PUT /api/invoice/{uid}/CONFIRM
    // ══════════════════════════════════════

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(string uid, decimal totalTTC, decimal totalTVA)
    {
        try
        {
            var body = new FinalizeInvoiceRequestDataDto { Total = totalTTC, Vtotal = totalTVA };
            var content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json");

            var resp = await _http.PutAsync($"{_invoiceUrl}/{uid}/CONFIRM", content);
            var rawJson = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine($"[e-MCF FinalizeInvoice] {(int)resp.StatusCode} | {rawJson}");

            if (!resp.IsSuccessStatusCode)
            {
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = ((int)resp.StatusCode).ToString(),
                    ErrorMessage = $"Confirmation échouée: {rawJson}"
                };
            }

            FinalizeInvoiceResponseDataDto? finalResp;
            try
            {
                finalResp = JsonSerializer.Deserialize<FinalizeInvoiceResponseDataDto>(rawJson, JsonOpts);
            }
            catch (JsonException jex)
            {
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorMessage = $"Réponse confirmation illisible: {jex.Message}"
                };
            }

            if (finalResp == null)
                return new FiscalFinalizeResult { Success = false, ErrorMessage = "Réponse confirmation vide" };

            if (!string.IsNullOrEmpty(finalResp.ErrorCode))
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = finalResp.ErrorCode,
                    ErrorMessage = finalResp.ErrorDesc
                };

            return new FiscalFinalizeResult
            {
                Success = true,
                CodeDEFDGI = finalResp.CodeDEFDGI,
                QRCode = finalResp.QrCode,
                NIM = finalResp.Nim,
                Counters = finalResp.Counters,
                DateTime = finalResp.InvoiceDateTime
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] FinalizeInvoice failed: {ex.GetType().Name}: {ex.Message}");
            return new FiscalFinalizeResult { Success = false, ErrorMessage = DescribeException(ex) };
        }
    }

    // ══════════════════════════════════════
    // PUT /api/invoice/{uid}/CANCEL
    // ══════════════════════════════════════

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        try
        {
            var resp = await _http.PutAsync($"{_invoiceUrl}/{uid}/CANCEL", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] CancelPending failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // GetDetailedInfoAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync()
    {
        var info = new FiscalDeviceDetailedInfo { DeviceTypeLabel = "e-MCF" };

        // ── 1. Invoice API status ──
        StatusResponseDto? statusResp = null;
        try
        {
            statusResp = await _http.GetFromJsonAsync<StatusResponseDto>($"{_invoiceUrl}/", JsonOpts);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] /invoice status failed: {ex.GetType().Name}: {ex.Message}");
            return new FiscalDeviceDetailedInfo
            {
                Success = false,
                DeviceTypeLabel = "e-MCF",
                ConnectionStatus = "DIS",
                ErrorMessage = DescribeException(ex)
            };
        }

        if (statusResp == null)
        {
            info.Success = false;
            info.ErrorMessage = "Réponse statut e-MCF vide";
            info.ConnectionStatus = "DIS";
            return info;
        }

        info.Success = statusResp.Status;
        info.NIM = statusResp.Nim;
        info.NIF = statusResp.Nif;
        info.ServerDateTime = statusResp.ServerDateTime;
        info.DeviceDateTime = statusResp.ServerDateTime;
        info.TokenValidUntil = statusResp.TokenValid;
        info.PendingRequestsCount = statusResp.PendingRequestsCount;
        info.ConnectionStatus = statusResp.Status ? "CON" : "DIS";
        info.LastServerConnection = statusResp.Status ? DateTime.Now : null;

        if (!statusResp.Status)
            info.ErrorMessage = "API e-MCF non opérationnelle";

        // ── 2. Info API — taxpayer & shop details ──
        try
        {
            var infoResp = await _http.GetFromJsonAsync<InfoResponseDto>($"{_infoUrl}/status", JsonOpts);
            if (infoResp != null)
            {
                info.ApiVersion = infoResp.Version;
                if (infoResp.EmcfList?.Count > 0)
                {
                    var activeEmcf = infoResp.EmcfList.FirstOrDefault(e => e.Nim == info.NIM)
                                     ?? infoResp.EmcfList[0];

                    info.TaxpayerName = activeEmcf.ShopName;
                    info.TaxpayerAddress = activeEmcf.Address1;
                    info.TaxpayerCity = activeEmcf.Address3;
                    info.TaxpayerPhone = activeEmcf.Contact1;
                    info.TaxpayerEmail = activeEmcf.Contact2;
                    info.EmcfStatus = activeEmcf.Status;

                    info.EmcfDevices = infoResp.EmcfList.Select(e => new EmcfDeviceInfo
                    {
                        NIM = e.Nim,
                        Status = e.Status,
                        ShopName = e.ShopName,
                        Address = string.Join(", ",
                            new[] { e.Address1, e.Address2, e.Address3 }
                            .Where(s => !string.IsNullOrWhiteSpace(s))),
                        City = e.Address3,
                        Phone = e.Contact1,
                        Email = e.Contact2
                    }).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] /info/status failed: {ex.GetType().Name}: {ex.Message}");
        }

        // ── 3. Tax groups ──
        try
        {
            var taxResp = await _http.GetFromJsonAsync<TaxGroupsDto>($"{_infoUrl}/taxGroups", JsonOpts);
            if (taxResp != null)
                info.TaxRates = taxResp.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] /info/taxGroups failed: {ex.GetType().Name}: {ex.Message}");
        }

        // ── 4. Currency rates ──
        try
        {
            var ratesResp = await _http.GetFromJsonAsync<List<CurrencyRateDto>>($"{_infoUrl}/currencyRates", JsonOpts);
            if (ratesResp != null)
            {
                info.CurrencyRates = ratesResp.Select(r => new CurrencyRateInfo
                {
                    Code = r.Type,
                    Description = r.Description,
                    Date = r.Date,
                    Rate = r.Rate
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[e-MCF] /info/currencyRates failed: {ex.GetType().Name}: {ex.Message}");
        }

        return info;
    }

    // ══════════════════════════════════════════════════════════════
    // GetServerConnectionStatusAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync()
    {
        try
        {
            var status = await GetStatusAsync();
            return new FiscalServerConnectionResult
            {
                Success = status.Success,
                LastServerConnection = status.Success ? DateTime.Now : null,
                ConnectionStatus = status.Success ? "CON" : "DIS",
                TransactionsPending = status.PendingCount,
                ErrorMessage = status.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            return new FiscalServerConnectionResult
            {
                Success = false,
                ConnectionStatus = "DIS",
                ErrorMessage = DescribeException(ex)
            };
        }
    }

    // ══════════════════════════════════════════════════════════════
    // MAPPING — unchanged
    // ══════════════════════════════════════════════════════════════

    private InvoiceRequestDataDto MapToDto(FiscalInvoiceRequest request)
    {
        var dto = new InvoiceRequestDataDto
        {
            Nif = request.NIF ?? "",
            Rn = request.InvoiceNumber ?? "",
            Mode = (request.PriceMode ?? "TTC").ToLowerInvariant(),
            Isf = request.ISF ?? "",
            Type = request.InvoiceType ?? "FV",
            Operator = new OperatorDto
            {
                Id = request.OperatorId ?? "",
                Name = request.OperatorName ?? ""
            }
        };

        string curCode = request.CurrencyCode ?? "CDF";
        if (string.IsNullOrWhiteSpace(curCode)) curCode = "CDF";
        dto.CurCode = curCode;
        dto.CurDate = (request.CurrencyDate ?? DateTime.Now)
            .ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        dto.CurRate = request.CurrencyRate ?? 0m;

        dto.Cmta = request.CommentA ?? "";
        dto.Cmtb = request.CommentB ?? "";
        dto.Cmtc = request.CommentC ?? "";
        dto.Cmtd = request.CommentD ?? "";
        dto.Cmte = request.CommentE ?? "";
        dto.Cmtf = request.CommentF ?? "";
        dto.Cmtg = request.CommentG ?? "";
        dto.Cmth = request.CommentH ?? "";

        dto.Reference = request.Reference ?? "";
        dto.ReferenceType = request.ReferenceType ?? "";
        dto.ReferenceDesc = request.ReferenceDesc ?? "";

        if (request.Client != null)
        {
            dto.Client = new ClientDto
            {
                Nif = request.Client.NIF ?? "",
                Name = request.Client.Name ?? "",
                Contact = request.Client.Contact ?? "",
                Address = request.Client.Address ?? "",
                Type = request.Client.Type ?? "PP",
                TypeDesc = request.Client.TypeDesc ?? "Personne physique"
            };
        }
        else
        {
            dto.Client = new ClientDto
            {
                Nif = "",
                Name = "PP",
                Contact = "",
                Address = "",
                Type = "PP",
                TypeDesc = "Personne physique"
            };
        }

        dto.Items = request.Items.Select(i => new ItemDto
        {
            Code = i.Code ?? "",
            Type = i.Type ?? "BIE",
            Name = i.Name ?? "",
            Price = Math.Round(i.Price, 2).ToString(CultureInfo.InvariantCulture),
            Quantity = i.Quantity,
            TaxGroup = i.TaxGroup ?? "A",
            TaxSpecificValue = string.IsNullOrWhiteSpace(i.TaxSpecificValue) ? "0%" : i.TaxSpecificValue,
            TaxSpecificAmount = i.TaxSpecificAmount ?? 0m,
            OriginalPrice = i.OriginalPrice ?? i.Price,
            PriceModification = i.PriceModification ?? ""
        }).ToList();

        if (request.Payments.Count > 0)
        {
            dto.Payment = request.Payments.Select(p => new PaymentDto
            {
                Name = p.Name ?? "ESPECES",
                Amount = p.Amount,
                CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode) ? curCode : p.CurrencyCode,
                CurrencyRate = p.CurrencyRate ?? dto.CurRate
            }).ToList();
        }
        else
        {
            dto.Payment = new List<PaymentDto>();
        }

        return dto;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _http.Dispose(); } catch { }
        try { _handler.Dispose(); } catch { }
    }
}