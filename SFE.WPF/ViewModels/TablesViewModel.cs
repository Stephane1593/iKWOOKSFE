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
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Events;

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

        // 🚨 Écoute des mises à jour réseau ET du bouton rafraîchir
        Subscribe(LoadTablesAsync, AppEvent.TableStatusChanged, AppEvent.ForceGlobalRefresh);
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
            List<TableDisplayModel> displayList = new();

            // 1. Enter the database bubble
            await RunInScopeAsync(async (sp) =>
            {
                var scopedTableService = sp.GetRequiredService<ITableService>();
                var tables = await scopedTableService.GetAllAsync();

                foreach (var table in tables)
                {
                    var displayModel = new TableDisplayModel(table);
                    var activeOrder = await scopedTableService.GetActiveOrderAsync(table.Id);

                    if (activeOrder != null)
                    {
                        if (table.Status == TableStatus.Free)
                        {
                            table.Status = TableStatus.Occupied;
                            await scopedTableService.ChangeStatusAsync(table.Id, TableStatus.Occupied);
                        }
                        displayModel.OrderTotal = activeOrder.TotalTTC;
                        displayModel.WaiterName = activeOrder.WaiterName ?? "Serveur";
                        displayModel.OrderTime = activeOrder.CreatedAtUtc;
                    }
                    else if (table.Status == TableStatus.Occupied)
                    {
                        table.Status = TableStatus.Free;
                        await scopedTableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                    }
                    displayList.Add(displayModel);
                }
            });

            // 🚨 2. FORCE THE UI TO REDRAW INSTANTLY ON THE MAIN THREAD
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Tables = new ObservableCollection<TableDisplayModel>(displayList);
            });
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
                _pendingTableForOpen = model;
                NewWaiterName = "";
                ShowWaiterPrompt = true;
            }
            else if (table.Status == TableStatus.Occupied)
            {
                int? activeOrderId = null;

                // 🚨 Use the scope bubble so tapping a table doesn't crash the background sync!
                await RunInScopeAsync(async (sp) =>
                {
                    var scopedTableService = sp.GetRequiredService<ITableService>();
                    var activeOrder = await scopedTableService.GetActiveOrderAsync(table.Id);
                    activeOrderId = activeOrder?.Id;
                });

                WeakReferenceMessenger.Default.Send(new OpenTableMessage(table.Id, activeOrderId));
            }
            else if (table.Status == TableStatus.Cleaning)
            {
                await RunInScopeAsync(async (sp) =>
                {
                    var scopedTableService = sp.GetRequiredService<ITableService>();
                    await scopedTableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                });
                await LoadTablesAsync();
                _ = ShowSuccessAsync($"La table {table.Number} est libre.");
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