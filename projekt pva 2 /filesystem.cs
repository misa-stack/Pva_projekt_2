using System.Globalization;
using System.Text;

namespace projekt_pva_2;

// Obecné úložiště: význam klíčů a jednotlivých položek určuje až aplikace.
public sealed class FileSystem
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
            Save(new Dictionary<string, string[]>());
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

    public string[] Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var fileLock = Lock();
        return Load()[key].ToArray();
    }

    public void Set(string key, params string[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(fields);
        var copy = CopyFields(fields);
        using var fileLock = Lock();
        var data = Load();
        data[key] = copy;
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

    public Dictionary<string, string[]> ReadAll()
    {
        using var fileLock = Lock();
        // Load pokaždé sestaví nový slovník i nová pole, žádnou sdílenou paměť nevracíme.
        return Load();
    }

    public void ReplaceAll(IReadOnlyDictionary<string, string[]> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var copy = new Dictionary<string, string[]>();
        foreach (var record in records)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(record.Key);
            copy.Add(record.Key, CopyFields(record.Value));
        }

        using var fileLock = Lock();
        Load(); // Ani úplné nahrazení nesmí přepsat neplatný nebo novější soubor.
        Save(copy);
    }

    private static string[] CopyFields(string[] fields)
    {
        if (fields is null)
            throw new ArgumentException("Pole položek nesmí být null.", nameof(fields));
        var copy = new string[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] is null)
                throw new ArgumentException("Položka nesmí být null.", nameof(fields));
            copy[i] = fields[i];
        }
        return copy;
    }

    // Zámek platí jen během jedné operace. Soubor zámku se nikdy nemaže.
    private FileStream Lock() =>
        File.Open(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private Dictionary<string, string[]> Load()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                return Read(FilePath);
            }
            catch (InvalidDataException) when (File.Exists(BackupPath))
            {
                // NotSupportedException sem nepatří: novější verzi neobnovujeme starší zálohou.
            }
        }
        else if (!File.Exists(BackupPath))
        {
            throw new FileNotFoundException("Hlavní soubor i jeho záloha chybí.", FilePath);
        }

        var recovered = Read(BackupPath);
        Save(recovered, keepBackup: false);
        RecoveredFromBackup = true;
        return recovered;
    }

    private static Dictionary<string, string[]> Read(string path)
    {
        string text;
        try
        {
            text = Utf8.GetString(File.ReadAllBytes(path));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"Soubor '{path}' není platný text UTF-8.", exception);
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        int lineCount = lines.Length;
        if (lines[^1] == "") lineCount--; // Nejvýše jeden koncový oddělovač řádků.

        string[] header = lines[0].Split('|');
        if (header.Length < 3 || header[0] != "PVA")
            throw new InvalidDataException("Chybné označení formátu.");
        int version = ParseCount(header[1]);
        // Verzi kontrolujeme před počtem záznamů a jejich tvarem.
        if (version != 1)
            throw new NotSupportedException($"Verze úložiště {version} není podporována.");
        if (header.Length != 3)
            throw new InvalidDataException("Chybná hlavička úložiště.");
        int recordCount = ParseCount(header[2]);
        if (text.Replace("\r\n", "").Contains('\r')
            || lineCount < 2 || recordCount != lineCount - 2 || lines[lineCount - 1] != "END")
            throw new InvalidDataException("Nesouhlasí počet záznamů nebo chybí konec END.");

        var data = new Dictionary<string, string[]>();
        for (int i = 1; i < lineCount - 1; i++)
        {
            var parts = ParseRecord(lines[i]);
            if (parts.Count < 3 || parts[0] != "D" || string.IsNullOrWhiteSpace(parts[1]))
                throw new InvalidDataException("Chybný záznam nebo prázdný klíč.");
            int fieldCount = ParseCount(parts[2]);
            if (fieldCount != parts.Count - 3)
                throw new InvalidDataException("Nesouhlasí počet položek záznamu.");
            if (!data.TryAdd(parts[1], parts.Skip(3).ToArray()))
                throw new InvalidDataException("Klíč je v souboru vícekrát.");
        }
        return data;
    }

    private static int ParseCount(string value)
    {
        if (value.Length == 0 || value.Any(character => character < '0' || character > '9')
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            throw new InvalidDataException("Počet nebo verze musí být nezáporné celé číslo.");
        return count;
    }

    private static List<string> ParseRecord(string line)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '|')
            {
                parts.Add(part.ToString());
                part.Clear();
            }
            else if (character == '\\')
            {
                i++;
                if (i == line.Length)
                    throw new InvalidDataException("Nedokončená escape sekvence.");
                switch (line[i])
                {
                    case '\\': part.Append('\\'); break;
                    case '|': part.Append('|'); break;
                    case 'n': part.Append('\n'); break;
                    case 'r': part.Append('\r'); break;
                    default: throw new InvalidDataException("Neznámá escape sekvence.");
                }
            }
            else
            {
                part.Append(character);
            }
        }
        parts.Add(part.ToString());
        return parts;
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("|", "\\|")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r");

    private void Save(Dictionary<string, string[]> data, bool keepBackup = true)
    {
        var text = new StringBuilder();
        text.Append("PVA|1|").Append(data.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var record in data)
        {
            text.Append("D|").Append(Escape(record.Key)).Append('|')
                .Append(record.Value.Length.ToString(CultureInfo.InvariantCulture));
            foreach (string field in record.Value)
                text.Append('|').Append(Escape(field));
            text.Append('\n');
        }
        text.Append("END\n");
        // I chybu převodu textu zjistíme dříve, než začneme měnit soubory.
        byte[] bytes = Utf8.GetBytes(text.ToString());
        string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
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
}
