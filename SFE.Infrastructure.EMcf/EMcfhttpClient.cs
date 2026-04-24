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
///
/// Format JSON : TOUS les champs toujours présents.
///   Texte vide = "", Nombre vide = 0, price = string.
/// </summary>
public class EMcfHttpClient : IFiscalDeviceService
{
    private readonly HttpClient _http;
    private readonly string _invoiceUrl;
    private readonly string _nif;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EMcfHttpClient(string baseUrl, string token, string nif)
    {
        _nif = nif;

        _invoiceUrl = $"{baseUrl.TrimEnd('/')}/api/invoice";

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
                PendingCount = resp.PendingRequestsCount,   // ← was missing
                PendingInvoices = pendingList                 // ← was missing
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
            Debug.WriteLine($"[FinalizeInvoice] Status: {(int)postResp.StatusCode}, Body: {rawJson}");

            if (!postResp.IsSuccessStatusCode)
            {
                var errorBody = await postResp.Content.ReadAsStringAsync();
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorCode = ((int)postResp.StatusCode).ToString(),
                    ErrorMessage = $"HTTP {postResp.StatusCode}: {errorBody}"
                };
            }

            var invoiceResp = await postResp.Content
                .ReadFromJsonAsync<InvoiceResponseDataDto>(JsonOpts);

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
    // POST /api/invoice/{uid}/CONFIRM
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

            // 👇 Log / inspect the raw response for testing
            Debug.WriteLine($"[FinalizeInvoice] Status: {(int)resp.StatusCode}, Body: {rawJson}");

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                return new FiscalFinalizeResult
                {
                    Success = false,
                    ErrorCode = ((int)resp.StatusCode).ToString(),
                    ErrorMessage = $"Confirmation échouée: {errorBody}"
                };
            }

            var finalResp = await resp.Content
                .ReadFromJsonAsync<FinalizeInvoiceResponseDataDto>(JsonOpts);

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
    // POST /api/invoice/{uid}/CANCEL
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
    // MAPPING  FiscalInvoiceRequest → InvoiceRequestDataDto
    //
    // Règle : aucun champ null dans le DTO.
    //   - Texte manquant  → ""
    //   - Nombre manquant → 0
    //   - price           → string arrondi 2 décimales
    //   - originalPrice   → decimal pleine précision
    //   - mode            → lowercase
    //   - curCode         → "CDF" si pas de devise étrangère
    // ══════════════════════════════════════════════════════════════

    private InvoiceRequestDataDto MapToDto(FiscalInvoiceRequest request)
    {
        var dto = new InvoiceRequestDataDto
        {
            Nif = request.NIF ?? "",
            Rn = request.InvoiceNumber ?? "",
            Mode = (request.PriceMode ?? "TTC").ToLowerInvariant(),   // "ht" ou "ttc"
            Isf = request.ISF ?? "",
            Type = request.InvoiceType ?? "FV",
            Operator = new OperatorDto
            {
                Id = request.OperatorId ?? "",
                Name = request.OperatorName ?? ""
            }
        };

        // ── Devise (toujours présente) ──
        string curCode = request.CurrencyCode ?? "CDF";
        if (string.IsNullOrWhiteSpace(curCode)) curCode = "CDF";
        dto.CurCode = curCode;
        dto.CurDate = (request.CurrencyDate ?? DateTime.Now)
            .ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        dto.CurRate = request.CurrencyRate ?? 0m;

        // ── Commentaires (toujours présents, "" si vide) ──
        dto.Cmta = request.CommentA ?? "";
        dto.Cmtb = request.CommentB ?? "";
        dto.Cmtc = request.CommentC ?? "";
        dto.Cmtd = request.CommentD ?? "";
        dto.Cmte = request.CommentE ?? "";
        dto.Cmtf = request.CommentF ?? "";
        dto.Cmtg = request.CommentG ?? "";
        dto.Cmth = request.CommentH ?? "";

        // ── Référence (toujours présente, "" si pas FA/EA) ──
        dto.Reference = request.Reference ?? "";
        dto.ReferenceType = request.ReferenceType ?? "";
        dto.ReferenceDesc = request.ReferenceDesc ?? "";

        // ── Client (toujours présent) ──
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
            // Anonyme PP par défaut
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

        // ── Articles ──
        dto.Items = request.Items.Select(i => new ItemDto
        {
            Code = i.Code ?? "",
            Type = i.Type ?? "BIE",
            Name = i.Name ?? "",

            // ⚠ price = STRING arrondi à 2 décimales
            Price = Math.Round(i.Price, 2)
                .ToString(CultureInfo.InvariantCulture),

            Quantity = i.Quantity,
            TaxGroup = i.TaxGroup ?? "A",

            // ⚠ Toujours présents — défaut "0%" / 0
            TaxSpecificValue = string.IsNullOrWhiteSpace(i.TaxSpecificValue)
                ? "0%" : i.TaxSpecificValue,
            TaxSpecificAmount = i.TaxSpecificAmount ?? 0m,

            // ⚠ Toujours présents — pleine précision pour originalPrice
            OriginalPrice = i.OriginalPrice ?? i.Price,
            PriceModification = i.PriceModification ?? ""
        }).ToList();

        // ── Paiements ──
        if (request.Payments.Count > 0)
        {
            dto.Payment = request.Payments.Select(p => new PaymentDto
            {
                Name = p.Name ?? "ESPECES",
                Amount = p.Amount,
                CurrencyCode = string.IsNullOrWhiteSpace(p.CurrencyCode)
                    ? (curCode) : p.CurrencyCode,      // Même devise que la facture
                CurrencyRate = p.CurrencyRate ?? dto.CurRate
            }).ToList();
        }
        else
        {
            // Liste vide (pas null)
            dto.Payment = new List<PaymentDto>();
        }

        return dto;
    }
}