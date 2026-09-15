using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.WPF.Messages;

namespace SFE.WPF.ViewModels;

public partial class TableDisplayModel : ObservableObject
{
    [ObservableProperty] private Table _entity;
    [ObservableProperty] private string _waiterName = "";
    [ObservableProperty] private decimal _orderTotal;
    [ObservableProperty] private DateTimeOffset? _orderTime;

    public TableDisplayModel(Table entity)
    {
        Entity = entity;
    }
}

public partial class TablesViewModel : BaseViewModel, IActivatable, IRecipient<ReloadTablesMessage>
{
    private readonly ITableService _tableService;

    [ObservableProperty]
    private ObservableCollection<TableDisplayModel> _tables = new();

    [ObservableProperty]
    private TableDisplayModel? _selectedTable;

    // 🚨 NOUVELLES PROPRIÉTÉS POUR LE POPUP SERVEUR
    [ObservableProperty] private bool _showWaiterPrompt;
    [ObservableProperty] private string _newWaiterName = "";
    private TableDisplayModel? _pendingTableForOpen;

    public TablesViewModel(ITableService tableService)
    {
        _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
        PageTitle = "Plan de Salle";
        WeakReferenceMessenger.Default.Register(this);
    }

    public async Task ActivateAsync()
    {
        await LoadTablesAsync();
    }

    public void Receive(ReloadTablesMessage message)
    {
        _ = LoadTablesAsync();
    }

    [RelayCommand]
    public async Task LoadTablesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearStatus();

        try
        {
            var tables = await _tableService.GetAllAsync();
            var displayList = new List<TableDisplayModel>();

            foreach (var table in tables)
            {
                var displayModel = new TableDisplayModel(table);
                var activeOrder = await _tableService.GetActiveOrderAsync(table.Id);

                if (activeOrder != null)
                {
                    if (table.Status == TableStatus.Free)
                    {
                        table.Status = TableStatus.Occupied;
                        await _tableService.ChangeStatusAsync(table.Id, TableStatus.Occupied);
                    }

                    displayModel.OrderTotal = activeOrder.TotalTTC;
                    displayModel.WaiterName = activeOrder.WaiterName ?? "Serveur";
                    displayModel.OrderTime = activeOrder.CreatedAtUtc;
                }
                else
                {
                    if (table.Status == TableStatus.Occupied)
                    {
                        table.Status = TableStatus.Free;
                        await _tableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                    }
                }

                displayList.Add(displayModel);
            }

            Tables = new ObservableCollection<TableDisplayModel>(displayList);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur lors du chargement des tables : {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task TableTappedAsync(TableDisplayModel? model)
    {
        if (model == null) return;
        var table = model.Entity;

        try
        {
            if (table.Status == TableStatus.Free)
            {
                // 🚨 Au lieu d'ouvrir directement, on prépare le popup
                _pendingTableForOpen = model;
                NewWaiterName = "";
                ShowWaiterPrompt = true;
            }
            else if (table.Status == TableStatus.Occupied)
            {
                var activeOrder = await _tableService.GetActiveOrderAsync(table.Id);
                WeakReferenceMessenger.Default.Send(new OpenTableMessage(table.Id, activeOrder?.Id));
            }
            else if (table.Status == TableStatus.Cleaning)
            {
                await _tableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                await LoadTablesAsync();
                _ = ShowSuccessAsync($"La table {table.Number} est maintenant libre.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.Message);
        }
    }

    // 🚨 COMMANDES DU POPUP
    [RelayCommand]
    private async Task ConfirmWaiterAsync()
    {
        ShowWaiterPrompt = false;
        if (_pendingTableForOpen == null) return;

        var table = _pendingTableForOpen.Entity;
        await _tableService.ChangeStatusAsync(table.Id, TableStatus.Occupied);
        table.Status = TableStatus.Occupied;

        // 🚨 Envoie le nom saisi au POS
        WeakReferenceMessenger.Default.Send(new OpenTableMessage(table.Id, null, NewWaiterName));
        _pendingTableForOpen = null;
    }

    [RelayCommand]
    private void CancelWaiterPrompt()
    {
        ShowWaiterPrompt = false;
        _pendingTableForOpen = null;
    }
}