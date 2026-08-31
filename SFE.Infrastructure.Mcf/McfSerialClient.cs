using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Mcf;

/// <summary>
/// Client MCF physique — communication port série RS232/USB.
/// Implémente le flux spec MCF: C3→C0→31h(×N)→36h→33h→35h puis 38h.
/// </summary>
public class McfSerialClient : IFiscalDeviceService, IDisposable
{
    private SerialPort? _port;
    private byte _seq = 0x20;
    private readonly string _comPort;
    private readonly int _baudRate;
    private readonly ITimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    // Default frame-read timeout for "fast" commands (C1h, C2h, 31h, …).
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(5);

    // 38h on FA/EA can legitimately block on the DGI round-trip. The Incotex
    // firmware does NOT reliably send P: placeholders during the wait — many
    // revisions just stay silent on the UART until the server verdict is in.
    // 60 s is comfortably above the worst real-world burst we've measured
    // (cold PDP-context activation + DGI verify ≈ 25 s on a marginal link).
    private static readonly TimeSpan CreditNoteReadTimeout = TimeSpan.FromSeconds(60);

    // Track whether the currently open invoice is a credit note (FA/EA)
    // so FinalizeInvoiceAsync can apply the proper timeout & pre-checks.
    private bool _currentInvoiceIsCreditNote;

    public bool IsConnected => _port?.IsOpen == true;

