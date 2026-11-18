using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Core;
using SoBar.Core;
using SoBar.Plugins;

namespace SoBar.Infrastructure;

public sealed class AppServices : IDisposable
{
    private static readonly Lazy<bool> NativeSqlite = new(() => {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        return true;
    });
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8, WriteIndented = true };
    private readonly SqliteConnection _database;
    private readonly Logger _log;
    private readonly bool _useStartup;
    private readonly bool _isolatePlugins;
    private readonly Assistant _assistant = new();
    private readonly LocalSearch _search = new();
    private ExtensionManifest? _pendingManifest;
    private string? _pendingManifestPath;
    private readonly object _sync = new();
    private string _current = "{}";
    public string DataDirectory { get; }
    public AppSettings Settings { get { lock (_sync) return Decode(_current); } }

    public AppServices(string? dataDirectory = null, bool isolatePlugins = false)
    {
        _isolatePlugins = isolatePlugins;
        _useStartup = dataDirectory == null;
        DataDirectory = Path.GetFullPath(dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoBar"));
        Directory.CreateDirectory(DataDirectory);
        _log = new LoggerConfiguration().WriteTo.File(Path.Combine(DataDirectory, "logs", "sobar-.log"),
            rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, fileSizeLimitBytes: 1_000_000,
            rollOnFileSizeLimit: true).CreateLogger();
        _ = NativeSqlite.Value;
        var file = Path.Combine(DataDirectory, "settings.db");
        SqliteConnection Open()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
            try
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS kv (scope TEXT, name TEXT, value TEXT NOT NULL, PRIMARY KEY(scope,name));
                    CREATE TABLE IF NOT EXISTS history (query TEXT PRIMARY KEY);
                    """;
                create.ExecuteNonQuery();
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
        try { _database = Open(); }
        catch (SqliteException error) when (error.SqliteErrorCode is 11 or 26)
        {
            var suffix = ".corrupt-" + Guid.NewGuid().ToString("N");
            foreach (var candidate in new[] { file, file + "-journal", file + "-wal", file + "-shm" })
                if (File.Exists(candidate)) File.Move(candidate, candidate + suffix);
            _log.Warning("Banco inválido preservado; iniciando preferências padrão");
            _database = Open();
            if (_useStartup) ApplyStartup(false);
        }
        var stored = Get("app", "settings");
        if (stored != null)
        {
            try { Decode(stored); _current = stored; }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                Put("recovery", Guid.NewGuid().ToString("N"), stored);
                Put("app", "settings", "{}");
                if (_useStartup) ApplyStartup(false);
                _log.Warning("Preferências inválidas preservadas");
            }
        }
        Log("Sessão iniciada");
    }

    private static AppSettings Decode(string data)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(data, Json) ?? throw new InvalidDataException("Preferências vazias.");
        settings.Validate();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        lock (_sync)
        {
            var previous = Decode(_current);
            var json = JsonSerializer.Serialize(settings, Json);
            using var transaction = _database.BeginTransaction();
            Put("app", "settings", json, transaction);
            if (!settings.HistoryEnabled)
            {
                using var clear = _database.CreateCommand();
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM history";
                clear.ExecuteNonQuery();
            }
            var updateStartup = _useStartup && previous.StartWithWindows != settings.StartWithWindows;
            if (updateStartup) ApplyStartup(settings.StartWithWindows);
            try { transaction.Commit(); }
            catch { if (updateStartup) ApplyStartup(previous.StartWithWindows); throw; }
            _current = json;
        }
    }

    private static void ApplyStartup(bool enable)
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var shortcut = Path.Combine(startup,"SoBar AutoStart.lnk");
        if (!enable) { if (File.Exists(shortcut)) File.Delete(shortcut); return; }
        Directory.CreateDirectory(startup);
        var path = Environment.ProcessPath ?? throw new IOException("Executável atual desconhecido.");
        var type = Type.GetTypeFromProgID("WScript.Shell",throwOnError:true)!;
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(shortcut);
            try { link.TargetPath = path; link.WorkingDirectory = Path.GetDirectoryName(path); link.Save(); }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }

    private string? Get(string scope, string name)
    {
        lock (_sync)
        {
            using var cmd = _database.CreateCommand();
            cmd.CommandText = "SELECT value FROM kv WHERE scope=$s AND name=$n";
            cmd.Parameters.AddWithValue("$s",scope);
            cmd.Parameters.AddWithValue("$n",name);
            return cmd.ExecuteScalar() as string;
        }
    }
    private void Put(string scope, string name, string value, SqliteTransaction? tx = null)
    {
        lock (_sync)
        {
            using var cmd = _database.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR REPLACE INTO kv VALUES ($s,$n,$v)";
            cmd.Parameters.AddWithValue("$s",scope);
            cmd.Parameters.AddWithValue("$n",name);
            cmd.Parameters.AddWithValue("$v",value);
            cmd.ExecuteNonQuery();
        }
    }
    private string[] Rows(string sql, string? scope = null)
    {
        lock (_sync)
        {
            using var cmd = _database.CreateCommand();
            cmd.CommandText = sql;
            if (scope != null) cmd.Parameters.AddWithValue("$s",scope);
            using var reader = cmd.ExecuteReader();
            var values = new List<string>();
            while (reader.Read()) values.Add(reader.GetString(0));
            return values.ToArray();
        }
    }

    public void Export(string path)
    {
        var temp = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(Settings, Json)); File.Move(temp,path,true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Import(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 256 * 1024) throw new InvalidDataException("Arquivo de configuração muito grande.");
        using var reader = new StreamReader(stream);
        var imported = Decode(reader.ReadToEnd());
        imported.StartWithWindows = Settings.StartWithWindows;
        Save(imported);
    }
    public void SaveProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60 || name.Any(char.IsControl))
            throw new ArgumentException("Informe um nome de até 60 caracteres.");
        Put("profile",name.Trim(),JsonSerializer.Serialize(Settings, Json));
    }
    public void LoadProfile(string name)
    {
        var profile = Decode(Get("profile",name) ?? throw new InvalidDataException("Perfil desconhecido."));
        profile.StartWithWindows = Settings.StartWithWindows;
        Save(profile);
    }
    public string[] Profiles() => Rows("SELECT name FROM kv WHERE scope=$s ORDER BY name COLLATE NOCASE","profile");
    public string[] History() => Rows("SELECT query FROM history ORDER BY rowid DESC LIMIT 40");
    public void Remember(string query)
    {
        lock (_sync)
        {
            if (!Settings.HistoryEnabled || string.IsNullOrWhiteSpace(query) || query.Length > 256) return;
            using var cmd = _database.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO history(query) VALUES($q);
                DELETE FROM history WHERE rowid NOT IN (SELECT rowid FROM history ORDER BY rowid DESC LIMIT 40);
                """;
            cmd.Parameters.AddWithValue("$q",query.Trim());
            cmd.ExecuteNonQuery();
        }
    }

    public string PreviewPlugin(string path)
    {
        _pendingManifest = _isolatePlugins ? PluginBroker.Inspect(path) : ExtensionManifest.Read(path);
        _pendingManifestPath = Path.GetFullPath(path);
        return $"{_pendingManifest.Name} ({_pendingManifest.Id})\nFonte: {_pendingManifest.Source}\nPermissões: " +
            (_pendingManifest.Permissions.Length == 0 ? "nenhuma" : string.Join(", ", _pendingManifest.Permissions));
    }
    public void ImportPlugin(string path)
    {
        if (_pendingManifest is null || !string.Equals(Path.GetFullPath(path),_pendingManifestPath,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Revise as permissões da extensão antes de importar.");
        Put("plugin",_pendingManifest.Id,_pendingManifest.Serialize());
        _pendingManifest = null;
        _pendingManifestPath = null;
    }
    public void RemovePlugin(string id)
    {
        lock (_sync)
        {
            using var cmd = _database.CreateCommand();
            cmd.CommandText = "DELETE FROM kv WHERE scope='plugin' AND name=$id";
            cmd.Parameters.AddWithValue("$id",id);
            cmd.ExecuteNonQuery();
        }
    }
    public IReadOnlyList<PluginTile> PluginTiles()
    {
        var tiles = new List<PluginTile>();
        foreach (var raw in Rows("SELECT value FROM kv WHERE scope=$s ORDER BY name","plugin"))
        {
            try { tiles.Add(ExtensionManifest.FromStored(raw).Render()); }
            catch (Exception e) when (e is JsonException or InvalidDataException) { _log.Warning("Extensão inválida ignorada"); }
        }
        return tiles;
    }
    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query,CancellationToken token) => _search.FindAsync(query,token);
    public void Launch(SearchHit hit) => _search.Launch(hit);
    public bool HasApiKey => !string.IsNullOrEmpty(Get("secret","openai"));
    public void SetApiKey(string key)
    {
        if (key.Length > 2048 || key.Any(char.IsControl)) throw new ArgumentException("Chave inválida.");
        var bytes = Encoding.UTF8.GetBytes(key.Trim());
        try { Put("secret","openai", bytes.Length == 0 ? "" : Convert.ToBase64String(
            ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser))); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task<string> AskAsync(string prompt,CancellationToken token) =>
        (await AskWithActionsAsync(prompt,token).ConfigureAwait(false)).Text;
    public async Task<AssistantReply> AskWithActionsAsync(string prompt,CancellationToken token)
    {
        var encrypted = Get("secret","openai");
        if (string.IsNullOrEmpty(encrypted)) throw new InvalidOperationException("Configure a chave OpenAI.");
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted),null,DataProtectionScope.CurrentUser);
        try { return await _assistant.AskAsync(Encoding.UTF8.GetString(bytes),prompt,token).ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void InstallUpdate(string path) => UpdateInstaller.Start(path);
    public void Log(string message) => _log.Information("{Event}",message);
    public void Dispose() { _assistant.Dispose(); _database.Dispose(); _log.Dispose(); }
}
