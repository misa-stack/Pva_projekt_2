using System.Text.Json;

namespace projekt_pva_2;

public sealed class FileSystem
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string FilePath { get; }
    public bool RecoveredFromBackup { get; private set; }

    private string BackupPath => FilePath + ".bak";

    public FileSystem(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        using var fileLock = Lock();
        if (File.Exists(FilePath) || File.Exists(BackupPath))
            Load();
        else
            Save(new Dictionary<string, JsonElement>());
    }

    public int Count
    {
        get
        {
            using var fileLock = Lock();
            return Load().Count;
        }
    }

    public IReadOnlyList<string> Keys
    {
        get
        {
            using var fileLock = Lock();
            return Load().Keys.ToArray();
        }
    }

    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var fileLock = Lock();
        return Load()[key].Deserialize<T>(JsonOptions);
    }

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var serialized = JsonSerializer.SerializeToElement(value, JsonOptions);
        using var fileLock = Lock();
        var data = Load();
        data[key] = serialized;
        Save(data);
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var fileLock = Lock();
        var data = Load();
        if (!data.Remove(key)) return false;
        Save(data);
        return true;
    }

    public void Clear()
    {
        using var fileLock = Lock();
        var data = Load();
        if (data.Count == 0) return;
        data.Clear();
        Save(data);
    }

    private FileStream Lock() =>
        File.Open(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private Dictionary<string, JsonElement> Load()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                return Read(FilePath);
            }
            catch (InvalidDataException) when (File.Exists(BackupPath))
            {
            }
        }
        else if (!File.Exists(BackupPath))
        {
            throw new FileNotFoundException("The save file and its backup are missing.", FilePath);
        }

        var recovered = Read(BackupPath);
        Save(recovered, keepBackup: false);
        RecoveredFromBackup = true;
        return recovered;
    }

    private static Dictionary<string, JsonElement> Read(string path)
    {
        SaveDocument? document;
        try
        {
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("format", out var format)
                || format.ValueKind != JsonValueKind.String
                || format.GetString() != "pva-save"
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var versionNumber))
                throw new InvalidDataException($"File '{path}' is not a valid PVA save.");

            // Check the version before interpreting a potentially different data schema.
            if (versionNumber != 1)
                throw new NotSupportedException($"Save version {versionNumber} in '{path}' is not supported.");

            document = root.Deserialize<SaveDocument>(JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Save file '{path}' is damaged or has an invalid format.", exception);
        }

        if (document is null || document.Format != "pva-save" || document.Data is null)
            throw new InvalidDataException($"File '{path}' is not a valid PVA save.");
        if (document.Data.Keys.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"Save file '{path}' contains an empty key.");

        return document.Data;
    }

    private void Save(Dictionary<string, JsonElement> data, bool keepBackup = true)
    {
        var temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new SaveDocument
                {
                    Format = "pva-save",
                    Version = 1,
                    Data = data
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            if (keepBackup && File.Exists(FilePath))
                File.Replace(temporaryPath, FilePath, BackupPath);
            else
                File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class SaveDocument
    {
        public required string Format { get; init; }
        public required int Version { get; init; }
        public required Dictionary<string, JsonElement> Data { get; init; }
    }
}
