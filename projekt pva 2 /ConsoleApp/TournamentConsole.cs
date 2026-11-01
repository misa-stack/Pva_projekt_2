namespace projekt_pva_2;

public sealed class TournamentConsole
{
    private readonly TextWriter output;
    private readonly InputReader input;
    private string savePath;

    public TournamentConsole(string savePath, TextReader? input = null, TextWriter? output = null)
    {
        this.savePath = savePath;
        this.output = output ?? Console.Out;
        this.input = new InputReader(input ?? Console.In, this.output);
    }

    public void Run()
    {
        Console.Clear();
        var service = Open();
        if (service is null) return;
        while (true){
    
string BuildPrompt()
{
    var t = service.State.CurrentTournament;   // always read fresh, never cache
    return $"Turnaj: {t.Name} | {t.Discipline} | kola {t.Rounds.Count}/{t.NumberOfRounds} | {(t.IsFinished ? "dokončen" : "rozpracován")}\n";
}

            string[] options = {
    "Hráči", "Přidat hráče", "Odebrat hráče", "Založit turnaj",
    "Přihlásit", "Odhlásit", "Účastníci", "Spustit kolo",
    "Zápasy", "Zadat/opravit výsledek", "Uzavřít kolo", "Tabulka",
    "Historie turnajů", "Upravit počet kol", "Znovu načíst", "Zpět / konec"
};
            UI.Menu StartMenu = new UI.Menu(BuildPrompt, options);
            int? choice = StartMenu.Run() + 1;
            if (choice is null or 0) return;
            if (choice == 15)
            {
                service = Open();
                if (service is null) return;
                continue;
            }
            try { Execute(choice.Value, service); }
            catch (EndOfStreamException) { return; }
            catch (Exception error) when (IsExpected(error))
            {
                output.WriteLine("Chyba: " + error.Message);
            }
        }
    }

    private TournamentService? Open()
    {
        while (true)
        {
            try
            {
                var store = new TournamentStore(savePath);
                var service = new TournamentService(store.Load(), store.Save);
                output.WriteLine("Ukládání: " + store.FilePath);
                if (store.RecoveredFromBackup) output.WriteLine("Byl obnoven předchozí uložený stav ze zálohy.");
                return service;
            }
            catch (Exception error) when (IsExpected(error))
            {
                output.WriteLine("Soubor nelze otevřít: " + error.Message);
                output.WriteLine("Zadejte jinou cestu, nebo 0 pro návrat. Původní soubor zůstane zachovaný.");
                string? path = input.ReadText("Cesta: ");
                if (path is null or "0") return null;
                savePath = path;
            }
        }
    }

    private void Execute(int choice, TournamentService service)
    {
        switch (choice)
        {
            case 1:
                Console.Clear();
                var players = service.State.Players;
                if (players.Count == 0) output.WriteLine("Zatím nejsou evidováni žádní hráči.");
                foreach (var player in players) output.WriteLine($"{player.Nickname}: {player.Name}");
                Console.ReadKey();
                break;
            case 2:
                string name = Text("Jméno: ");
                string nickname = Text("Jedinečná přezdívka: ");
                service.AddPlayer(name, nickname);
                Saved("Hráč byl přidán.");
                Console.ReadKey();
                break;
            case 3:
                service.RemovePlayer(Text("Přezdívka hráče: "));
                Saved("Hráč byl odebrán.");
                Console.ReadKey();
                break;
            case 4:
                string tournamentName = Text("Název turnaje: ");
                string discipline = Text("Disciplína: ");
                int count = Number("Počet kol: ");
                service.CreateTournament(tournamentName, discipline, count);
                Saved("Turnaj byl založen. Úplný rozpis pro sudý počet hráčů má n−1 kol, pro lichý n kol.");
                Console.ReadKey();
                break;
            case 5:
                service.RegisterPlayer(Text("Přezdívka evidovaného hráče: "));
                Saved("Hráč byl přihlášen.");
                Console.ReadKey();
                break;
            case 6:
                service.UnregisterPlayer(Text("Přezdívka hráče: "));
                Saved("Hráč byl odhlášen.");
                Console.ReadKey();
                break;
            case 7:
                var tournament = Current(service);
                if (tournament.Participants.Count == 0) output.WriteLine("Zatím není nikdo přihlášen.");
                foreach (string participant in tournament.Participants) output.WriteLine(participant);
                Console.ReadKey();
                break;
            case 8:
                var round = service.StartRound();
                Saved($"Kolo {round.Number} bylo spuštěno.");
                PrintRound(round);
                Console.ReadKey();
                break;
            case 9:
                var active = Current(service).Rounds.LastOrDefault();
                if (active is null) output.WriteLine("Zatím nebylo spuštěno žádné kolo.");
                else PrintRound(active);
                Console.ReadKey();
                break;
            case 10:
                int id = Number("Číslo zápasu: ");
                output.WriteLine("1 Výhra prvního hráče | 2 Výhra druhého hráče | 3 Remíza");
                int result = Number("Výsledek: ", 1, 3);
                service.SetResult(id, (MatchResult)result);
                Saved("Výsledek byl zapsán. Tabulka je přepočítaná.");
                Console.ReadKey();
                break;
            case 11:
                service.CloseRound();
                Saved("Kolo bylo uzavřeno.");
                Console.ReadKey();
                break;
            case 12:
                output.WriteLine("Pořadí | Přezdívka | Hráč | Body | Odehráno | Body soupeřů | Výhry");
                foreach (var row in service.GetStandings())
                    output.WriteLine($"{row.Rank} | {row.Nickname} | {row.Name} | {row.Points} | {row.Played} | {row.OpponentPoints} | {row.Wins}");
                Console.ReadKey();
                break;
            case 13:
                foreach (var item in service.State.Tournaments)
                    output.WriteLine($"{item.Id}: {item.Name}, {item.Discipline}, {(item.IsFinished ? "dokončen" : "rozpracován")}");
                Console.ReadKey();
                break;
            case 14:
                service.SetRoundCount(Number("Nový počet kol: "));
                Saved("Počet kol byl upraven.");
                Console.ReadKey();
                break;
        }
    }

    private void PrintRound(Round round)
    {
        output.WriteLine($"Kolo {round.Number} ({(round.IsClosed ? "uzavřené" : "otevřené")})");
        foreach (var match in round.Matches)
        {
            string result = match.Result switch
            {
                MatchResult.Pending => "čeká na výsledek", MatchResult.FirstWin => "výhra prvního",
                MatchResult.SecondWin => "výhra druhého", MatchResult.Draw => "remíza", _ => "neplatný výsledek"
            };
            output.WriteLine($"{match.Id}: {match.FirstNickname} – {match.SecondNickname}: {result}");
        }
        if (round.ByeNickname is not null) output.WriteLine($"Volný los: {round.ByeNickname} (+1 bod, nepočítá se jako odehraný zápas).");
    }

    private string Text(string prompt) => input.ReadText(prompt) ?? throw new EndOfStreamException();
    private int Number(string prompt, int minimum = 1, int maximum = int.MaxValue) =>
        input.ReadInt(prompt, minimum, maximum) ?? throw new EndOfStreamException();
    private void Saved(string message) => output.WriteLine(message + " Automaticky uloženo.");
    private static Tournament Current(TournamentService service) => service.State.CurrentTournament
        ?? throw new InvalidOperationException("Nejdřív založte turnaj.");
    private static bool IsExpected(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or NotSupportedException
        or System.Security.SecurityException;
}
