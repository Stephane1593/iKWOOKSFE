using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Helpers;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace SFE.WPF.ViewModels;

public partial class PointOfSaleManagementViewModel : BaseViewModel
{
    private readonly PointOfSaleService _posService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockService _stockService;

    public PointOfSaleManagementViewModel(
        PointOfSaleService posService, IUnitOfWork unitOfWork, StockService stockService)
    {
        _posService = posService;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        PageTitle = "Points de vente";

        _ = LoadAsync();   // ← ADD THIS
    }

    // ══════════════════════════════════════════════
    //  PROPRIÉTÉS
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private int _companyId;

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _allPos = new();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formTitle = "";

    // ── Champs formulaire : Général ──

    private int _editId;

    [ObservableProperty]
    private string _editCode = "";

    [ObservableProperty]
    private string _editName = "";

    [ObservableProperty]
    private string _editAddress = "";

    [ObservableProperty]
    private string _editCity = "";

    [ObservableProperty]
    private string _editPhone = "";

    [ObservableProperty]
    private bool _editManagesStock = true;

    [ObservableProperty]
    private bool _editAllowNegativeStock;

    [ObservableProperty]
    private string _editEmcfUrl = "";

    [ObservableProperty]
    private string _editEmcfToken = "";

    [ObservableProperty]
    private string _editEmcfNim = "";

    // ── Radio fiscal ──

    private bool _editIsEmcf = true;
    public bool EditIsEmcf
    {
        get => _editIsEmcf;
        set
        {
            if (SetProperty(ref _editIsEmcf, value))
                OnPropertyChanged(nameof(EditIsEmcf));
        }
    }

    // ══════════════════════════════════════════════
    //  🖨 CHAMPS FORMULAIRE : IMPRIMANTE
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private string _editPrinterName = "";

    [ObservableProperty]
    private int _editPaperWidth = 80;

    [ObservableProperty]
    private bool _editAutoPrint = true;

    [ObservableProperty]
    private int _editPrintCopies = 1;

    [ObservableProperty]
    private bool _editEnableCustomerDisplay;

    [ObservableProperty]
    private bool _editEnableCashDrawer;

    [ObservableProperty]
    private int _editCashDrawerPin;

    [ObservableProperty]
    private int _editCodePage = 858;

    [ObservableProperty]
    private bool _editPrintLogo;

    [ObservableProperty]
    private string _editFooterText = "Merci pour votre achat !";

    // ── Sources ComboBox imprimante ──

    public int[] PaperWidths { get; } = { 80, 58 };
    public int[] PrintCopiesOptions { get; } = { 1, 2, 3 };
    public int[] CashDrawerPins { get; } = { 0, 1 };
    public int[] CodePages { get; } = { 858, 850, 437, 1252 };

    public ObservableCollection<string> DetectedPrinters { get; } = new();

