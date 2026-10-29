using projekt_pva_2;

var root = Path.Combine(Path.GetTempPath(), "pva-integration-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
try
{
    Run("Complete state round trip and resume mid-round", () =>
    {
        var path = Path.Combine(root, "resume.pva");
        var store = new TournamentStore(path);
        var service = new TournamentService(store.Load(), store.Save);
        service.AddPlayer("Jméno | s\\lomítkem\na řádkem", "a");
        service.AddPlayer("Druhý", "b");
        service.AddPlayer("Třetí", "c");
        service.CreateTournament("Turnaj | test", "Piškvorky", 3);
        foreach (var nick in new[] { "a", "b", "c" }) service.RegisterPlayer(nick);
        var round = service.StartRound();
        service.SetResult(round.Matches[0].Id, MatchResult.Draw);
        var reopened = new TournamentStore(path);
        var resumed = new TournamentService(reopened.Load(), reopened.Save);
        Equal("Jméno | s\\lomítkem\na řádkem", resumed.State.Players[0].Name);
        Equal(MatchResult.Draw, resumed.State.CurrentTournament!.Rounds[0].Matches[0].Result);
        Equal(round.ByeNickname, resumed.State.CurrentTournament.Rounds[0].ByeNickname);
        resumed.SetResult(round.Matches[0].Id, MatchResult.FirstWin);
        resumed.CloseRound();
        while (!resumed.State.CurrentTournament!.IsFinished)
        {
            var next = resumed.StartRound();
            foreach (var match in next.Matches) resumed.SetResult(match.Id, MatchResult.Draw);
            resumed.CloseRound();
        }
        resumed.CreateTournament("Další", "Šachy", 1);
        var history = new TournamentStore(path).Load();
        Equal(2, history.Tournaments.Count);
        Check(history.Tournaments[0].IsFinished, "Completed history was lost.");
        Equal("Další", history.CurrentTournament!.Name);
    });
    Run("Invalid domain records stay untouched", () =>
    {
        var path = Path.Combine(root, "bad-record.pva");
        var file = new FileSystem(path);
        file.Set("state", "STATE", "1");
        file.Set("match/1/1", "MATCH", "1", "1", "1", "a", "b", "1");
        var bytes = File.ReadAllBytes(path);
        Throws<InvalidDataException>(() => new TournamentStore(path).Load());
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Invalid domain data was overwritten.");
    });
    Run("Unknown state versions stay untouched", () =>
    {
        var path = Path.Combine(root, "newer-state.pva");
        var file = new FileSystem(path);
        file.Set("state", "STATE", "2");
        var bytes = File.ReadAllBytes(path);
        Throws<NotSupportedException>(() => new TournamentStore(path).Load());
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Newer state was overwritten.");
    });
    Run("Record keys and field shapes are checked", () =>
    {
        var path = Path.Combine(root, "wrong-key.pva");
        var file = new FileSystem(path);
        file.Set("state", "STATE", "1");
        file.Set("player/other", "PLAYER", "Anna", "anna");
        Throws<InvalidDataException>(() => new TournamentStore(path).Load());
    });
    Run("Saving failure leaves in-memory state unchanged", () =>
    {
        var path = Path.Combine(root, "failed-write.pva");
        var store = new TournamentStore(path);
        var service = new TournamentService(store.Load(), store.Save);
        Directory.CreateDirectory(path + ".bak");
        Throws<IOException>(() => service.AddPlayer("Anna", "anna"));
        Equal(0, service.State.Players.Count);
        Equal(0, new TournamentStore(path).Load().Players.Count);
    });
    Run("Input retries text, blank and out-of-range numbers", () =>
    {
        var output = new StringWriter();
        var input = new InputReader(new StringReader("\nabc\n0\n4\n2\n"), output);
        Equal<int?>(2, input.ReadInt("Číslo: ", 1, 3));
        Equal<int?>(null, input.ReadInt("Číslo: ", 1, 3));
        Check(output.ToString().Contains("1") && output.ToString().Contains("3"), "Range was not explained.");
        Equal("Anna", new InputReader(new StringReader("  \n Anna \n"), output).ReadText("Jméno: "));
    });
    Run("A stale app instance cannot overwrite another app's changes", () =>
    {
        var path = Path.Combine(root, "two-writers.pva");
        var firstStore = new TournamentStore(path);
        var secondStore = new TournamentStore(path);
        var first = new TournamentService(firstStore.Load(), firstStore.Save);
        var second = new TournamentService(secondStore.Load(), secondStore.Save);
        first.AddPlayer("Anna", "anna");
        Throws<IOException>(() => second.AddPlayer("Boris", "boris"));
        Equal(0, second.State.Players.Count);
        var saved = new TournamentStore(path).Load();
        Equal(1, saved.Players.Count);
        Equal("anna", saved.Players[0].Nickname);
    });
    Run("Console can complete a tournament and resume", () =>
    {
        var path = Path.Combine(root, "console.pva");
        var output = new StringWriter();
        var commands = "abc\n2\nAnna\nanna\n2\nBoris\nboris\n4\nTest\nŠachy\n1\n5\nanna\n5\nboris\n8\n10\n999\n1\n10\n1\n3\n11\n12\n0\n";
        new TournamentConsole(path, new StringReader(commands), output).Run();
        Check(new TournamentStore(path).Load().CurrentTournament!.IsFinished, "Console did not complete the tournament.");
        Check(output.ToString().Contains("anna") && output.ToString().Contains("boris"), "Standings were not displayed.");
        var resumedOutput = new StringWriter();
        new TournamentConsole(path, new StringReader("12\n0\n"), resumedOutput).Run();
        Check(resumedOutput.ToString().Contains("Test"), "Console did not resume saved data.");
    });
    Run("Corrupt save permits choosing another file without a crash", () =>
    {
        var path = Path.Combine(root, "corrupt.pva");
        var alternative = Path.Combine(root, "alternative.pva");
        File.WriteAllText(path, "corrupt save");
        var output = new StringWriter();
        new TournamentConsole(path, new StringReader(alternative + "\n0\n"), output).Run();
        Equal("corrupt save", File.ReadAllText(path));
        Equal(0, new TournamentStore(alternative).Load().Players.Count);
    });
    Console.WriteLine($"{passed} integration checks passed.");
    return 0;
}
finally { Directory.Delete(root, recursive: true); }

void Run(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}, got {actual}.");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
