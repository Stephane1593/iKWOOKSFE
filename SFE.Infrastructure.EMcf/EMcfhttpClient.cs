using System.Diagnostics;
using System.Globalization;
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
/// </summary>
public class EMcfHttpClient : IFiscalDeviceService
{
    private readonly HttpClient _http;
    private readonly string _invoiceUrl;
    private readonly string _infoUrl;
    private readonly string _nif;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EMcfHttpClient(string baseUrl, string token, string nif)
    {
        _nif = nif;

        var trimmed = baseUrl.TrimEnd('/');
        _invoiceUrl = $"{trimmed}/api/invoice";
        _infoUrl = $"{trimmed}/api/info";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ══════════════════════════════════════
    // GET /api/invoice/  → Status
    // ══════════════════════════════════════

    public async Task<FiscalStatusResult> GetStatusAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<StatusResponseDto>(
                $"{_invoiceUrl}/", JsonOpts);

            if (resp == null)
                return new FiscalStatusResult { Success = false, ErrorMessage = "Réponse vide" };

            var pendingList = new List<PendingInvoiceInfo>();
            if (resp.PendingRequestsList != null)
            {
                foreach (var p in resp.PendingRequestsList)
                {
                    pendingList.Add(new PendingInvoiceInfo
                    {
                        Uid = p.Uid,
                        Date = p.Date
                    });
                }
            }

            return new FiscalStatusResult
            {
                Success = resp.Status,
                NIM = resp.Nim,
                NIF = resp.Nif,
                ErrorMessage = resp.Status ? null : "API non opérationnelle",
                PendingCount = resp.PendingRequestsCount,
                PendingInvoices = pendingList
            };
        }
        catch (HttpRequestException ex)
        {
            return new FiscalStatusResult
            {
                Success = false,
                ErrorMessage = $"Connexion échouée: {ex.Message}"
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

            var postResp = await _http.PostAsJsonAsync(
                $"{_invoiceUrl}/", dto, JsonOpts);
            var rawJson = await postResp.Content.ReadAsStringAsync();
            Debug.WriteLine($"[SubmitInvoice] Status: {(int)postResp.StatusCode}, Body: {rawJson}");

            if (!postResp.IsSuccessStatusCode)
            {
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = ((int)postResp.StatusCode).ToString(),
                    ErrorMessage = $"HTTP {postResp.StatusCode}: {rawJson}"
                };
            }

            var invoiceResp = JsonSerializer.Deserialize<InvoiceResponseDataDto>(rawJson, JsonOpts);

            if (invoiceResp == null)
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorMessage = "Réponse vide du serveur"
                };

