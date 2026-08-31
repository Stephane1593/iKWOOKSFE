using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class ClientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ITimeProvider _time;

    public ClientService(IUnitOfWork unitOfWork, IAuditService audit, ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _time = time;
    }

    public async Task<List<Client>> GetAllAsync()
        => (await _unitOfWork.Clients.GetAllAsync()).OrderBy(c => c.Name).ToList();

    public async Task<List<Client>> SearchAsync(string term, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(term)) return new();
        var results = await _unitOfWork.Clients.SearchAsync(term);
        return results.Take(max).ToList();
    }

    public async Task<Client?> GetByNIFAsync(string nif)
        => await _unitOfWork.Clients.GetByNIFAsync(nif);

    public async Task<ClientSaveResult> CreateAsync(Client client)
    {
        var v = ValidateClient(client);
        if (!v.IsValid) return v;

        if (!string.IsNullOrWhiteSpace(client.NIF))
        {
            var dup = await _unitOfWork.Clients.GetByNIFAsync(client.NIF);
            if (dup != null)
                return new("Un client avec ce NIF existe déjà.");
        }

        // ⚠ DGI §1.1 — timestamps via ITimeProvider only.
        client.CreatedAt = _time.UtcNow.UtcDateTime;

        await _unitOfWork.Clients.AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        // ── AUDIT ── (fixed argument order: description, entityType, entityId)
        var description = $"Client « {client.Name} » · Type {client.Type}"
            + (!string.IsNullOrWhiteSpace(client.NIF) ? $" · NIF {client.NIF}" : "");

        await _audit.LogAsync(
            AuditAction.ClientCreated,
            AuditModule.Clients,
            description,
            entityType: "Client",
            entityId: client.Id.ToString());

        return new() { IsValid = true, Client = client };
    }

    public async Task<ClientSaveResult> UpdateAsync(Client client)
    {
        var v = ValidateClient(client);
        if (!v.IsValid) return v;

        if (!string.IsNullOrWhiteSpace(client.NIF))
        {
            var dup = await _unitOfWork.Clients.GetByNIFAsync(client.NIF);
            if (dup != null && dup.Id != client.Id)
                return new("Un autre client avec ce NIF existe déjà.");
        }

        // Capture old values for audit delta
        var existing = await _unitOfWork.Clients.GetByIdAsync(client.Id);
        var changes = new List<string>();
        if (existing != null)
        {
            if (existing.Name != client.Name)
                changes.Add($"Nom : « {existing.Name} » → « {client.Name} »");
            if (existing.Type != client.Type)
                changes.Add($"Type : {existing.Type} → {client.Type}");
            if (existing.NIF != client.NIF)
                changes.Add($"NIF : « {existing.NIF ?? "—"} » → « {client.NIF ?? "—"} »");
            if (existing.Phone != client.Phone)
                changes.Add($"Tél : « {existing.Phone ?? "—"} » → « {client.Phone ?? "—"} »");
            if (existing.Email != client.Email)
                changes.Add($"Email : « {existing.Email ?? "—"} » → « {client.Email ?? "—"} »");
            if (existing.Address != client.Address)
                changes.Add($"Adresse modifiée");
        }

        await _unitOfWork.Clients.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync();

        // ── AUDIT ── (fixed argument order)
        var description = changes.Count > 0
            ? $"Client « {client.Name} » · {string.Join(" · ", changes)}"
            : $"Client « {client.Name} » · Aucune modification détectée";

        await _audit.LogAsync(
            AuditAction.ClientUpdated,
            AuditModule.Clients,
            description,
            entityType: "Client",
            entityId: client.Id.ToString());

        return new() { IsValid = true, Client = client };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _unitOfWork.Clients.GetByIdAsync(id);
        if (client == null) return false;

        // Capture before deletion
        var name = client.Name;
        var type = client.Type;
        var nif = client.NIF;

        await _unitOfWork.Clients.DeleteAsync(client);
        await _unitOfWork.SaveChangesAsync();

        // ── AUDIT ── (fixed argument order)
        var description = $"Client « {name} » · Type {type}"
            + (!string.IsNullOrWhiteSpace(nif) ? $" · NIF {nif}" : "")
            + " · Supprimé";

        await _audit.LogAsync(
            AuditAction.ClientDeleted,
            AuditModule.Clients,
            description,
            entityType: "Client",
            entityId: id.ToString());

        return true;
    }

    /// <summary>
    /// Validation DGI 2026 — Champs obligatoires par type de client.
    /// PP  → tout facultatif
    /// PM  → Dénomination + NIF
    /// PC  → Nom + NIF
    /// PL  → Nom + NIF
    /// AO  → Nom (+ Réf. certificat exonération = CommentA sur la facture)
    /// </summary>
    public static ClientSaveResult ValidateClient(Client client)
    {
        switch (client.Type)
        {
            case ClientType.PM:
                if (string.IsNullOrWhiteSpace(client.Name))
                    return new("La dénomination est obligatoire pour une Personne Morale (PM).");
                if (string.IsNullOrWhiteSpace(client.NIF))
                    return new("Le NIF est obligatoire pour une Personne Morale (PM).");
                break;

            case ClientType.PC:
                if (string.IsNullOrWhiteSpace(client.Name))
                    return new("Le nom est obligatoire pour une Personne physique commerçante (PC).");
                if (string.IsNullOrWhiteSpace(client.NIF))
                    return new("Le NIF est obligatoire pour une Personne physique commerçante (PC).");
                break;

            case ClientType.PL:
                if (string.IsNullOrWhiteSpace(client.Name))
                    return new("Le nom est obligatoire pour une Profession libérale (PL).");
                if (string.IsNullOrWhiteSpace(client.NIF))
                    return new("Le NIF est obligatoire pour une Profession libérale (PL).");
                break;

            case ClientType.AO:
                if (string.IsNullOrWhiteSpace(client.Name))
                    return new("Le nom est obligatoire pour les Ambassades / Organisations internationales (AO).");
                break;

            case ClientType.PP:
            default:
                break;
        }

        return new() { IsValid = true };
    }

    public static string GetTypeLabel(ClientType type) => type switch
    {
        ClientType.PP => "Personne physique",
        ClientType.PM => "Personne morale",
        ClientType.PC => "Pers. phys. commerçante",
        ClientType.PL => "Profession libérale",
        ClientType.AO => "Ambassade / Org. int.",
        _ => type.ToString()
    };

    public static string GetTypeMention(ClientType type) => type switch
    {
        ClientType.PP => "[PP] Personne physique",
        ClientType.PM => "[PM] Personne Morale",
        ClientType.PC => "[PC] Personne physique commerçante",
        ClientType.PL => "[PL] Profession libérale",
        ClientType.AO => "[AO] Ambassades et Organisations internationales",
        _ => type.ToString()
    };
}

public class ClientSaveResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
    public Client? Client { get; set; }

    public ClientSaveResult() { }
    public ClientSaveResult(string error) { IsValid = false; ErrorMessage = error; }
}