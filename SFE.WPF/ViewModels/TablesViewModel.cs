using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.WPF.Messages; 

namespace SFE.WPF.ViewModels;

// 🚨 Ajout de IRecipient<ReloadTablesMessage>
public partial class TablesViewModel : BaseViewModel, IActivatable, IRecipient<ReloadTablesMessage>
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

        // 🚨 On s'abonne aux messages de rafraîchissement
        WeakReferenceMessenger.Default.Register(this);
    }

    public async Task ActivateAsync()
    {
        await LoadTablesAsync();
    }

    // 🚨 Méthode déclenchée quand le PosViewModel envoie le message
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

            // 🚨 AUTO-CORRECTION (Source de vérité) : 
            // On vérifie s'il y a des commandes actives pour s'assurer que le statut visuel est correct.
            foreach (var table in tables)
            {
                var activeOrder = await _tableService.GetActiveOrderAsync(table.Id);

                if (activeOrder != null && table.Status == TableStatus.Free)
                {
                    // Une commande existe, mais la table est marquée "Libre" -> On corrige
                    table.Status = TableStatus.Occupied;
                    await _tableService.ChangeStatusAsync(table.Id, TableStatus.Occupied);
                }
                else if (activeOrder == null && table.Status == TableStatus.Occupied)
                {
                    // Aucune commande n'existe, mais la table est bloquée sur "Occupée" -> On libère
                    table.Status = TableStatus.Free;
                    await _tableService.ChangeStatusAsync(table.Id, TableStatus.Free);
                }
            }

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

                // 🚨 Mise à jour instantanée de l'UI avant d'ouvrir la caisse
                table.Status = TableStatus.Occupied;

                WeakReferenceMessenger.Default.Send(new OpenTableMessage(table.Id, null));
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
                await ShowSuccessAsync($"La table {table.Number} est maintenant libre.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.Message);
        }
    }
}