            if (!string.IsNullOrEmpty(invoiceResp.ErrorCode))
            {
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = invoiceResp.ErrorCode,
                    ErrorMessage = invoiceResp.ErrorDesc
                };
            }

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
            return new FiscalSubmitResult
            {
                Success = false,
                ErrorMessage = $"Exception e-MCF: {ex.Message}"
            };
        }
    }

    // ══════════════════════════════════════
    // PUT /api/invoice/{uid}/CONFIRM
    // ══════════════════════════════════════

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(
        string uid, decimal totalTTC, decimal totalTVA)
    {
        try
        {
            var body = new FinalizeInvoiceRequestDataDto
            {
                Total = totalTTC,
                Vtotal = totalTVA
            };

            var content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json");

            var resp = await _http.PutAsync(
                $"{_invoiceUrl}/{uid}/CONFIRM", content);

            var rawJson = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine($"[FinalizeInvoice] Status: {(int)resp.StatusCode}, Body: {rawJson}");

            if (!resp.IsSuccessStatusCode)
            {
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = ((int)resp.StatusCode).ToString(),
                    ErrorMessage = $"Confirmation échouée: {rawJson}"
                };
            }

            var finalResp = JsonSerializer.Deserialize<FinalizeInvoiceResponseDataDto>(rawJson, JsonOpts);

            if (finalResp == null)
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorMessage = "Réponse confirmation vide"
                };

            if (!string.IsNullOrEmpty(finalResp.ErrorCode))
            {
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = finalResp.ErrorCode,
                    ErrorMessage = finalResp.ErrorDesc
                };
            }

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
            return new FiscalFinalizeResult
            {
                Success = false,
                ErrorMessage = $"Exception confirmation: {ex.Message}"
            };
        }
    }

    // ══════════════════════════════════════
    // PUT /api/invoice/{uid}/CANCEL
    // ══════════════════════════════════════

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        try
        {
            var resp = await _http.PutAsync(
                $"{_invoiceUrl}/{uid}/CANCEL", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ══════════════════════════════════════════════════════════════
    // GetDetailedInfoAsync — Comprehensive device info
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync()
    {
        var info = new FiscalDeviceDetailedInfo
        {
            DeviceTypeLabel = "e-MCF"
        };

        try
        {
            // ── 1. Invoice API status ──
            var statusResp = await _http.GetFromJsonAsync<StatusResponseDto>(
                $"{_invoiceUrl}/", JsonOpts);

            if (statusResp != null)
            {
                info.Success = statusResp.Status;
                info.NIM = statusResp.Nim;
                info.NIF = statusResp.Nif;
                info.ServerDateTime = statusResp.ServerDateTime;
                info.DeviceDateTime = statusResp.ServerDateTime;
                info.TokenValidUntil = statusResp.TokenValid;
                info.PendingRequestsCount = statusResp.PendingRequestsCount;
                info.ConnectionStatus = statusResp.Status ? "CON" : "DIS";
                info.LastServerConnection = statusResp.Status ? DateTime.Now : null;
            }
            else
            {
                info.Success = false;
                info.ErrorMessage = "Réponse statut vide";
                info.ConnectionStatus = "DIS";
                return info;
            }

            // ── 2. Info API — taxpayer & shop details ──
            try
            {
                var infoResp = await _http.GetFromJsonAsync<InfoResponseDto>(
                    $"{_infoUrl}/status", JsonOpts);

                if (infoResp != null)
                {
                    info.ApiVersion = infoResp.Version;

                    if (infoResp.EmcfList?.Count > 0)
                    {
                        var activeEmcf = infoResp.EmcfList
                            .FirstOrDefault(e => e.Nim == info.NIM)
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
                Debug.WriteLine($"[e-MCF] Info/status call failed: {ex.Message}");
            }

            // ── 3. Tax groups ──
            try
            {
                var taxResp = await _http.GetFromJsonAsync<TaxGroupsDto>(
                    $"{_infoUrl}/taxGroups", JsonOpts);

                if (taxResp != null)
                    info.TaxRates = taxResp.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[e-MCF] TaxGroups call failed: {ex.Message}");
            }

            // ── 4. Currency rates ──
            try
            {
                var ratesResp = await _http.GetFromJsonAsync<List<CurrencyRateDto>>(
                    $"{_infoUrl}/currencyRates", JsonOpts);

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
                Debug.WriteLine($"[e-MCF] CurrencyRates call failed: {ex.Message}");
            }

            return info;
        }
        catch (HttpRequestException ex)
        {
            return new FiscalDeviceDetailedInfo
            {
                Success = false,
                DeviceTypeLabel = "e-MCF",
                ConnectionStatus = "DIS",
                ErrorMessage = $"Connexion échouée: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new FiscalDeviceDetailedInfo
            {
                Success = false,
                DeviceTypeLabel = "e-MCF",
                ErrorMessage = $"Erreur: {ex.Message}"
            };
        }
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
                ErrorMessage = ex.Message
            };
        }
    }

    // ══════════════════════════════════════════════════════════════
    // MAPPING  FiscalInvoiceRequest → InvoiceRequestDataDto
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
            TaxSpecificValue = string.IsNullOrWhiteSpace(i.TaxSpecificValue)
                ? "0%" : i.TaxSpecificValue,
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
                CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode)
                    ? curCode : p.CurrencyCode,
                CurrencyRate = p.CurrencyRate ?? dto.CurRate
            }).ToList();
        }
        else
        {
            dto.Payment = new List<PaymentDto>();
        }

        return dto;
    }
}