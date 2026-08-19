using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using System.Diagnostics;

namespace SFE.Application.Services;

public class BulkInvoiceService : IBulkInvoiceService
{
    private readonly IExcelInvoiceParser _parser;
    private readonly InvoiceService _invoiceService;
    private readonly ITimeProvider _time;

    /// <summary>Delay between two MCF invoices, to let the serial port breathe.</summary>
    public TimeSpan InterInvoiceDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Hard cap to protect operators from typing errors.</summary>
    public int MaxInvoicesPerBatch { get; set; } = 500;

    public BulkInvoiceService(
        IExcelInvoiceParser parser,
        InvoiceService invoiceService,
        ITimeProvider time)
    {
        _parser = parser;
        _invoiceService = invoiceService;
        _time = time;
    }

    public Task<BulkParseResult> ParseAndValidateAsync(
        Stream xlsxStream,
        int pointOfSaleId,
        string operatorId,
        string operatorName,
        CancellationToken ct = default)
        => _parser.ParseAsync(xlsxStream, pointOfSaleId, operatorId, operatorName, ct);

    public async Task<BulkExecutionResult> ExecuteAsync(
        BulkParseResult parsed,
        IProgress<BulkProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!parsed.IsValid)
            throw new InvalidOperationException("Le lot contient des erreurs et ne peut pas être exécuté.");

        if (parsed.Invoices.Count > MaxInvoicesPerBatch)
            throw new InvalidOperationException(
                $"Le lot dépasse la limite autorisée ({MaxInvoicesPerBatch}). Divisez-le en plusieurs fichiers.");

        var result = new BulkExecutionResult { StartedAt = _time.UtcNow };
        var sw = Stopwatch.StartNew();
        int successes = 0, failures = 0, total = parsed.Invoices.Count;

        for (int i = 0; i < total; i++)
        {
            if (ct.IsCancellationRequested)
            {
                result.WasCancelled = true;
                break;
            }

            var invoice = parsed.Invoices[i];
            var reference = invoice.CommentA; // "Import Excel — Ref: F-XXX"

            progress?.Report(new BulkProgress
            {
                Current = i + 1,
                Total = total,
                CurrentReference = reference,
                Phase = "processing",
                Successes = successes,
                Failures = failures,
                Elapsed = sw.Elapsed,
                EstimatedRemaining = EstimateRemaining(sw.Elapsed, i, total)
            });

            var row = new BulkRowResult
            {
                Reference = reference,
                ClientName = invoice.ClientName,
                ProcessedAt = _time.UtcNow
            };

            try
            {
                // Numéro de facture — le service refera un check anti-collision
                if (string.IsNullOrEmpty(invoice.InvoiceNumber))
                {
                    invoice.InvoiceNumber = await _invoiceService
                        .GenerateInvoiceNumberAsync(invoice.Type, invoice.PointOfSaleId);
                }

                // ══ APPEL UNIQUE : réutilise strictement la logique POS ══
                var norm = await _invoiceService.NormalizeInvoiceAsync(invoice);

                row.InvoiceNumber = invoice.InvoiceNumber;
                row.Success = norm.Success;
                row.InvoiceId = norm.InvoiceId;
                row.CodeDEFDGI = norm.CodeDEFDGI;
                row.QRCodeContent = norm.QRCodeContent;
                row.ErrorMessage = norm.ErrorMessage;
                row.TotalTTC = invoice.TotalTTC;

                if (norm.Success) successes++;
                else failures++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BulkInvoiceService] Facture '{reference}' — exception: {ex}");
                row.Success = false;
                row.ErrorMessage = ex.GetBaseException().Message;
                failures++;
            }

            result.Results.Add(row);

            progress?.Report(new BulkProgress
            {
                Current = i + 1,
                Total = total,
                CurrentReference = reference,
                Phase = row.Success ? "success" : "failure",
                LastError = row.Success ? null : row.ErrorMessage,
                Successes = successes,
                Failures = failures,
                Elapsed = sw.Elapsed,
                EstimatedRemaining = EstimateRemaining(sw.Elapsed, i + 1, total)
            });

            // Pause pour le port série MCF, mais pas après la dernière
            if (i < total - 1 && InterInvoiceDelay > TimeSpan.Zero)
            {
                try { await Task.Delay(InterInvoiceDelay, ct); }
                catch (OperationCanceledException) { result.WasCancelled = true; break; }
            }
        }

        sw.Stop();
        result.CompletedAt = _time.UtcNow;

        progress?.Report(new BulkProgress
        {
            Current = result.Results.Count,
            Total = total,
            Phase = "done",
            Successes = successes,
            Failures = failures,
            Elapsed = sw.Elapsed
        });

        return result;
    }

    private static TimeSpan EstimateRemaining(TimeSpan elapsed, int done, int total)
    {
        if (done <= 0) return TimeSpan.Zero;
        var avg = elapsed.TotalSeconds / done;
        return TimeSpan.FromSeconds(avg * (total - done));
    }
}