    public McfSerialClient(string comPort, ITimeProvider time, int baudRate = 115200)
    {
        _comPort = comPort;
        _baudRate = baudRate;
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public void Connect()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McfSerialClient));

        if (_port != null)
        {
            try { if (_port.IsOpen) _port.Close(); } catch { /* ignore */ }
            try { _port.Dispose(); } catch { /* ignore */ }
            _port = null;
        }

        var port = new SerialPort(_comPort, _baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 5000,
            WriteTimeout = 2000
        };

        try
        {
            port.Open();
            _port = port;
        }
        catch
        {
            try { port.Dispose(); } catch { /* ignore */ }
            throw;
        }
    }

    private byte NextSeq()
    {
        _seq++;
        if (_seq > 0xFF) _seq = 0x20;
        return _seq;
    }

    // ══════════════════════════════════════
    // Helper — MCF local DateTime → DateTimeOffset
    // ══════════════════════════════════════

    /// <summary>
    /// The MCF firmware emits timestamps in the device's wall-clock time
    /// (no timezone marker). We attach the current machine's local offset,
    /// sourced from <see cref="ITimeProvider.LocalNow"/>, so that the
    /// resulting <see cref="DateTimeOffset"/> round-trips cleanly through
    /// persistence and display layers.
    /// </summary>
    private DateTimeOffset ToLocalOffset(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = _time.LocalNow.Offset;
        return new DateTimeOffset(unspecified, offset);
    }

    // ══════════════════════════════════════
    // ENVOI / RÉCEPTION
    // ══════════════════════════════════════

    private async Task<McfResponse> SendCommandAsync(
        byte cmd, string? data = null, TimeSpan? readTimeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McfSerialClient));
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("MCF non connecté");

        await _lock.WaitAsync();
        try
        {
            byte seq = NextSeq();
            byte[] frame = McfProtocol.BuildCommand(seq, cmd, data);

            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);

            return await ReadResponseAsync(readTimeout ?? DefaultReadTimeout);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<McfResponse> ReadResponseAsync(TimeSpan timeout)
    {
        var buffer = new byte[512];
        var received = new List<byte>();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (_port!.BytesToRead > 0)
            {
                int count = _port.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < count; i++) received.Add(buffer[i]);

                if (received.Count == 1)
                {
                    if (received[0] == McfProtocol.NAK)
                        return new McfResponse { IsNak = true };
                    if (received[0] == McfProtocol.SYN)
                    {
                        received.Clear();
                        continue;
                    }
                }

                if (received.Count >= 2 && received[^1] == McfProtocol.ETX)
                {
                    var response = McfProtocol.ParseResponse(received.ToArray(), received.Count);
                    if (response != null) return response;
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Pas de réponse du MCF dans le délai imparti ({timeout.TotalSeconds:F0}s)");
    }

    // ══════════════════════════════════════
    // GetStatusAsync — C1h
    // ══════════════════════════════════════

    public async Task<FiscalStatusResult> GetStatusAsync()
    {
        try
        {
            var resp = await SendCommandAsync(McfProtocol.CMD_STATUS);
            if (resp.IsError)
                return new FiscalStatusResult
                {
                    Success = false,
                    ErrorMessage = "Erreur communication MCF",
                    PendingCount = 0,
                    PendingInvoices = new List<PendingInvoiceInfo>()
                };

            var f = resp.Fields;
            if (f.Length < 2)
                return new FiscalStatusResult
                {
                    Success = false,
                    ErrorMessage = "Réponse MCF incomplète",
                    PendingCount = 0,
                    PendingInvoices = new List<PendingInvoiceInfo>()
                };

            bool hasPending = f.Length >= 3
                && !string.IsNullOrWhiteSpace(f[2])
                && f[2] != "0" && f[2].ToUpperInvariant() != "N";

            var pendingList = new List<PendingInvoiceInfo>();
            if (hasPending)
            {
                pendingList.Add(new PendingInvoiceInfo
                {
                    Uid = "MCF",
                    Date = _time.LocalNow
                });
            }

            return new FiscalStatusResult
            {
                Success = true,
                NIM = f[0],
                NIF = f[1],
                PendingCount = hasPending ? 1 : 0,
                PendingInvoices = pendingList
            };
        }
        catch (Exception ex)
        {
            return new FiscalStatusResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                PendingCount = 0,
                PendingInvoices = new List<PendingInvoiceInfo>()
            };
        }
    }

    // ══════════════════════════════════════
    // SubmitInvoiceAsync — flux C3→C0→31h→36h→33h→35h
    // ══════════════════════════════════════

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        try
        {
            await SendClientInfoAsync(request);

            var openResp = await OpenInvoiceAsync(request);
            if (openResp.StartsWith("E:"))
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = "C0",
                    ErrorMessage = openResp[2..]
                };

            foreach (var item in request.Items)
            {
                var itemResp = await RegisterItemAsync(item, request.PriceMode);
                if (itemResp.StartsWith("E:"))
                    return new FiscalSubmitResult
                    {
                        Success = false,
                        ErrorCode = "31",
                        ErrorMessage = itemResp[2..]
                    };
            }

            await SendCommentsAsync(request);

            var subtotalResp = await SendCommandAsync(McfProtocol.CMD_SUBTOTAL);

            decimal serverTTC = 0, serverTVA = 0, serverTS = 0, serverUSD = 0;
            var groupAmounts = new Dictionary<string, decimal>();
            var groupTVA = new Dictionary<string, decimal>();

            if (!subtotalResp.IsError && subtotalResp.Fields.Length >= 35)
            {
                var ic = CultureInfo.InvariantCulture;
                var groups = new[]
                {
                    "A","B","C","D","E","F","G","H",
                    "I","J","K","L","M","N","O","P"
                };

                decimal.TryParse(subtotalResp.Fields[0], NumberStyles.Any, ic, out serverTTC);

                for (int i = 0; i < 16; i++)
                {
                    if (decimal.TryParse(subtotalResp.Fields[1 + i], NumberStyles.Any, ic, out var amt) && amt != 0)
                        groupAmounts[groups[i]] = amt;
                }

                for (int i = 0; i < 16; i++)
                {
                    if (decimal.TryParse(subtotalResp.Fields[17 + i], NumberStyles.Any, ic, out var tva))
                    {
                        if (tva != 0)
                            groupTVA[groups[i]] = tva;
                        serverTVA += tva;
                    }
                }

                decimal.TryParse(subtotalResp.Fields[33], NumberStyles.Any, ic, out serverTS);
                decimal.TryParse(subtotalResp.Fields[34], NumberStyles.Any, ic, out serverUSD);
            }

            foreach (var payment in request.Payments)
                await RegisterPaymentAsync(payment);

            return new FiscalSubmitResult
            {
                Success = true,
                Uid = "MCF",
                TotalTTC = serverTTC,
                TotalTVA = serverTVA,
                TotalTS = serverTS,
                TotalUSD = serverUSD,
                GroupAmounts = groupAmounts,
                GroupTVA = groupTVA
            };
        }
        catch (Exception ex)
        {
            return new FiscalSubmitResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ══════════════════════════════════════
    // FinalizeInvoiceAsync — 38h (strict + DGI-aware)
    // ══════════════════════════════════════

    // ══════════════════════════════════════
    // FinalizeInvoiceAsync — 38h (DGI-aware, lenient on R: prefix)
    // ══════════════════════════════════════

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(
        string uid, decimal totalTTC, decimal totalTVA)
    {
        // Snapshot the flag — we reset it in the finally so a future call
        // (after a fresh OpenInvoice) is never polluted by a previous run.
        bool isCreditNote = _currentInvoiceIsCreditNote;

        try
        {
            // ─────────────────────────────────────────────────────────
            // SOFT pre-flight diagnostics for credit notes only.
            // ─────────────────────────────────────────────────────────
            if (isCreditNote)
            {
                try
                {
                    var conn = await GetServerConnectionStatusAsync();

                    if (conn.Success)
                    {
                        Debug.WriteLine(
                            $"[38h pre-flight] STA={conn.ConnectionStatus} " +
                            $"sent={conn.TransactionsSent} pending={conn.TransactionsPending} " +
                            $"lastConn={conn.LastServerConnection:yyyy-MM-dd HH:mm:ss} " +
                            $"lastErr='{conn.LastError}'");

                        bool neverConnected = conn.LastServerConnection == null
                                               || conn.LastServerConnection == DateTimeOffset.MinValue;

                        bool hasHardTransportError = !string.IsNullOrWhiteSpace(conn.LastError)
                            && (conn.LastError.Contains("APN", StringComparison.OrdinalIgnoreCase)
                             || conn.LastError.Contains("SIM", StringComparison.OrdinalIgnoreCase)
                             || conn.LastError.Contains("NO NETWORK", StringComparison.OrdinalIgnoreCase)
                             || conn.LastError.Contains("PAS DE RESEAU", StringComparison.OrdinalIgnoreCase));

                        if (neverConnected && hasHardTransportError)
                        {
                            await TryCancelInvoiceAsync("preflight-no-network");

                            return new FiscalFinalizeResult
                            {
                                Success = false,
                                ErrorCode = "38",
                                ErrorMessage =
                                    $"Le MCF n'a jamais établi de connexion avec le serveur DGI " +
                                    $"(erreur transport: {conn.LastError}). " +
                                    "Vérifiez la SIM, l'APN et la couverture réseau du MCF " +
                                    "avant de générer une facture d'avoir."
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[38h pre-flight] C2h failed (non-fatal): {ex.Message}");
                }
            }

            var ic = CultureInfo.InvariantCulture;
            string data = $"{totalTTC.ToString("F2", ic)},{totalTVA.ToString("F2", ic)}";

            // ─────────────────────────────────────────────────────────
            // Iterative "P:" polling with bounded wall-clock timeout.
            // ─────────────────────────────────────────────────────────
            var maxWait = isCreditNote
                ? TimeSpan.FromSeconds(90)   // FA/EA: server verification
                : TimeSpan.FromSeconds(15);  // FV/FT/EV/ET: local only

            var pollInterval = TimeSpan.FromMilliseconds(500);
            var sw = Stopwatch.StartNew();
            McfResponse resp;
            int pendingCount = 0;
            string? lastPendingReason = null;

            var perFrameTimeout = isCreditNote ? CreditNoteReadTimeout : DefaultReadTimeout;

            while (true)
            {
                try
                {
                    resp = await SendCommandAsync(McfProtocol.CMD_FINALIZE, data, perFrameTimeout);
                }
                catch (TimeoutException tex)
                {
                    pendingCount++;
                    Debug.WriteLine(
                        $"[38h] frame timeout #{pendingCount} after " +
                        $"{perFrameTimeout.TotalSeconds:F0}s ({tex.Message}) — " +
                        $"elapsed {sw.Elapsed.TotalSeconds:F0}s / max {maxWait.TotalSeconds:F0}s");

                    try { _port?.DiscardInBuffer(); } catch { /* ignore */ }

                    if (sw.Elapsed > maxWait)
                    {
                        await TryCancelInvoiceAsync(
                            $"frame-timeout-budget-exhausted (isCreditNote={isCreditNote}, " +
                            $"attempts={pendingCount})");

                        return new FiscalFinalizeResult
                        {
                            Success = false,
                            ErrorCode = "38",
                            ErrorMessage = isCreditNote
                                ? $"Délai de vérification de la facture d'avoir dépassé " +
                                  $"({maxWait.TotalSeconds:F0}s, {pendingCount} tentatives sans réponse). " +
                                  "Le MCF n'a pas répondu sur le port série pendant la vérification DGI. " +
                                  "Vérifiez la connectivité réseau du MCF et réessayez ultérieurement."
                                : $"Délai de normalisation dépassé ({maxWait.TotalSeconds:F0}s)."
                        };
                    }

                    await Task.Delay(pollInterval);
                    continue;
                }

                if (resp.IsError)
                    return new FiscalFinalizeResult
                    {
                        Success = false,
                        ErrorCode = "38",
                        ErrorMessage = "Erreur communication 38h"
                    };

                // Always log the raw response — invaluable for diagnosing.
                Debug.WriteLine(
                    $"[38h] << raw='{Truncate(resp.Data, 300)}' " +
                    $"fields={resp.Fields?.Length ?? 0}");

                // ── P:{MID},{NIF},{FN},{C|MV},{PR} — still pending ──
                if (resp.Data.StartsWith("P:", StringComparison.Ordinal))
                {
                    pendingCount++;
                    var pParts = resp.Data[2..].Split(',');
                    if (pParts.Length >= 5 && !string.IsNullOrWhiteSpace(pParts[4]))
                        lastPendingReason = pParts[4];

                    if (sw.Elapsed > maxWait)
                    {
                        await TryCancelInvoiceAsync(
                            $"poll-timeout (isCreditNote={isCreditNote}, " +
                            $"attempts={pendingCount}, " +
                            $"lastReason='{lastPendingReason ?? ""}')");

                        var msg = isCreditNote
                            ? $"Délai de vérification de la facture d'avoir dépassé " +
                              $"({maxWait.TotalSeconds:F0}s, {pendingCount} tentatives). " +
                              "Le serveur DGI n'a pas répondu à temps. " +
                              (string.IsNullOrEmpty(lastPendingReason)
                                  ? ""
                                  : $"Raison MCF: {lastPendingReason}. ") +
                              "Vérifiez la connectivité réseau du MCF et réessayez ultérieurement."
                            : $"Délai de normalisation dépassé ({maxWait.TotalSeconds:F0}s).";

                        return new FiscalFinalizeResult
                        {
                            Success = false,
                            ErrorCode = "38",
                            ErrorMessage = msg
                        };
                    }

                    await Task.Delay(pollInterval);
                    continue;
                }

                break;   // R:, E:, or raw success payload
            }

            // ─────────────────────────────────────────────────────────
            // E:{ER} — error or cancellation
            // ─────────────────────────────────────────────────────────
            if (resp.Data.StartsWith("E:", StringComparison.Ordinal))
            {
                var errorMsg = resp.Data[2..].Trim();

                if (isCreditNote)
                    await TryCancelInvoiceAsync($"dgi-rejected: {Truncate(errorMsg, 80)}");

                if (isCreditNote && LooksLikeRefundVerificationFailure(errorMsg))
                {
                    errorMsg +=
                        "\n\nLe serveur DGI a rejeté la facture d'avoir car elle ne correspond pas " +
                        "à la facture de vente originale. Vérifiez :" +
                        "\n• Le code DEF/DGI de la facture d'origine (champ référence)" +
                        "\n• Que les articles de l'avoir EXISTENT sur la facture d'origine" +
                        "\n• Que les prix unitaires sont IDENTIQUES à ceux de l'origine" +
                        "\n• Que les groupes TVA / taux sont IDENTIQUES" +
                        "\n• Que la quantité avoir ≤ quantité originale" +
                        "\n• Que la facture d'origine est déjà synchronisée côté DGI " +
                          "(attendre quelques minutes si elle vient d'être émise)";
                }

                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = "38",
                    ErrorMessage = errorMsg
                };
            }

            // ─────────────────────────────────────────────────────────
            // SUCCESS payload — R:{FC},{TC},{FT},{DT},{MID},{NIF},[FN],{SIG}
            //
            // LENIENT prefix handling (restored to match prior working behavior):
            // some MCF firmwares emit the success payload with an "R:" prefix,
            // others emit it raw. We accept both, and only treat the response
            // as malformed if the field count is implausible for a finalize.
            // ─────────────────────────────────────────────────────────
            string rawData;
            if (resp.Data.StartsWith("R :", StringComparison.Ordinal))
                rawData = resp.Data[3..];
            else if (resp.Data.StartsWith("R:", StringComparison.Ordinal))
                rawData = resp.Data[2..];
            else
                rawData = resp.Data;   // ← restored fallback: no prefix → raw payload

            var f = rawData.Split(',');

            // Sanity guard: a real finalize payload always has ≥7 comma-separated
            // fields. Anything shorter is a stray/garbled frame, not a success.
            if (f.Length < 7)
            {
                Debug.WriteLine(
                    $"[38h] ✗ payload too short ({f.Length} fields). " +
                    $"Raw='{Truncate(resp.Data, 300)}'");

                if (isCreditNote)
                    await TryCancelInvoiceAsync("short-38h-payload");

                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = "38",
                    ErrorMessage = $"Réponse 38h incomplète ({f.Length} champs, attendu ≥ 7). " +
                                   $"Données reçues : '{Truncate(resp.Data, 120)}'"
                };
            }

            // Dual-layout decode:
            //   Modern firmware: 8 fields with FN at [6] and SIG/DEF at [7]
            //   Legacy firmware: 7 fields with no FN, SIG/DEF at [6]
            string fn = f.Length >= 8 ? f[6] : string.Empty;
            string sig = f.Length >= 8 ? f[7] : f[6];

            return new FiscalFinalizeResult
            {
                Success = true,
                CodeDEFDGI = sig,
                NIM = f[4],                                  // MID
                Counters = $"{f[0]}/{f[1]} {f[2]}",               // FC/TC FT
                DateTime = f[3],                                  // DT
                QRCode = BuildQrContent(f[4], sig, f[5], f[3])  // MID, SIG, NIF, DT
            };
        }
        catch (Exception ex)
        {
            return new FiscalFinalizeResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            // Always reset, even on exception, so the next OpenInvoice
            // starts from a clean slate.
            _currentInvoiceIsCreditNote = false;
        }
    }

    // Helper — heuristic match for refund verification errors so we can
    // attach actionable guidance. Matches both French and English wording
    // observed across MCF firmwares.
    private static bool LooksLikeRefundVerificationFailure(string err)
    {
        if (string.IsNullOrEmpty(err)) return false;
        var u = err.ToUpperInvariant();
        return u.Contains("VERIFICATION")
            || u.Contains("VÉRIFICATION")
            || u.Contains("REFUND")
            || u.Contains("AVOIR")
            || u.Contains("NO REFUND")
            || u.Contains("ECHEC DE LA VERIFICATION")
            || u.Contains("INCOHERENT")
            || u.Contains("INCOHÉRENT");
    }

    // Helper — safe truncation for log lines (keeps logs grep-friendly).
    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max] + "…";
    }

    /// <summary>
    /// Sends the 38h "C" cancel command and logs everything the MCF
    /// returned (or threw) so we can diagnose why a 38h flow had to be
    /// aborted. Never throws — best-effort cleanup, by design.
    /// </summary>
    /// <param name="reason">
    /// Human-readable tag identifying which call-site triggered the cancel
    /// (e.g. "preflight-no-network", "poll-timeout", "manual").
    /// </param>
    private async Task TryCancelInvoiceAsync(string reason)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 10 s — long enough to outwait the tail of a delayed 38h response,
            // short enough that a wedged port still surfaces quickly.
            var resp = await SendCommandAsync(
                McfProtocol.CMD_FINALIZE, "C", TimeSpan.FromSeconds(10));
            sw.Stop();

            // Build a one-line diagnostic dump. Fields are joined with '|'
            // so the log stays grep-friendly even with embedded commas.
            string fieldsDump = resp.Fields == null || resp.Fields.Length == 0
                ? "<none>"
                : string.Join(" | ", resp.Fields);

            Debug.WriteLine(
                $"[38h CANCEL] reason='{reason}' " +
                $"elapsed={sw.ElapsedMilliseconds}ms " +
                $"isNak={resp.IsNak} isError={resp.IsError} " +
                $"data='{Truncate(resp.Data, 200)}' " +
                $"fields=[{Truncate(fieldsDump, 300)}]");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Debug.WriteLine(
                $"[38h CANCEL] reason='{reason}' " +
                $"elapsed={sw.ElapsedMilliseconds}ms " +
                $"THREW {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ══════════════════════════════════════
    // CancelPendingInvoiceAsync
    // ══════════════════════════════════════

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var resp = await SendCommandAsync(McfProtocol.CMD_FINALIZE, "C");
            sw.Stop();

            string fieldsDump = resp.Fields == null || resp.Fields.Length == 0
                ? "<none>"
                : string.Join(" | ", resp.Fields);

            Debug.WriteLine(
                $"[38h CANCEL] reason='manual uid={uid}' " +
                $"elapsed={sw.ElapsedMilliseconds}ms " +
                $"isNak={resp.IsNak} isError={resp.IsError} " +
                $"data='{Truncate(resp.Data, 200)}' " +
                $"fields=[{Truncate(fieldsDump, 300)}]");

            return !resp.IsError;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"[38h CANCEL] reason='manual uid={uid}' " +
                $"THREW {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            _currentInvoiceIsCreditNote = false;
        }
    }

    // ══════════════════════════════════════
    // PRIVATE — MCF commands from FiscalInvoiceRequest
    // ══════════════════════════════════════

    private async Task SendClientInfoAsync(FiscalInvoiceRequest request)
    {
        if (request.Client == null)
        {
            await SendCommandAsync(McfProtocol.CMD_CLIENT_INFO, "");
            return;
        }

        var c = request.Client;
        var parts = new[]
        {
            c.Type ?? "PP",
            McfProtocol.EscapeData(c.NIF ?? ""),
            McfProtocol.EscapeData(c.Name ?? ""),
            McfProtocol.EscapeData(c.Address ?? ""),
            McfProtocol.EscapeData(c.Contact ?? ""),
            "",
            ""
        };

        await SendCommandAsync(McfProtocol.CMD_CLIENT_INFO, string.Join(",", parts));
    }

    private async Task<string> OpenInvoiceAsync(FiscalInvoiceRequest request)
    {
        var parts = new List<string>
        {
            McfProtocol.EscapeData(request.OperatorId),
            McfProtocol.EscapeData(request.OperatorName),
            request.NIF
        };

        bool isCreditNote = request.InvoiceType is "FA" or "EA";

        // Remember it for the upcoming 38h call.
        _currentInvoiceIsCreditNote = isCreditNote;

        if (isCreditNote)
        {
            parts.Add(request.InvoiceType);
            parts.Add(request.ReferenceType ?? "COR");
            parts.Add(request.Reference ?? "");
        }
        else
        {
            parts.Add(request.InvoiceType);
        }

        parts.Add(request.PriceMode);
        parts.Add(request.ISF);
        parts.Add(request.InvoiceNumber);

        if (request.CurrencyRate.HasValue)
        {
            var ic = CultureInfo.InvariantCulture;
            parts.Add(request.CurrencyRate.Value.ToString("F2", ic));
            parts.Add(request.CurrencyDate?.LocalDateTime.ToString("yyyyMMdd") ?? "");
        }

        var resp = await SendCommandAsync(McfProtocol.CMD_NEW_INVOICE, string.Join(",", parts));
        return resp.IsError ? "E:Communication" : resp.Data;
    }

    private async Task<string> RegisterItemAsync(FiscalItemInfo item, string priceMode)
    {
        var ic = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        // ---------- 1. NOM (article name, mandatory) ----------
        if (string.IsNullOrWhiteSpace(item.Name))
            return "E:ItemNameRequired";

        sb.Append(McfProtocol.EscapeData(item.Name));

        // ---------- 2. [LF DESC] (optional code/description) ----------
        if (!string.IsNullOrEmpty(item.Code))
        {
            sb.Append((char)0x0A);
            sb.Append(McfProtocol.EscapeData(item.Code));
        }

        // ---------- 3. TAB separator ----------
        sb.Append((char)0x09);

        // ---------- 4. ITYPE (BIE / SER / TAX / etc.) ----------
        if (string.IsNullOrWhiteSpace(item.Type))
            return "E:ItemTypeRequired";
        sb.Append(item.Type);

        sb.Append(',');

        // ---------- 5. TAX group (A / B / C / D / E / F) ----------
        if (string.IsNullOrWhiteSpace(item.TaxGroup))
            return "E:TaxGroupRequired";
        sb.Append(item.TaxGroup);

        // ---------- 6. TR% (VAT rate with 2 decimals) ----------
        sb.Append(item.TaxRate.ToString("F2", ic));
        sb.Append('%');

        // ---------- 7. Compute TS amount (needed for MON) ----------
        decimal ts = 0m;
        decimal? tsrRate = null;
        bool hasTs = false;

        if (!string.IsNullOrEmpty(item.TaxSpecificValue))
        {
            // Normalize: accept "10,0" / "10.0" / "10" → 10.00
            var tsrStr = item.TaxSpecificValue.Replace(',', '.').TrimEnd('%').Trim();
            if (decimal.TryParse(tsrStr, NumberStyles.Any, ic, out var parsed) && parsed > 0)
            {
                tsrRate = parsed;
                hasTs = true;
            }
        }

        if (hasTs)
        {
            // If caller supplied amount, trust it; else compute from rate
            ts = item.TaxSpecificAmount
                 ?? Math.Round((item.Price * item.Quantity) * tsrRate.Value / 100m, 2,
                               MidpointRounding.AwayFromZero);
        }
        else if (item.TaxSpecificAmount.HasValue && item.TaxSpecificAmount.Value > 0)
        {
            // Fixed-amount TS (no rate, just an amount) — still valid per spec
            ts = item.TaxSpecificAmount.Value;
            hasTs = true;
        }

        // ---------- 8. MON (line total) ----------
        // HT mode  → item.HT  (excl. VAT)
        // TTC mode → item.TTC (incl. VAT)
        decimal mon = priceMode?.ToUpperInvariant() == "HT"
            ? item.HT
            : item.TTC;
        sb.Append(mon.ToString("F2", ic));

        // ---------- 9. TAB separator ----------
        sb.Append((char)0x09);

        // ---------- 10. PR (unit price) ----------
        sb.Append(item.Price.ToString("F2", ic));

        // ---------- 11. [*QT] (only if quantity ≠ 1) ----------
        if (item.Quantity != 1m)
        {
            sb.Append('*');
            // Up to 3 decimals; trim trailing zeros for cleanliness, but keep at least integer form
            sb.Append(item.Quantity.ToString("0.###", ic));
        }

        // ---------- 12. [;T{TSR},{TS}[,{TSDEC}]] ----------
        if (hasTs)
        {
            sb.Append(";T");

            if (tsrRate.HasValue)
            {
                // "0.##" → 10 stays "10", 10.5 stays "10.5", 10.25 stays "10.25"
                sb.Append(tsrRate.Value.ToString("0.##", ic));
                sb.Append('%');
            }
            // else: fixed-amount TS → no rate token before the comma

            sb.Append(',');
            sb.Append(ts.ToString("F2", ic));

            //if (item.TaxSpecificDeclared.HasValue)
            //{
            //    sb.Append(',');
            //    sb.Append(item.TaxSpecificDeclared.Value.ToString("F2", ic));
            //}
        }

        // ---------- 13. [TAB PRORIG,PRDESC] (price modification) ----------
        //if (item.OriginalPrice.HasValue)
        //{
        //   sb.Append((char)0x09);
        //   sb.Append(item.OriginalPrice.Value.ToString("F2", ic));
        //   sb.Append(',');
        //   sb.Append(McfProtocol.EscapeData(item.PriceModification ?? ""));
        //}

        // ---------- Send ----------
        var payload = sb.ToString();

        // Optional: trace exactly what's being sent (very useful while debugging TS issues)
        Debug.WriteLine($"[31h REGISTER_ITEM] >> {payload}");

        var resp = await SendCommandAsync(McfProtocol.CMD_REGISTER_ITEM, payload);

        if (resp.IsError)
            return "E:Communication";

        return resp.Data;
    }


    private async Task SendCommentsAsync(FiscalInvoiceRequest request)
    {
        var comments = new (string id, string? val)[]
        {
            ("A", request.CommentA), ("B", request.CommentB),
            ("C", request.CommentC), ("D", request.CommentD),
            ("E", request.CommentE), ("F", request.CommentF),
            ("G", request.CommentG), ("H", request.CommentH)
        };

        foreach (var (id, val) in comments)
        {
            if (!string.IsNullOrEmpty(val))
            {
                await SendCommandAsync(McfProtocol.CMD_ADDITIONAL_INFO,
                    $"{id},{McfProtocol.EscapeData(val)}");
            }
        }
    }

    private async Task RegisterPaymentAsync(FiscalPaymentInfo payment)
    {
        string pa = payment.Name.ToUpperInvariant() switch
        {
            "ESPECES" => "E",
            "VIREMENT" => "V",
            "CARTEBANCAIRE" => "C",
            "MOBILEMONEY" => "M",
            "CHEQUES" => "D",
            "CREDIT" => "R",
            "AUTRE" => "A",
            _ => "E"
        };

        var ic = CultureInfo.InvariantCulture;
        string data = $"{pa}{payment.Amount.ToString("F2", ic)}";
        await SendCommandAsync(McfProtocol.CMD_PAYMENT, data);
    }

    // ══════════════════════════════════════
    // GetServerConnectionStatusAsync — C2h
    // ══════════════════════════════════════
    //
    // C2h response layout (per spec):
    //   f[0] = EC  — transactions ENVOYÉES au serveur DGI (cumulative ACKs)
    //   f[1] = DC  — transactions présentes DANS LE DISPOSITIF (cumulative
    //                lifetime counter, identical to C1h.TC on a healthy MCF)
    //   f[2] = DT  — date/heure de la dernière connexion serveur (yyyyMMddHHmmss)
    //   f[3] = STA — état session: CON | TRA | DIS
    //   f[4] = ER  — dernière erreur (optionnel)
    //
    // ⚠️ Important: DC is NOT the pending count. It is the total count of
    // transactions stored in the MCF since activation. The actual backlog
    // waiting to be reported to DGI is:
    //
    //        Pending = max(0, DC - EC)
    //
    // On a healthy, fully-synced device DC == EC ⇒ Pending == 0, even though
    // STA may legitimately read DIS (Incotex MCFs don't keep a permanent TCP
    // session — they batch-send and disconnect).
    //
    // ⚠️ Interpreting STA:
    //   CON = a TCP session to the DGI server is currently OPEN
    //   TRA = a transmission is in progress
    //   DIS = no session open right now (this is the NORMAL idle state)
    //
    // The fields that actually matter for diagnosing health are:
    //   - Pending (DC - EC): should not grow unbounded
    //   - LastServerConnection: should be recent (minutes/hours, not days)
    //   - LastError: should be empty or transient
    // ══════════════════════════════════════

    public async Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync()
    {
        try
        {
            var resp = await SendCommandAsync(McfProtocol.CMD_SERVER_STATUS);
            if (resp.IsError)
                return new FiscalServerConnectionResult
                {
                    Success = false,
                    ErrorMessage = "Erreur communication C2h"
                };

            var f = resp.Fields;
            if (f.Length < 4)
                return new FiscalServerConnectionResult
                {
                    Success = false,
                    ErrorMessage = "Réponse C2h incomplète"
                };

            // f[0] = EC (sent to DGI)
            // f[1] = DC (total stored in device — NOT the pending count)
            int sent = int.TryParse(f[0], out var ec) ? ec : 0;
            int inDevice = int.TryParse(f[1], out var dc) ? dc : 0;

            // The real pending backlog is the delta. Clamp to ≥ 0 to guard
            // against the (theoretical) case where firmware reports EC > DC
            // during a brief race window while an ACK is being applied.
            int pending = Math.Max(0, inDevice - sent);

#if DEBUG
            // Soft invariant: TC (from C1h) and DC (from C2h) should match on
            // a healthy device. A persistent divergence would indicate the
            // device's invoice journal and its DGI outbox have desynced — a
            // real anomaly worth surfacing in logs.
            Debug.WriteLine(
                $"[C2h] EC(sent)={sent} DC(inDevice)={inDevice} " +
                $"=> Pending={pending} STA={f[3]} " +
                $"LastErr='{(f.Length >= 5 ? f[4] : "")}'");
#endif

            DateTimeOffset? lastConn = null;
            if (f[2].Length == 14
                && DateTime.TryParseExact(f[2], "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
            {
                lastConn = ToLocalOffset(dt);
            }

            return new FiscalServerConnectionResult
            {
                Success = true,
                TransactionsSent = sent,
                TransactionsPending = pending,           // ← FIXED: DC - EC, not DC
                LastServerConnection = lastConn,
                ConnectionStatus = f[3],
                LastError = f.Length >= 5 ? f[4] : null
            };
        }
        catch (Exception ex)
        {
            return new FiscalServerConnectionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ══════════════════════════════════════
    // GetHealthReportAsync — synthetic verdict from C2h
    // ══════════════════════════════════════
    //
    // Why this exists: STA alone is misleading. An Incotex MCF spends most
    // of its life in STA=DIS (idle, no open TCP session) which is perfectly
    // normal. Real health is the combination of:
    //   • backlog size (TransactionsPending / DC)
    //   • freshness of last successful handshake
    //   • nature of LastError (transient vs hard transport failure)
    //
    // This method consolidates those signals into a single verdict suitable
    // for a dashboard traffic-light or an automated alert.
    // ══════════════════════════════════════

    public async Task<McfHealthReport> GetHealthReportAsync(McfHealthThresholds? thresholds = null)
    {
        var t = thresholds ?? McfHealthThresholds.Default;
        var report = new McfHealthReport();

        FiscalServerConnectionResult conn;
        try
        {
            conn = await GetServerConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            report.CommunicationFailed = true;
            report.Status = McfHealth.Unknown;
            report.Summary = $"Impossible de joindre le MCF : {ex.Message}";
            return report;
        }

        if (!conn.Success)
        {
            report.CommunicationFailed = true;
            report.Status = McfHealth.Unknown;
            report.Summary = conn.ErrorMessage ?? "C2h a échoué (raison inconnue).";
            return report;
        }

        // Populate raw fields
        report.RawConnectionStatus = conn.ConnectionStatus;
        report.PendingCount = conn.TransactionsPending;
        report.SentCount = conn.TransactionsSent;
        report.LastServerConnection = conn.LastServerConnection;
        report.LastError = conn.LastError;

        if (conn.LastServerConnection.HasValue
            && conn.LastServerConnection.Value != DateTimeOffset.MinValue)
        {
            report.TimeSinceLastSync = _time.LocalNow - conn.LastServerConnection.Value;
        }

        // Score against thresholds
        var status = McfHealth.Healthy;

        // ── Hard transport errors → Unhealthy ──
        if (!string.IsNullOrWhiteSpace(conn.LastError))
        {
            var u = conn.LastError.ToUpperInvariant();
            bool hardError =
                   u.Contains("APN")
                || u.Contains("SIM")
                || u.Contains("NO NETWORK")
                || u.Contains("PAS DE RESEAU")
                || u.Contains("PAS DE RÉSEAU");

            if (hardError)
            {
                status = McfHealth.Unhealthy;
                report.Warnings.Add($"Erreur transport matériel : {conn.LastError}");
            }
            else
            {
                // Transient / soft error → at least Degraded
                if (status < McfHealth.Degraded) status = McfHealth.Degraded;
                report.Warnings.Add($"Dernière erreur signalée : {conn.LastError}");
            }
        }

        // ── Pending backlog ──
        if (conn.TransactionsPending >= t.UnhealthyPendingCount)
        {
            status = McfHealth.Unhealthy;
            report.Warnings.Add(
                $"File d'attente saturée : {conn.TransactionsPending} transactions non transmises " +
                $"(seuil critique {t.UnhealthyPendingCount}).");
        }
        else if (conn.TransactionsPending >= t.DegradedPendingCount)
        {
            if (status < McfHealth.Degraded) status = McfHealth.Degraded;
            report.Warnings.Add(
                $"File d'attente en croissance : {conn.TransactionsPending} transactions en attente " +
                $"(seuil normal ≤{t.DegradedPendingCount - 1}).");
        }

        // ── Sync freshness ──
        if (report.TimeSinceLastSync.HasValue)
        {
            var age = report.TimeSinceLastSync.Value;

            if (age >= t.UnhealthySyncAge)
            {
                status = McfHealth.Unhealthy;
                report.Warnings.Add(
                    $"Aucune synchronisation DGI depuis {FormatAge(age)} " +
                    $"(seuil critique {FormatAge(t.UnhealthySyncAge)}).");
            }
            else if (age >= t.DegradedSyncAge)
            {
                if (status < McfHealth.Degraded) status = McfHealth.Degraded;
                report.Warnings.Add(
                    $"Dernière synchronisation DGI il y a {FormatAge(age)} " +
                    $"(seuil normal <{FormatAge(t.DegradedSyncAge)}).");
            }
        }
        else
        {
            // Never connected
            if (t.TreatNeverConnectedAsUnhealthy)
            {
                status = McfHealth.Unhealthy;
                report.Warnings.Add("Le MCF n'a jamais établi de connexion avec le serveur DGI.");
            }
            else
            {
                if (status < McfHealth.Degraded) status = McfHealth.Degraded;
                report.Warnings.Add("Aucune date de dernière synchronisation connue.");
            }
        }

        report.Status = status;
        report.Summary = status switch
        {
            McfHealth.Healthy =>
                $"OK — {conn.TransactionsSent} transactions envoyées, {conn.TransactionsPending} en attente.",
            McfHealth.Degraded =>
                $"Dégradé — {report.Warnings.Count} avertissement(s).",
            McfHealth.Unhealthy =>
                $"Critique — {report.Warnings.Count} problème(s) détecté(s).",
            _ => "État inconnu."
        };

        return report;
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1) return $"{age.TotalDays:F1} j";
        if (age.TotalHours >= 1) return $"{age.TotalHours:F1} h";
        if (age.TotalMinutes >= 1) return $"{age.TotalMinutes:F0} min";
        return $"{age.TotalSeconds:F0} s";
    }

    private static string BuildQrContent(string mid, string sig, string nif, string dt)
    {
        return $"RDCDEF01;{mid};{sig};{nif};{dt}";
    }

    // ══════════════════════════════════════════════════════════════
    // GetDetailedInfoAsync — C1h + C2h + 2Bh(×5)
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync()
    {
        var info = new FiscalDeviceDetailedInfo
        {
            DeviceTypeLabel = "MCF"
        };

        try
        {
            // ── 1. C1h ──
            var c1Resp = await SendCommandAsync(McfProtocol.CMD_STATUS);
            if (c1Resp.IsError)
            {
                info.Success = false;
                info.ErrorMessage = "Erreur communication C1h";
                info.ConnectionStatus = "DIS";
                return info;
            }

            var f = c1Resp.Fields;
            if (f.Length < 11)
            {
                info.Success = false;
                info.ErrorMessage = $"Réponse C1h incomplète ({f.Length} champs)";
                info.ConnectionStatus = "DIS";
                return info;
            }

            info.Success = true;
            info.NIM = f[0];
            info.NIF = f[1];
            info.ConnectionStatus = "CON";

            var ic = CultureInfo.InvariantCulture;

            if (f[2].Length == 14 && DateTime.TryParseExact(f[2], "yyyyMMddHHmmss",
                ic, DateTimeStyles.None, out var devDt))
            {
                info.DeviceDateTime = ToLocalOffset(devDt);
            }

            if (int.TryParse(f[3], out var tc)) info.TotalTransactions = tc;
            if (int.TryParse(f[4], out var fvc)) info.SalesInvoiceCount = fvc;
            if (int.TryParse(f[5], out var frc)) info.CreditNoteCount = frc;

            if (f.Length > 6 && f[6].Length == 14 && DateTime.TryParseExact(f[6], "yyyyMMddHHmmss",
                ic, DateTimeStyles.None, out var lastInvDt))
            {
                info.LastInvoiceDate = ToLocalOffset(lastInvDt);
            }

            if (f.Length > 7) info.LastInvoiceType = f[7];
            if (f.Length > 8) info.LastInvoiceCodeDEF = f[8];
            if (f.Length > 9) info.LastInvoiceNumber = f[9];
            if (f.Length > 10 && decimal.TryParse(f[10], NumberStyles.Any, ic, out var lastAmt))
            {
                info.LastInvoiceAmount = lastAmt;
            }

            if (f.Length >= 27)
            {
                for (int i = 0; i < 16 && (11 + i) < f.Length; i++)
                {
                    if (decimal.TryParse(f[11 + i], NumberStyles.Any, ic, out var rate))
                        info.TaxRates[i] = rate;
                }
            }

            if (f.Length >= 30)
            {
                try
                {
                    string currCode = f[27]?.Trim() ?? "";
                    string currRateStr = f[28]?.Trim() ?? "";
                    string currDateStr = f[29]?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(currCode) &&
                        decimal.TryParse(currRateStr, NumberStyles.Any, ic, out var currRate) &&
                        currRate > 0)
                    {
                        DateTimeOffset currDate = _time.LocalNow;
                        if (!string.IsNullOrEmpty(currDateStr) && currDateStr.Length == 8 &&
                            DateTime.TryParseExact(currDateStr, "yyyyMMdd",
                                ic, DateTimeStyles.None, out var parsedDate))
                        {
                            currDate = ToLocalOffset(parsedDate);
                        }

                        info.CurrencyRates = new List<CurrencyRateInfo>
                        {
                            new CurrencyRateInfo
                            {
                                Code = currCode,
                                Description = currCode == "USD" ? "United States Dollar" :
                                              currCode == "EUR" ? "Euro" :
                                              currCode == "CNY" ? "Chinese Yuan" : currCode,
                                Rate = currRate,
                                Date = currDate
                            }
                        };

                        Debug.WriteLine($"[MCF] Currency rate found: 1 {currCode} = {currRate:N2} CDF ({currDate:dd/MM/yyyy})");
                    }
                    else
                    {
                        Debug.WriteLine($"[MCF] No currency rate in C1h (field 27='{f[27]}', 28='{f[28]}', 29='{f[29]}')");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MCF] Currency rate parsing error: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[MCF] C1h response has {f.Length} fields, need ≥30 for currency rate");

                try
                {
                    var rateInfo = await GetCurrencyRateFromDeviceAsync();
                    if (rateInfo != null)
                        info.CurrencyRates = new List<CurrencyRateInfo> { rateInfo };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MCF] Fallback currency read failed: {ex.Message}");
                }
            }

            // ── 2. C2h ──
            try
            {
                var c2Resp = await SendCommandAsync(McfProtocol.CMD_SERVER_STATUS);
                if (!c2Resp.IsError && c2Resp.Fields.Length >= 4)
                {
                    var c2f = c2Resp.Fields;
                    if (int.TryParse(c2f[0], out var ec)) info.TransactionsSent = ec;
                    if (int.TryParse(c2f[1], out var dc)) info.TransactionsInDevice = dc;

                    if (c2f[2].Length == 14 && DateTime.TryParseExact(c2f[2], "yyyyMMddHHmmss",
                        ic, DateTimeStyles.None, out var lastConn))
                    {
                        info.LastServerConnection = ToLocalOffset(lastConn);
                    }

                    info.ConnectionStatus = c2f[3];

                    if (c2f.Length >= 5 && !string.IsNullOrWhiteSpace(c2f[4]))
                        info.LastError = c2f[4];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MCF] C2h call failed: {ex.Message}");
            }

            // ── 3. 2Bh ──
            try
            {
                info.TaxpayerName = await GetTaxpayerFieldAsync("I0");
                info.TaxpayerAddress = await GetTaxpayerFieldAsync("I1");
                info.TaxpayerCity = await GetTaxpayerFieldAsync("I2");
                info.TaxpayerPhone = await GetTaxpayerFieldAsync("I3");
                info.TaxpayerEmail = await GetTaxpayerFieldAsync("I4");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MCF] 2Bh taxpayer info failed: {ex.Message}");
            }

            return info;
        }
        catch (Exception ex)
        {
            return new FiscalDeviceDetailedInfo
            {
                Success = false,
                DeviceTypeLabel = "MCF",
                ConnectionStatus = "DIS",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> GetTaxpayerFieldAsync(string fieldId)
    {
        var resp = await SendCommandAsync(McfProtocol.CMD_TAXPAYER_INFO, fieldId);
        if (resp.IsError) return "";
        var idx = resp.Data.IndexOf(',');
        return idx >= 0 ? resp.Data[(idx + 1)..].Trim() : resp.Data.Trim();
    }

    private async Task<CurrencyRateInfo?> GetCurrencyRateFromDeviceAsync()
    {
        var ic = CultureInfo.InvariantCulture;

        string code = await GetTaxpayerFieldAsync("D0");
        string rateStr = await GetTaxpayerFieldAsync("D1");
        string dateStr = await GetTaxpayerFieldAsync("D2");

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(rateStr))
        {
            Debug.WriteLine("[MCF] 2Bh D0/D1/D2 returned empty — no currency rate available");
            return null;
        }

        if (!decimal.TryParse(rateStr, NumberStyles.Any, ic, out var rate) || rate <= 0)
        {
            Debug.WriteLine($"[MCF] 2Bh D1 rate invalid: '{rateStr}'");
            return null;
        }

        DateTimeOffset date = _time.LocalNow;
        if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 8 &&
            DateTime.TryParseExact(dateStr.Substring(0, 8), "yyyyMMdd",
                ic, DateTimeStyles.None, out var parsedDate))
        {
            date = ToLocalOffset(parsedDate);
        }

        Debug.WriteLine($"[MCF] Currency from 2Bh: 1 {code} = {rate:N2} CDF ({date:dd/MM/yyyy})");

        return new CurrencyRateInfo
        {
            Code = code.Trim(),
            Description = code.Trim() == "USD" ? "United States Dollar" :
                          code.Trim() == "EUR" ? "Euro" : code.Trim(),
            Rate = rate,
            Date = date
        };
    }

    // ══════════════════════════════════════════════════════════════
    // DISPOSE
    // ══════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var port = _port;
        _port = null;

        if (port != null)
        {
            try
            {
                if (port.IsOpen)
                {
                    try { port.DiscardInBuffer(); } catch { /* ignore */ }
                    try { port.DiscardOutBuffer(); } catch { /* ignore */ }
                    port.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McfSerialClient] Close error: {ex.Message}");
            }

            try { port.Dispose(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McfSerialClient] Dispose error: {ex.Message}");
            }
        }

        try { _lock.Dispose(); } catch { /* ignore */ }

        GC.SuppressFinalize(this);
    }
}