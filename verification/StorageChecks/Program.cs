using System.Text;
using projekt_pva_2;

string root = Path.Combine(Path.GetTempPath(), "pva-storage-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
int failed = 0;
try
{
    Check("custom empty format and resume", path =>
    {
        var store = new FileSystem(path);
        Equal("PVA|1|0\nEND\n", File.ReadAllText(path));
        Equal(Path.GetFullPath(path), store.FilePath);
        Equal(0, new FileSystem(path).Count);
        Equal(false, store.RecoveredFromBackup);
    });
    Check("CRUD and resume", path =>
    {
        var store = new FileSystem(path);
        store.Set("first", "one", "two");
        store.Set("second", "three");
        store.Set("first", "changed");
        Equal(2, store.Count);
        Sequence(["first", "second"], store.Keys.Order().ToArray());
        Sequence(["changed"], new FileSystem(path).Get("first"));
        Equal(true, store.Remove("second"));
        Equal(false, store.Remove("missing"));
        Throws<KeyNotFoundException>(() => store.Get("second"));
        store.Clear();
        Equal(0, new FileSystem(path).Count);
    });
    Check("empty field and zero fields are distinct", path =>
    {
        var store = new FileSystem(path);
        store.Set("empty", "");
        store.Set("none");
        Sequence([""], new FileSystem(path).Get("empty"));
        Sequence([], store.Get("none"));
        Equal("PVA|1|2\nD|empty|1|\nD|none|0\nEND\n", File.ReadAllText(path));
    });
    Check("Czech text and escaped characters round trip", path =>
    {
        var store = new FileSystem(path);
        store.Set("klíč|\\\n\r", "Příliš žluťoučký kůň", "a|b", "c\\d", "x\ny\rz", "", "😀");
        Sequence(["Příliš žluťoučký kůň", "a|b", "c\\d", "x\ny\rz", "", "😀"],
            new FileSystem(path).Get("klíč|\\\n\r"));
        Equal("PVA|1|1\nD|klíč\\|\\\\\\n\\r|6|Příliš žluťoučký kůň|a\\|b|c\\\\d|x\\ny\\rz||😀\nEND\n",
            File.ReadAllText(path));
    });
    Check("Set Get and ReadAll use independent arrays", path =>
    {
        var store = new FileSystem(path);
        string[] fields = ["original"];
        store.Set("a", fields);
        fields[0] = "input changed";
        var retrieved = store.Get("a");
        retrieved[0] = "returned changed";
        var all = store.ReadAll();
        all["a"][0] = "dictionary changed";
        all.Clear();
        Sequence(["original"], store.Get("a"));
    });
    Check("ReplaceAll commits one snapshot and clones input", path =>
    {
        var store = new FileSystem(path);
        store.Set("old", "saved");
        string before = File.ReadAllText(path);
        var replacement = new Dictionary<string, string[]> { ["new"] = ["one", "two"], ["zero"] = [] };
        store.ReplaceAll(replacement);
        replacement["new"][0] = "changed";
        Equal(before, File.ReadAllText(path + ".bak"));
        Sequence(["new", "zero"], store.Keys.Order().ToArray());
        Sequence(["one", "two"], new FileSystem(path).Get("new"));
    });
    Check("conditional ReplaceAll rejects stale independent snapshot without writes", path =>
    {
        var first = new FileSystem(path);
        var second = new FileSystem(path);
        first.Set("a", "original");
        var firstBaseline = first.ReadAll();
        var secondBaseline = second.ReadAll();
        first.ReplaceAll(new Dictionary<string, string[]> { ["a"] = ["first update"] }, firstBaseline);
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Throws<IOException>(() => second.ReplaceAll(
            new Dictionary<string, string[]> { ["a"] = ["stale update"] }, secondBaseline));
        Preserved(path, main, backup);
        Sequence(["first update"], second.Get("a"));
    });
    Check("conditional ReplaceAll accepts equal snapshots in different key order", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "one", "two");
        store.Set("b");
        var baseline = new Dictionary<string, string[]> { ["b"] = [], ["a"] = ["one", "two"] };
        string previous = File.ReadAllText(path);
        store.ReplaceAll(new Dictionary<string, string[]> { ["new"] = ["saved"] }, baseline);
        Sequence(["saved"], new FileSystem(path).Get("new"));
        Equal(previous, File.ReadAllText(path + ".bak"));
    });
    Check("conditional ReplaceAll compares empty expected snapshot", path =>
    {
        var store = new FileSystem(path);
        var empty = new Dictionary<string, string[]>();
        store.ReplaceAll(new Dictionary<string, string[]> { ["a"] = ["one"] }, empty);
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Throws<IOException>(() => store.ReplaceAll(empty, empty));
        Preserved(path, main, backup);
    });
    Check("conditional ReplaceAll compares keys and arrays ordinally", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "Value", "second");
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Dictionary<string, string[]>[] mismatches =
        [
            new(StringComparer.OrdinalIgnoreCase) { ["A"] = ["Value", "second"] },
            new() { ["a"] = ["value", "second"] },
            new() { ["a"] = ["Value"] },
            new() { ["a"] = ["second", "Value"] },
            new() { ["a"] = ["Value", "second"], ["extra"] = [] }
        ];
        foreach (var baseline in mismatches)
            Throws<IOException>(() => store.ReplaceAll(new Dictionary<string, string[]>(), baseline));
        Preserved(path, main, backup);
    });
    Check("conditional ReplaceAll validates expected snapshot before writes", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "saved");
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        var replacement = new Dictionary<string, string[]> { ["new"] = ["data"] };
        Throws<ArgumentException>(() => store.ReplaceAll(replacement, new Dictionary<string, string[]> { [" "] = [] }));
        Throws<ArgumentException>(() => store.ReplaceAll(replacement, new Dictionary<string, string[]> { ["a"] = null! }));
        Throws<ArgumentException>(() => store.ReplaceAll(replacement, new Dictionary<string, string[]> { ["a"] = [null!] }));
        Preserved(path, main, backup);
    });
    Check("no-op remove and clear preserve backup", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "one");
        store.Clear();
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Equal(false, store.Remove("absent"));
        store.Clear();
        Preserved(path, main, backup);
    });
    Check("reject invalid input without changing either file", path =>
    {
        var store = new FileSystem(path);
        store.Set("old", "saved");
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Throws<ArgumentException>(() => store.Set(" \t", "bad"));
        Throws<ArgumentNullException>(() => store.Set("a", (string[])null!));
        Throws<ArgumentException>(() => store.Set("a", [null!]));
        Throws<ArgumentException>(() => store.Get(" "));
        Throws<ArgumentException>(() => store.Remove(" "));
        Throws<ArgumentNullException>(() => store.ReplaceAll(null!));
        Throws<ArgumentException>(() => store.ReplaceAll(new Dictionary<string, string[]> { [" "] = ["bad"] }));
        Throws<ArgumentException>(() => store.ReplaceAll(new Dictionary<string, string[]> { ["a"] = null! }));
        Throws<ArgumentException>(() => store.ReplaceAll(new Dictionary<string, string[]> { ["a"] = [null!] }));
        Preserved(path, main, backup);
    });
    Check("invalid UTF-16 cannot be silently serialized", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "saved");
        string main = File.ReadAllText(path);
        string backup = File.ReadAllText(path + ".bak");
        Throws<EncoderFallbackException>(() => store.Set("bad", "\ud800"));
        Throws<EncoderFallbackException>(() => store.ReplaceAll(new Dictionary<string, string[]> { ["bad"] = ["\udc00"] }));
        Preserved(path, main, backup);
        Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
    });

    (string Name, string Content)[] malformed =
    [
        ("legacy JSON", "{\"format\":\"pva-save\",\"version\":1,\"data\":{}}"),
        ("missing header", ""),
        ("wrong marker", "BAD|1|0\nEND\n"),
        ("missing version", "PVA||0\nEND\n"),
        ("header extra token", "PVA|1|0|extra\nEND\n"),
        ("negative count", "PVA|1|-1\nEND\n"),
        ("signed count", "PVA|1|+0\nEND\n"),
        ("whitespace count", "PVA|1| 0\nEND\n"),
        ("overflow count", "PVA|1|99999999999999999999\nEND\n"),
        ("record count mismatch", "PVA|1|2\nD|a|0\nEND\n"),
        ("missing terminator", "PVA|1|1\nD|a|1|value\n"),
        ("wrong terminator", "PVA|1|0\nSTOP\n"),
        ("extra line", "PVA|1|0\nEND\nextra\n"),
        ("extra final terminator", "PVA|1|0\nEND\n\n"),
        ("unknown record tag", "PVA|1|1\nX|a|0\nEND\n"),
        ("duplicate keys", "PVA|1|2\nD|a|0\nD|a|1|other\nEND\n"),
        ("duplicate identical records", "PVA|1|2\nD|a|0\nD|a|0\nEND\n"),
        ("blank key", "PVA|1|1\nD| \t|0\nEND\n"),
        ("escaped blank key", "PVA|1|1\nD|\\n|0\nEND\n"),
        ("missing field count", "PVA|1|1\nD|a\nEND\n"),
        ("invalid field count", "PVA|1|1\nD|a|-1\nEND\n"),
        ("too few fields", "PVA|1|1\nD|a|2|one\nEND\n"),
        ("too many fields", "PVA|1|1\nD|a|0|extra\nEND\n"),
        ("unknown escape", "PVA|1|1\nD|a|1|bad\\q\nEND\n"),
        ("truncated escape", "PVA|1|1\nD|a|1|bad\\\nEND\n"),
        ("raw carriage return", "PVA|1|1\nD|a|1|x\ry\nEND\n")
    ];
    foreach (var item in malformed)
        Check("reject " + item.Name, path =>
        {
            File.WriteAllText(path, item.Content);
            Throws<InvalidDataException>(() => new FileSystem(path));
            Equal(item.Content, File.ReadAllText(path));
            Equal(false, File.Exists(path + ".bak"));
        });

    Check("strict UTF-8 rejects invalid bytes", path =>
    {
        byte[] invalid = [0x50, 0x56, 0x41, 0x7c, 0x31, 0x7c, 0x30, 0x0a, 0xff];
        File.WriteAllBytes(path, invalid);
        Throws<InvalidDataException>(() => new FileSystem(path));
        Equal(true, invalid.SequenceEqual(File.ReadAllBytes(path)));
    });
    Check("accept no final line terminator and CRLF", path =>
    {
        File.WriteAllText(path, "PVA|1|1\r\nD|a|1|A\r\nEND");
        Sequence(["A"], new FileSystem(path).Get("a"));
    });
    Check("unsupported version precedes version-specific schema and recovery", path =>
    {
        string unsupported = "PVA|2|future-shape|future-count\nNEW records\nEND\n";
        string backup = "PVA|1|1\nD|old|1|saved\nEND\n";
        File.WriteAllText(path, unsupported);
        File.WriteAllText(path + ".bak", backup);
        Throws<NotSupportedException>(() => new FileSystem(path));
        Preserved(path, unsupported, backup);
    });
    Check("valid backup restores corrupt main without rotating corruption", path =>
    {
        string backup = "PVA|1|1\nD|a|1|saved\nEND\n";
        File.WriteAllText(path, "damaged main");
        File.WriteAllText(path + ".bak", backup);
        var store = new FileSystem(path);
        Equal(true, store.RecoveredFromBackup);
        Sequence(["saved"], store.Get("a"));
        Preserved(path, backup, backup);
    });
    Check("valid backup restores missing main", path =>
    {
        string backup = "PVA|1|1\nD|a|0\nEND\n";
        File.WriteAllText(path + ".bak", backup);
        var store = new FileSystem(path);
        Equal(true, store.RecoveredFromBackup);
        Preserved(path, backup, backup);
    });
    Check("both corrupt files are preserved", path =>
    {
        File.WriteAllText(path, "broken main");
        File.WriteAllText(path + ".bak", "broken backup");
        Throws<InvalidDataException>(() => new FileSystem(path));
        Preserved(path, "broken main", "broken backup");
    });
    Check("existing instance rejects externally changed unsupported version", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "saved");
        string backup = File.ReadAllText(path + ".bak");
        string unsupported = "PVA|2|1\nNEW|future\nEND\n";
        File.WriteAllText(path, unsupported);
        Throws<NotSupportedException>(() => store.Set("b", "new"));
        Throws<NotSupportedException>(() => store.ReplaceAll(new Dictionary<string, string[]>()));
        Preserved(path, unsupported, backup);
    });
    Check("failed replace preserves main and removes temporary file", path =>
    {
        var store = new FileSystem(path);
        store.Set("a", "saved");
        string main = File.ReadAllText(path);
        File.Delete(path + ".bak");
        Directory.CreateDirectory(path + ".bak");
        Throws<IOException>(() => store.ReplaceAll(new Dictionary<string, string[]> { ["new"] = ["data"] }));
        Equal(main, File.ReadAllText(path));
        Equal(true, Directory.Exists(path + ".bak"));
        Sequence(["saved"], store.Get("a"));
        Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
    });
    Check("independent instances reload each operation", path =>
    {
        var first = new FileSystem(path);
        var second = new FileSystem(path);
        first.Set("first", "one");
        second.Set("second", "two");
        Equal(2, first.Count);
        Sequence(["two"], first.Get("second"));
        Equal(2, second.ReadAll().Count);
        Equal(true, second.Remove("first"));
        Sequence(["second"], first.Keys.ToArray());
        first.Clear();
        Equal(0, second.Count);
    });
    Check("lock collision rejects reads writes and constructors", path =>
    {
        var store = new FileSystem(path);
        using (var busy = File.Open(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Throws<IOException>(() => new FileSystem(path));
            Throws<IOException>(() => _ = store.Count);
            Throws<IOException>(() => _ = store.Keys);
            Throws<IOException>(() => store.Get("a"));
            Throws<IOException>(() => store.ReadAll());
            Throws<IOException>(() => store.Set("a", "value"));
            Throws<IOException>(() => store.Remove("a"));
            Throws<IOException>(() => store.Clear());
            Throws<IOException>(() => store.ReplaceAll(new Dictionary<string, string[]>()));
        }
        store.Set("after", "released");
        Equal(true, File.Exists(path + ".lock"));
        Sequence(["released"], store.Get("after"));
    });
}
finally
{
    Directory.Delete(root, recursive: true);
}
Console.WriteLine($"Storage checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

void Check(string name, Action<string> test)
{
    string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        test(Path.Combine(directory, "data.pva"));
        passed++;
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {exception}");
    }
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}
static void Sequence(string[] expected, string[] actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception("Expected [" + string.Join(", ", expected) + "], got [" + string.Join(", ", actual) + "].");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name + ".");
}
static void Preserved(string path, string main, string backup)
{
    Equal(main, File.ReadAllText(path));
    Equal(backup, File.ReadAllText(path + ".bak"));
}
