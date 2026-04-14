using System.Globalization;
using System.IO.Ports;
using System.Text;
using SFE.Application.Interfaces;

namespace SFE.Infrastructure.Mcf;

/// <summary>
/// Client MCF physique — communication port série RS232/USB.
/// Implémente le flux spec MCF: C3→C0→31h(×N)→36h→33h→35h puis 38h.
/// Submit = tout sauf 38h, Finalize = 38h.
/// </summary>
public class McfSerialClient : IFiscalDeviceService, IDisposable
{
    private SerialPort? _port;
    private byte _seq = 0x20;
    private readonly string _comPort;
    private readonly int _baudRate;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _port?.IsOpen == true;

    public McfSerialClient(string comPort, int baudRate = 115200)
    {
        _comPort = comPort;
        _baudRate = baudRate;
    }

    public void Connect()
    {
        _port?.Dispose();
        _port = new SerialPort(_comPort, _baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 5000,
            WriteTimeout = 2000
        };
        _port.Open();
    }

    private byte NextSeq()
    {
        _seq++;
        if (_seq > 0xFF) _seq = 0x20;
        return _seq;
    }

    // ══════════════════════════════════════
    // ENVOI / RÉCEPTION (inchangé)
    // ══════════════════════════════════════

    private async Task<McfResponse> SendCommandAsync(byte cmd, string? data = null)
    {
        if (_port == null || !_port.IsOpen)
            throw new InvalidOperationException("MCF non connecté");

        await _lock.WaitAsync();
        try
        {
            byte seq = NextSeq();
            byte[] frame = McfProtocol.BuildCommand(seq, cmd, data);

            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);

            return await ReadResponseAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<McfResponse> ReadResponseAsync()
    {
        var buffer = new byte[512];
        var received = new List<byte>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < 5000)
        {
            if (_port!.BytesToRead > 0)
            {
                int count = _port.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < count; i++)
                    received.Add(buffer[i]);

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

        throw new TimeoutException("Pas de réponse du MCF dans le délai imparti");
    }

    // ══════════════════════════════════════
    // IFiscalDeviceService — GetStatusAsync
    // C1h → FiscalStatusResult
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
                    Date = DateTime.Now
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
    // IFiscalDeviceService — SubmitInvoiceAsync
    // Flux: C3→C0→31h(×N)→36h→33h→35h
    // ══════════════════════════════════════

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        try
        {
            // 1. C3h — Info client
            await SendClientInfoAsync(request);

            // 2. C0h — Ouvrir la facture
            var openResp = await OpenInvoiceAsync(request);
            if (openResp.StartsWith("E:"))
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = "C0",
                    ErrorMessage = openResp[2..]
                };

            // 3. 31h — Enregistrer chaque article
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

            // 4. 36h — Commentaires
            await SendCommentsAsync(request);

            // 5. 33h — Sous-total (vérification)
            var subtotalResp = await SendCommandAsync(McfProtocol.CMD_SUBTOTAL);

            decimal serverTTC = 0, serverTVA = 0, serverTS = 0, serverUSD = 0;
            var groupAmounts = new Dictionary<string, decimal>();
            var groupTVA = new Dictionary<string, decimal>();

            if (!subtotalResp.IsError && subtotalResp.Fields.Length >= 35)
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                var groups = new[]
                {
                  "A","B","C","D","E","F","G","H",
                  "I","J","K","L","M","N","O","P"
                };

                // ── MV (index 0) — Montant total enregistré ──
                decimal.TryParse(subtotalResp.Fields[0], NumberStyles.Any, ic, out serverTTC);

                // ── MVA…MVP (index 1–16) — Montant TTC par groupe ──
                for (int i = 0; i < 16; i++)
                {
                    if (decimal.TryParse(subtotalResp.Fields[1 + i],
                                         NumberStyles.Any, ic, out var amt) && amt != 0)
                    {
                        groupAmounts[groups[i]] = amt;
                    }
                }

                // ── MTA…MTP (index 17–32) — TVA par groupe ──
                for (int i = 0; i < 16; i++)
                {
                    if (decimal.TryParse(subtotalResp.Fields[17 + i],
                                         NumberStyles.Any, ic, out var tva))
                    {
                        if (tva != 0)
                            groupTVA[groups[i]] = tva;

                        serverTVA += tva;   // ← accumulate total TVA
                    }
                }

                // ── MTS (index 33) — Taxe spécifique totale ──
                decimal.TryParse(subtotalResp.Fields[33], NumberStyles.Any, ic, out serverTS);

                // ── MCUR (index 34) — Équivalent USD ──
                decimal.TryParse(subtotalResp.Fields[34], NumberStyles.Any, ic, out serverUSD);
            }

            // 6. 35h — Paiements
            foreach (var payment in request.Payments)
            {
                await RegisterPaymentAsync(payment);
            }

