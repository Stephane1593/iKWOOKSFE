using System;
using System.IO;
using System.Text.Json;

namespace SFE.Application.Helpers;

public class LocalNetworkConfig
{
    // The type of database: "SQLite", "PostgreSQL", or "MySQL"
    public string DatabaseProvider { get; set; } = "SQLite";

    // The address to connect to (empty if SQLite)
    public string ConnectionString { get; set; } = "";

    // Is this computer the main server or a client?
    public bool IsServer { get; set; } = true;

    //NOUVEAU: Propriétés pour le mode "Local + Sync" (Offline-First)
    public bool EnableSync { get; set; } = false;
    public string SyncServerUrl { get; set; } = "";

    // --- HELPER METHODS TO READ/WRITE THE STICKY NOTE ---

    private static string GetConfigFilePath()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SFE");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, "localconfig.json");
    }

    public static LocalNetworkConfig Load()
    {
        var filePath = GetConfigFilePath();
        if (!File.Exists(filePath))
        {
            var defaultConfig = new LocalNetworkConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<LocalNetworkConfig>(json) ?? new LocalNetworkConfig();
        }
        catch
        {
            return new LocalNetworkConfig(); // Fallback to SQLite if file is corrupted
        }
    }

    public static void Save(LocalNetworkConfig config)
    {
        var filePath = GetConfigFilePath();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}