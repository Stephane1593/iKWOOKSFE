using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class ClientsViewModel : BaseViewModel
{
    private readonly ClientService _clientService;

    // ══════ LIST ══════
    public ObservableCollection<ClientListItem> Clients { get; } = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ClientType? _filterType;
    [ObservableProperty] private int _clientCount;

    // ══════ FORM ══════
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _formTitle = "";
    private int _editingId;

    [ObservableProperty] private ClientType _formType = ClientType.PP;
    [ObservableProperty] private string _formNIF = "";
    [ObservableProperty] private string _formName = "";
    [ObservableProperty] private string _formAddress = "";
    [ObservableProperty] private string _formPhone = "";
    [ObservableProperty] private string _formEmail = "";
    [ObservableProperty] private string _formRCCM = "";

    // ══════ VALIDATION HINTS ══════
    public bool IsNifRequired => FormType is ClientType.PM or ClientType.PC or ClientType.PL;
    public bool IsNameRequired => FormType != ClientType.PP;
    public string FormTypeDescription => ClientService.GetTypeMention(FormType);

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;

    // ══════ ENUMS ══════
    public ClientType[] ClientTypes { get; } = Enum.GetValues<ClientType>();

    public ClientsViewModel(ClientService clientService)
    {
        _clientService = clientService;
        PageTitle = "Clients";
        _ = LoadAsync();
    }

    // ══════ PROPERTY CHANGE HANDLERS ══════

    partial void OnFormTypeChanged(ClientType value)
    {
        OnPropertyChanged(nameof(IsNifRequired));
        OnPropertyChanged(nameof(IsNameRequired));
        OnPropertyChanged(nameof(FormTypeDescription));
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnFilterTypeChanged(ClientType? value) => _ = LoadAsync();

    // ══════ LOAD ══════

    private async Task LoadAsync()
    {
        try
        {
            List<Client> list;
            if (!string.IsNullOrWhiteSpace(SearchText))
                list = await _clientService.SearchAsync(SearchText, 200);
            else
                list = await _clientService.GetAllAsync();

            if (FilterType.HasValue)
                list = list.Where(c => c.Type == FilterType.Value).ToList();

            Clients.Clear();
            foreach (var c in list)
                Clients.Add(new ClientListItem(c));

            ClientCount = Clients.Count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
    }

    // ══════ COMMANDS ══════

    [RelayCommand]
    private void NewClient()
    {
        ClearForm();
        IsCreating = true;
        IsEditing = true;
        FormTitle = "Nouveau client";
    }

    [RelayCommand]
    private void EditClient(ClientListItem? item)
    {
        if (item == null) return;
        _editingId = item.Id;
        FormType = item.Type;
        FormNIF = item.NIF ?? "";
        FormName = item.Name;
        FormAddress = item.Address ?? "";
        FormPhone = item.Phone ?? "";
        FormEmail = item.Email ?? "";
        FormRCCM = item.RCCM ?? "";

        IsCreating = false;
        IsEditing = true;
        FormTitle = $"Modifier — {item.Name}";
    }

    [RelayCommand]
    private async Task SaveClient()
    {
        ClearStatus();

        var client = new Client
        {
            Id = IsCreating ? 0 : _editingId,
            Type = FormType,
            NIF = string.IsNullOrWhiteSpace(FormNIF) ? null : FormNIF.Trim(),
            Name = FormName.Trim(),
            Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim(),
            Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
            Email = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim(),
            RCCM = string.IsNullOrWhiteSpace(FormRCCM) ? null : FormRCCM.Trim(),
        };

        var result = IsCreating
            ? await _clientService.CreateAsync(client)
            : await _clientService.UpdateAsync(client);

        if (result.IsValid)
        {
            StatusMessage = IsCreating ? "✓ Client créé avec succès." : "✓ Client mis à jour.";
            ShowSuccess = true;
            IsEditing = false;
            await LoadAsync();
        }
        else
        {
            StatusMessage = result.ErrorMessage;
            ShowError = true;
        }
    }

    [RelayCommand]
    private async Task DeleteClient(ClientListItem? item)
    {
        if (item == null) return;
        ClearStatus();
        try
        {
            await _clientService.DeleteAsync(item.Id);
            if (IsEditing && _editingId == item.Id) CancelEdit();
            StatusMessage = "✓ Client supprimé.";
            ShowSuccess = true;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsCreating = false;
        ClearForm();
    }

    // ══════ HELPERS ══════

    private void ClearForm()
    {
        _editingId = 0;
        FormType = ClientType.PP;
        FormNIF = ""; FormName = ""; FormAddress = "";
        FormPhone = ""; FormEmail = ""; FormRCCM = "";
        FormTitle = "";
        ClearStatus();
    }

    private void ClearStatus()
    {
        ShowError = false;
        ShowSuccess = false;
        StatusMessage = "";
    }
}

// ══════ LIST DISPLAY WRAPPER ══════

public class ClientListItem
{
    public int Id { get; }
    public ClientType Type { get; }
    public string TypeCode { get; }
    public string TypeLabel { get; }
    public string? NIF { get; }
    public string Name { get; }
    public string? Address { get; }
    public string? Phone { get; }
    public string? Email { get; }
    public string? RCCM { get; }
    public string CreatedDisplay { get; }

    public ClientListItem(Client c)
    {
        Id = c.Id;
        Type = c.Type;
        TypeCode = c.Type.ToString();
        TypeLabel = ClientService.GetTypeLabel(c.Type);
        NIF = c.NIF;
        Name = c.Name;
        Address = c.Address;
        Phone = c.Phone;
        Email = c.Email;
        RCCM = c.RCCM;
        CreatedDisplay = c.CreatedAt.ToString("dd/MM/yyyy");
    }
}