            // MCF n'a pas de UID — la facture est "ouverte" sur le dispositif
            return new FiscalSubmitResult
            {
                Success = true,
                Uid = "MCF",
                TotalTTC = serverTTC,
                TotalTVA = serverTVA,     // now correct: sum of MTA…MTP
                TotalTS = serverTS,
                TotalUSD = serverUSD,
                GroupAmounts = groupAmounts,   // for per-group verification
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
    // IFiscalDeviceService — FinalizeInvoiceAsync
    // 38h → FiscalFinalizeResult
    // ══════════════════════════════════════

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(
        string uid, decimal totalTTC, decimal totalTVA)
    {
        try
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string data = $"{totalTTC.ToString("F2", ic)},{totalTVA.ToString("F2", ic)}";

            var resp = await SendCommandAsync(McfProtocol.CMD_FINALIZE, data);

            if (resp.IsError)
                return new FiscalFinalizeResult { Success = false, ErrorMessage = "Erreur communication 38h" };

            if (resp.Data.StartsWith("E:"))
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = "38",
                    ErrorMessage = resp.Data[2..]
                };

            if (resp.Data.StartsWith("P:"))
            {
                // MCF a besoin de plus de temps
                await Task.Delay(500);
                return await FinalizeInvoiceAsync(uid, totalTTC, totalTVA);
            }

            // Succès: "R:{FC},{TC},{FT},{DT},{MID},{NIF},{FN},{SIG}"
            string rawData;
            if (resp.Data.StartsWith("R :"))
                rawData = resp.Data[3..];
            else if (resp.Data.StartsWith("R:"))
                rawData = resp.Data[2..];
            else
                rawData = resp.Data;

            var f = rawData.Split(',');

            if (f.Length < 6)
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorMessage = "Réponse 38h incomplète"
                };

            return new FiscalFinalizeResult
            {
                Success = true,
                CodeDEFDGI = f[6],
                NIM = f[4],
                Counters = $"{f[0]}/{f[1]} {f[2]}",
                DateTime = f[3],
                QRCode = BuildQrContent(f[4], f[6], f[5], f[3])
            };
        }
        catch (Exception ex)
        {
            return new FiscalFinalizeResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ══════════════════════════════════════
    // IFiscalDeviceService — CancelPendingInvoiceAsync
    // ══════════════════════════════════════

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        try
        {
            var resp = await SendCommandAsync(McfProtocol.CMD_FINALIZE, "C");
            return !resp.IsError;
        }
        catch { return false; }
    }

    // ══════════════════════════════════════
    // PRIVATE — Commandes MCF depuis FiscalInvoiceRequest
    // ══════════════════════════════════════

    // ── C3h — Client ──
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
            "", // email (inclus dans contact)
            ""  // RCCM
        };

        await SendCommandAsync(McfProtocol.CMD_CLIENT_INFO, string.Join(",", parts));
    }

    // ── C0h — Ouvrir facture ──
    private async Task<string> OpenInvoiceAsync(FiscalInvoiceRequest request)
    {
        // {OPID},{OPN},{NIF},{VT|RT,RR,RN},{PMODE},{ISF},{FN}[,{CRT},{CDT}]
        var parts = new List<string>
        {
            McfProtocol.EscapeData(request.OperatorId),
            McfProtocol.EscapeData(request.OperatorName),
            request.NIF
        };

        bool isCreditNote = request.InvoiceType is "FA" or "EA";
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
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            parts.Add(request.CurrencyRate.Value.ToString("F2", ic));
            parts.Add(request.CurrencyDate?.ToString("yyyyMMdd") ?? "");
        }

        var resp = await SendCommandAsync(McfProtocol.CMD_NEW_INVOICE, string.Join(",", parts));
        return resp.IsError ? "E:Communication" : resp.Data;
    }

    // ── 31h — Article ──
    private async Task<string> RegisterItemAsync(FiscalItemInfo item, string priceMode)
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        // Nom
        sb.Append(McfProtocol.EscapeData(item.Name));

        // Code (description sur LF)
        if (!string.IsNullOrEmpty(item.Code))
        {
            sb.Append((char)0x0A); // LF
            sb.Append(McfProtocol.EscapeData(item.Code));
        }

        // TAB + ITYPE,TAX TR% MON
        sb.Append((char)0x09);
        sb.Append(item.Type);
        sb.Append(',');
        sb.Append(item.TaxGroup);
        sb.Append(item.TaxRate.ToString("F2", ic));
        sb.Append('%');

        decimal mon = item.Price * item.Quantity;
        sb.Append(mon.ToString("F2", ic));

        // TAB + PR [*QT]
        sb.Append((char)0x09);
        sb.Append(item.Price.ToString("F2", ic));

        if (item.Quantity != 1)
        {
            sb.Append('*');
            sb.Append(item.Quantity.ToString("G", ic));
        }

        // Taxe spécifique
        if (!string.IsNullOrEmpty(item.TaxSpecificValue))
        {
            sb.Append(";T");
            sb.Append(item.TaxSpecificValue);
            sb.Append(',');
            sb.Append((item.TaxSpecificAmount ?? 0).ToString("F2", ic));
        }

        // Modification de prix
        if (item.OriginalPrice.HasValue)
        {
            sb.Append((char)0x09);
            sb.Append(item.OriginalPrice.Value.ToString("F2", ic));
            sb.Append(',');
            sb.Append(McfProtocol.EscapeData(item.PriceModification ?? ""));
        }

        var resp = await SendCommandAsync(McfProtocol.CMD_REGISTER_ITEM, sb.ToString());
        return resp.IsError ? "E:Communication" : resp.Data;
    }

    // ── 36h — Commentaires ──
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

    // ── 35h — Paiement ──
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

        var ic = System.Globalization.CultureInfo.InvariantCulture;
        string data = $"{pa}{payment.Amount.ToString("F2", ic)}";
        await SendCommandAsync(McfProtocol.CMD_PAYMENT, data);
    }

    // ── Helpers ──
    private static string BuildQrContent(string mid, string sig, string nif, string dt)
    {
        return $"RDCDEF01;{mid};{sig};{nif};{dt}";
    }

    public void Dispose()
    {
        _port?.Close();
        _port?.Dispose();
        _lock.Dispose();
    }
}