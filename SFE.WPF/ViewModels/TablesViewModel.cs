using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
// N'oubliez pas l'using pour IActivatable si nécessaire (dépend de votre namespace)

namespace SFE.WPF.ViewModels;

// 🆕 Ajoutez IActivatable ici
public partial class TablesViewModel : BaseViewModel, IActivatable
{
    private readonly ITableService _tableService;

    [ObservableProperty]
    private ObservableCollection<Table> _tables = new();

    [ObservableProperty]
    private Table? _selectedTable;

    public TablesViewModel(ITableService tableService)
    {
        _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
        PageTitle = "Plan de Salle";
    }

    // 🆕 Implémentez la méthode qui sera appelée automatiquement à l'ouverture de la page
    public async Task ActivateAsync()
    {
        await LoadTablesAsync();
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
            Tables = new ObservableCollection<Table>(tables);
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
    public async Task TableTappedAsync(Table? table)
    {
        if (table == null) return;

        try
        {
            if (table.Status == TableStatus.Free)
            {
                await _tableService.ChangeStatusAsync(table.Id, TableStatus.Occupied);
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new SFE.WPF.Messages.OpenTableMessage(table.Id, null));
            }
            else if (table.Status == TableStatus.Occupied)
            {
                var activeOrder = await _tableService.GetActiveOrderAsync(table.Id);
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new SFE.WPF.Messages.OpenTableMessage(table.Id, activeOrder?.Id));
            }
            else if (table.Status == TableStatus.Cleaning)
            {
                await _tableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                await LoadTablesAsync();
                await ShowSuccessAsync($"La table {table.Number} est maintenant libre.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.Message);
        }
    }
}