    // ══════════════════════════════════════════════
    //  COMMANDES — CRUD POS
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadAsync()
    {

        if (CompanyId == 0)
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null) CompanyId = company.Id;
        }

        if (!await EnsureCompanyLoadedAsync()) return;

        var posList = await _posService.GetAllAsync(CompanyId);
        AllPos = new ObservableCollection<PointOfSale>(posList);
    }

    [RelayCommand]
    private async Task StartNewPosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;

        _editId = 0;
        EditCode = await _posService.GenerateNextCodeAsync(CompanyId);
        EditName = "";
        EditAddress = "";
        EditCity = "";
        EditPhone = "";
        EditManagesStock = true;
        EditAllowNegativeStock = false;
        EditIsEmcf = true;
        EditEmcfUrl = "";
        EditEmcfToken = "";
        EditEmcfNim = "";

        // 🖨 Valeurs par défaut imprimante
        EditPrinterName = "";
        EditPaperWidth = 80;
        EditAutoPrint = true;
        EditPrintCopies = 1;
        EditEnableCustomerDisplay = false;
        EditEnableCashDrawer = false;
        EditCashDrawerPin = 0;
        EditCodePage = 858;
        EditPrintLogo = false;
        EditFooterText = "Merci pour votre achat !";

        RefreshPrinterList();
        FormTitle = "Nouveau point de vente";
        IsEditing = true;
    }

    [RelayCommand]
    private void EditPos(PointOfSale pos)
    {
        _editId = pos.Id;
        EditCode = pos.Code;
        EditName = pos.Name;
        EditAddress = pos.Address;
        EditCity = pos.City;
        EditPhone = pos.Phone;
        EditManagesStock = pos.ManagesStock;
        EditAllowNegativeStock = pos.AllowNegativeStock;
        EditIsEmcf = pos.DeviceType == DeviceType.EMcf;
        EditEmcfUrl = pos.EmcfApiUrl ?? "";
        EditEmcfToken = pos.EmcfToken ?? "";
        EditEmcfNim = pos.EmcfNIM ?? "";

        // 🖨 Charger config imprimante existante
        EditPrinterName = pos.ThermalPrinterName ?? "";
        EditPaperWidth = pos.PaperWidthMm > 0 ? pos.PaperWidthMm : 80;
        EditAutoPrint = pos.AutoPrintReceipt;
        EditPrintCopies = pos.PrintCopies > 0 ? pos.PrintCopies : 1;
        EditEnableCustomerDisplay = pos.EnableCustomerDisplay;
        EditEnableCashDrawer = pos.EnableCashDrawer;
        EditCashDrawerPin = pos.CashDrawerPin;
        EditCodePage = pos.PrinterCodePage > 0 ? pos.PrinterCodePage : 858;
        EditPrintLogo = pos.PrintLogo;
        EditFooterText = pos.ReceiptFooterText ?? "Merci pour votre achat !";

        RefreshPrinterList();
        FormTitle = $"Modifier {pos.Code}";
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SavePosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;

        var deviceType = EditIsEmcf ? DeviceType.EMcf : DeviceType.Mcf;
        PosSaveResult result;

        if (_editId == 0)
        {
            var pos = new PointOfSale
            {
                CompanyId = CompanyId,
                Code = EditCode,
                Name = EditName,
                Address = EditAddress,
                City = EditCity,
                Phone = EditPhone,
                ManagesStock = EditManagesStock,
                AllowNegativeStock = EditAllowNegativeStock,
                DeviceType = deviceType,
                EmcfApiUrl = NullIfEmpty(EditEmcfUrl),
                EmcfToken = NullIfEmpty(EditEmcfToken),
                EmcfNIM = NullIfEmpty(EditEmcfNim),

                // 🖨 Imprimante
                ThermalPrinterName = EditPrinterName?.Trim() ?? "",
                PaperWidthMm = EditPaperWidth,
                AutoPrintReceipt = EditAutoPrint,
                PrintCopies = EditPrintCopies,
                EnableCustomerDisplay = EditEnableCustomerDisplay,
                EnableCashDrawer = EditEnableCashDrawer,
                CashDrawerPin = EditCashDrawerPin,
                PrinterCodePage = EditCodePage,
                PrintLogo = EditPrintLogo,
                ReceiptFooterText = EditFooterText?.Trim() ?? "Merci pour votre achat !"
            };
            result = await _posService.CreateAsync(pos);
        }
        else
        {
            var pos = await _posService.GetByIdAsync(_editId);
            if (pos == null) { ShowErrorMessage("POS introuvable."); return; }

            pos.Code = EditCode;
            pos.Name = EditName;
            pos.Address = EditAddress;
            pos.City = EditCity;
            pos.Phone = EditPhone;
            pos.ManagesStock = EditManagesStock;
            pos.AllowNegativeStock = EditAllowNegativeStock;
            pos.DeviceType = deviceType;
            pos.EmcfApiUrl = NullIfEmpty(EditEmcfUrl);
            pos.EmcfToken = NullIfEmpty(EditEmcfToken);
            pos.EmcfNIM = NullIfEmpty(EditEmcfNim);

            // 🖨 Imprimante
            pos.ThermalPrinterName = EditPrinterName?.Trim() ?? "";
            pos.PaperWidthMm = EditPaperWidth;
            pos.AutoPrintReceipt = EditAutoPrint;
            pos.PrintCopies = EditPrintCopies;
            pos.EnableCustomerDisplay = EditEnableCustomerDisplay;
            pos.EnableCashDrawer = EditEnableCashDrawer;
            pos.CashDrawerPin = EditCashDrawerPin;
            pos.PrinterCodePage = EditCodePage;
            pos.PrintLogo = EditPrintLogo;
            pos.ReceiptFooterText = EditFooterText?.Trim() ?? "Merci pour votre achat !";

            result = await _posService.UpdateAsync(pos);
        }

        if (result.Success)
        {
            IsEditing = false;
            await LoadAsync();
            _ = ShowSuccessAsync(_editId == 0 ? "✅ POS créé avec succès." : "✅ POS mis à jour.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task DeactivatePosAsync(PointOfSale pos)
    {
        var result = await _posService.DeactivateAsync(pos.Id);
        if (result.Success)
        {
            await LoadAsync();
            _ = ShowSuccessAsync($"POS {pos.Code} désactivé.");
        }
        else ShowErrorMessage(result.ErrorMessage);
    }

    [RelayCommand]
    private async Task InitializeStockAsync(PointOfSale pos)
    {
        IsBusy = true;
        try
        {
            var count = await _stockService.InitializePosStockFromProductsAsync(pos.Id, "Admin");
            _ = ShowSuccessAsync($"✅ {count} produit(s) initialisé(s) dans {pos.Code}.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur initialisation stock : {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    //  🖨 COMMANDES — IMPRIMANTE
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void RefreshPrinterList()
    {
        var previous = EditPrinterName;
        DetectedPrinters.Clear();
        DetectedPrinters.Add(""); // vide = auto-détection

        try
        {
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                DetectedPrinters.Add(printer);
        }
        catch
        {
            // System.Drawing non disponible ou aucune imprimante
        }

        // Restaurer la sélection
        if (!string.IsNullOrEmpty(previous) && DetectedPrinters.Contains(previous))
            EditPrinterName = previous;
    }

    [RelayCommand]
    private void AutoDetectPrinter()
    {
        string[] thermalKeywords =
        {
            "pos", "thermal", "receipt", "epson", "tm-t", "tm-m",
            "star ", "tsp", "bixolon", "srp-", "citizen", "ct-",
            "xprinter", "xp-", "rongta", "rp-", "zjiang",
            "pos-58", "pos-80", "80mm", "58mm", "optima"
        };

        try
        {
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                string lower = printer.ToLowerInvariant();
                foreach (var kw in thermalKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        EditPrinterName = printer;
                        _ = ShowSuccessAsync($"🖨 Imprimante détectée : {printer}");
                        return;
                    }
                }
            }
        }
        catch { /* ignore */ }

        ShowErrorMessage("Aucune imprimante thermique détectée automatiquement.");
    }

    [RelayCommand]
    private void TestPrint()
    {
        string printerName = EditPrinterName?.Trim() ?? "";

        // Si vide, tenter auto-détection rapide
        if (string.IsNullOrEmpty(printerName))
        {
            AutoDetectPrinter();
            printerName = EditPrinterName?.Trim() ?? "";
        }

        if (string.IsNullOrEmpty(printerName))
        {
            ShowErrorMessage("Aucune imprimante configurée ou détectée.");
            return;
        }

        try
        {
            int charsPerLine = EditPaperWidth >= 80 ? 48 : 32;
            byte[] receipt = BuildTestReceipt(charsPerLine, EditCodePage);
            RawPrinterHelper.SendBytesToPrinter(printerName, receipt, "SFE-TestPrint");

            _ = ShowSuccessAsync($"🖨 Ticket test envoyé à « {printerName} ».");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur impression : {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════

    private async Task<bool> EnsureCompanyLoadedAsync()
    {
        if (CompanyId > 0) return true;

        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company != null)
        {
            CompanyId = company.Id;
            return true;
        }

        ShowErrorMessage("Aucune entreprise configurée.");
        return false;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>
    /// Construit un ticket test ESC/POS complet pour vérifier
    /// l'alignement, les accents et la largeur papier.
    /// </summary>
    private static byte[] BuildTestReceipt(int charsPerLine, int codePage)
    {
        using var ms = new MemoryStream();

        void Write(params byte[] data) => ms.Write(data, 0, data.Length);

        Encoding enc;
        try { enc = Encoding.GetEncoding(codePage); }
        catch { enc = Encoding.GetEncoding(858); }

        void PrintLine(string text)
        {
            byte[] bytes = enc.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
            ms.WriteByte(0x0A); // LF
        }

        // ── ESC @ : Initialize printer ──
        Write(0x1B, 0x40);

        // ── Set code page ──
        byte cpByte = codePage switch
        {
            437 => 0x00,
            850 => 0x02,
            858 => 0x13,
            1252 => 0x10,
            _ => 0x13
        };
        Write(0x1B, 0x74, cpByte);

        // ── Center align ──
        Write(0x1B, 0x61, 0x01);

        // ── Bold ON + Double height/width ──
        Write(0x1B, 0x45, 0x01);
        Write(0x1D, 0x21, 0x11);

        PrintLine("SFE GECOM");

        // ── Reset size ──
        Write(0x1D, 0x21, 0x00);
        Write(0x1B, 0x45, 0x00);

        PrintLine("");
        PrintLine(new string('=', charsPerLine));
        PrintLine("TEST D'IMPRESSION");
        PrintLine(new string('=', charsPerLine));
        PrintLine("");
        PrintLine($"Largeur : {(charsPerLine >= 48 ? 80 : 58)} mm");
        PrintLine($"Caractères/ligne : {charsPerLine}");
        PrintLine($"Code page : {codePage}");
        PrintLine($"Date : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        PrintLine("");
        PrintLine("Caractères spéciaux :");
        PrintLine("é è ê ë à â ù û ç ô î ï €");
        PrintLine("");
        PrintLine(new string('-', charsPerLine));

        // ── Left align for table ──
        Write(0x1B, 0x61, 0x00);

        void PrintRow(string left, string right)
        {
            int gap = charsPerLine - left.Length - right.Length;
            if (gap < 1) gap = 1;
            PrintLine(left + new string(' ', gap) + right);
        }

        PrintRow("Article test", "1 500,00 CDF");
        PrintRow("TVA 16%", "240,00 CDF");

        // ── Center ──
        Write(0x1B, 0x61, 0x01);
        PrintLine(new string('-', charsPerLine));

        // ── Bold total ──
        Write(0x1B, 0x61, 0x00);
        Write(0x1B, 0x45, 0x01);
        Write(0x1D, 0x21, 0x01); // Double height

        PrintRow("TOTAL TTC", "1 740,00 CDF");

        Write(0x1D, 0x21, 0x00);
        Write(0x1B, 0x45, 0x00);

        // ── Center footer ──
        Write(0x1B, 0x61, 0x01);
        PrintLine(new string('=', charsPerLine));
        PrintLine("");
        PrintLine("Si ce ticket s'imprime");
        PrintLine("correctement, votre");
        PrintLine("imprimante est configurée !");
        PrintLine("");
        PrintLine("--- iKWOOK SFE ---");
        PrintLine("");

        // ── Feed 5 lines + Partial cut ──
        Write(0x1B, 0x64, 0x05);
        Write(0x1D, 0x56, 0x01);

        return ms.ToArray();
    }
}