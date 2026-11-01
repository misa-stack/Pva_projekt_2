// Napsala AI (Codex) pro rychlejší hledání chyb; nenahrazuje ruční přezkoušení týmem.
using System.Globalization;
using System.Text;
using projekt_pva_2;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Reject(Action action, string message)
{
    checks++;
    try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
    throw new Exception("Mělo být odmítnuto: " + message);
}
TournamentService Setup(int n, int? rounds = null)
{
    var service = new TournamentService();
    for (int i = 0; i < n; i++) service.AddPlayer("Player " + i, "P" + i);
    service.CreateTournament("Turnaj", "Šachy", rounds ?? (n % 2 == 0 ? Math.Max(1, n - 1) : n));
    for (int i = 0; i < n; i++) service.RegisterPlayer("P" + i);
    return service;
}
void Finish(TournamentService service)
{
    foreach (var match in service.State.CurrentTournament!.Rounds.Last().Matches)
        service.SetResult(match.Id, MatchResult.Draw);
    service.CloseRound();
}
string Snapshot(TournamentService service) => SnapshotState(service.State);
// Nezávislý otisk hodnot pro porovnání před/po operaci, bez JSON/XML a bez ukládání.
string SnapshotState(TournamentState state)
{
    var result = new StringBuilder();
    void Add(object? value)
    {
        if (value is null) { result.Append("null;"); return; }
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)!;
        result.Append(text.Length).Append(':').Append(text).Append(';');
    }
    Add(state.Players.Count);
    foreach (var player in state.Players) { Add(player.Name); Add(player.Nickname); }
    Add(state.Tournaments.Count);
    foreach (var tournament in state.Tournaments)
    {
        Add(tournament.Id); Add(tournament.Name); Add(tournament.Discipline); Add(tournament.NumberOfRounds);
        Add(tournament.Participants.Count);
        foreach (string nickname in tournament.Participants) Add(nickname);
        Add(tournament.Rounds.Count);
        foreach (var round in tournament.Rounds)
        {
            Add(round.Number); Add(round.IsClosed); Add(round.ByeNickname); Add(round.Matches.Count);
            foreach (var match in round.Matches)
            {
                Add(match.Id); Add(match.FirstNickname); Add(match.SecondNickname); Add((int)match.Result);
            }
        }
    }
    return result.ToString();
}

var players = new TournamentService();
players.AddPlayer(" Alice ", " AL ");
Check(players.State.Players[0] == new Player("Alice", "AL"), "Vstupy se oříznou.");
Reject(() => players.AddPlayer("Other", "al"), "duplicita bez ohledu na velikost písmen");
Reject(() => players.AddPlayer(" ", "B"), "prázdné jméno");
Reject(() => players.AddPlayer("B", " "), "prázdná přezdívka");
Reject(() => players.RemovePlayer("missing"), "neexistující hráč");
players.CreateTournament(" T ", " D ", 1);
Reject(() => players.CreateTournament("Další", "D", 1), "nedokončený turnaj");
Reject(() => players.RegisterPlayer("missing"), "registrace neexistujícího hráče");
players.RegisterPlayer("al");
Reject(() => players.RegisterPlayer("AL"), "duplicitní registrace");
Reject(() => players.UnregisterPlayer("missing"), "odregistrace neexistujícího hráče");
players.UnregisterPlayer("al");
Reject(() => players.UnregisterPlayer("al"), "odregistrace nepřihlášeného hráče");
players.RegisterPlayer("AL");
players.RemovePlayer("al");
Check(players.State.CurrentTournament!.Participants.Count == 0 && players.State.Players.Count == 0, "Smazání před startem odstraní registraci.");
Reject(() => players.SetRoundCount(0), "nulový počet kol");
Reject(() => new TournamentService().CreateTournament("", "D", 1), "prázdný turnaj");
Reject(() => new TournamentService().CreateTournament("T", "", 1), "prázdná disciplína");
foreach (int n in new[] { 0, 1 })
{
    var service = Setup(n);
    string before = Snapshot(service);
    Reject(() => service.StartRound(), "méně než dva hráči");
    Check(Snapshot(service) == before, "Odmítnutý start nesmí změnit stav.");
}
var wrongRounds = Setup(3, 2);
Reject(() => wrongRounds.StartRound(), "vyžadovat úplný každý s každým");
wrongRounds.SetRoundCount(3);
Check(wrongRounds.StartRound().ByeNickname == "P0", "Deterministický první volný hráč.");
Reject(() => wrongRounds.SetRoundCount(4), "změna počtu kol po startu");
Reject(() => wrongRounds.StartRound(), "otevřené kolo");
Reject(() => wrongRounds.RegisterPlayer("P0"), "uzamčená soupiska");
Reject(() => wrongRounds.UnregisterPlayer("P0"), "uzamčená soupiska");
Reject(() => wrongRounds.RemovePlayer("P0"), "smazání volného hráče po startu");
Reject(() => wrongRounds.CloseRound(), "neúplné výsledky");
Reject(() => wrongRounds.SetResult(999, MatchResult.Draw), "neexistující zápas");
Reject(() => wrongRounds.SetResult(1, MatchResult.Pending), "nevyplněný výsledek");
Reject(() => wrongRounds.SetResult(1, (MatchResult)42), "neznámý výsledek");
wrongRounds.SetResult(1, MatchResult.FirstWin);
wrongRounds.SetResult(1, MatchResult.SecondWin);
Check(wrongRounds.GetStandings().Single(x => x.Nickname == "P2").Wins == 1, "Oprava výsledku před zavřením.");
wrongRounds.CloseRound();
Reject(() => wrongRounds.SetResult(1, MatchResult.Draw), "oprava uzavřeného kola");
Reject(() => wrongRounds.CloseRound(), "opakované uzavření");

for (int n = 2; n <= 15; n++)
{
    var service = Setup(n);
    var pairs = new HashSet<string>();
    var byes = new Dictionary<string, int>();
    int nextId = 1;
    int rounds = n % 2 == 0 ? n - 1 : n;
    for (int r = 1; r <= rounds; r++)
    {
        var round = service.StartRound();
        Check(round.Number == r, "Pořadí kol.");
        var appearances = new HashSet<string>();
        if (round.ByeNickname != null)
        {
            appearances.Add(round.ByeNickname);
            byes[round.ByeNickname] = byes.GetValueOrDefault(round.ByeNickname) + 1;
        }
        foreach (var match in round.Matches)
        {
            Check(match.Id == nextId++, "Souvislá ID zápasů.");
            Check(appearances.Add(match.FirstNickname) && appearances.Add(match.SecondNickname), "Jeden výskyt za kolo.");
            Check(pairs.Add(string.Join("/", new[] { match.FirstNickname, match.SecondNickname }.Order())), "Dvojice se neopakuje.");
        }
        Check(appearances.Count == n, "Všichni hráči mají zápas nebo volno.");
        Finish(service);
        TournamentService.Validate(service.State);
    }
    Check(pairs.Count == n * (n - 1) / 2, "Všechny dvojice právě jednou.");
    Check(n % 2 == 0 ? byes.Count == 0 : byes.Count == n && byes.Values.All(x => x == 1), "Spravedlivé volno.");
    Check(service.State.CurrentTournament!.IsFinished, "Turnaj je dokončený.");
    Check(service.GetStandings().All(x => x.Played == n - 1 && x.Points == (n - 1) * .5m + (n % 2)), "Remízy a volno se počítají správně.");
    Reject(() => service.StartRound(), "kolo po dokončení");
    Reject(() => service.RemovePlayer("P0"), "historická soupiska chráněna");
    service.CreateTournament("Druhý", "Go", 1);
    Check(service.State.Tournaments.Count == 2 && service.State.CurrentTournament!.Id == 2, "Historie a ID dalšího turnaje.");
}

var scoring = Setup(3);
scoring.StartRound(); // P0 bye, P1 vs P2
scoring.SetResult(1, MatchResult.FirstWin);
var table = scoring.GetStandings();
Check(table.Select(x => x.Nickname).SequenceEqual(new[] { "P1", "P0", "P2" }), "Při shodě bodů i soupeřů rozhoduje počet výher.");
Check(table.Single(x => x.Nickname == "P0") is { Points: 1m, Played: 0, Wins: 0, OpponentPoints: 0m }, "Volno není zápas ani výhra.");
scoring.CloseRound();
scoring.StartRound(); // P0 vs P2, P1 bye
scoring.SetResult(2, MatchResult.SecondWin);
scoring.CloseRound();
scoring.StartRound(); // P0 vs P1, P2 bye
scoring.SetResult(3, MatchResult.FirstWin);
table = scoring.GetStandings();
Check(table.Select(x => x.Nickname).SequenceEqual(new[] { "P0", "P1", "P2" }), "Trojitá rovnost rozhodnuta přezdívkou.");
Check(table.All(x => x.Points == 2m && x.Wins == 1 && x.Played == 2 && x.OpponentPoints == 4m), "Ruční trojčlenná tabulka: dva body, dvě hraní, jedna výhra a soupeři 4.");
Check(table.Select(x => x.Rank).SequenceEqual(new[] { 1, 2, 3 }), "Pořadí tabulky.");

var tie = Setup(4);
tie.StartRound(); // P0-P3, P1-P2
tie.SetResult(1, MatchResult.FirstWin);
tie.SetResult(2, MatchResult.Draw);
table = tie.GetStandings();
Check(table.Single(x => x.Nickname == "P1") is { Points: .5m, Played: 1, Wins: 0, OpponentPoints: .5m }, "Remíza a síla soupeře.");
Check(table.Single(x => x.Nickname == "P3") is { Points: 0m, Played: 1, Wins: 0, OpponentPoints: 1m }, "Prohra a síla soupeře.");
tie.CloseRound();
tie.StartRound(); // P0-P2, P3-P1
tie.SetResult(3, MatchResult.SecondWin);
Check(tie.GetStandings().First().Nickname == "P2", "Započítat výsledky z otevřeného kola.");

var original = Setup(2);
var source = original.State;
var copy = new TournamentService(source);
source.Players.Clear(); source.Tournaments[0].Participants.Clear();
Check(copy.State.Players.Count == 2 && copy.State.CurrentTournament!.Participants.Count == 2, "Vstupní stav je izolovaný.");
var exposed = copy.State; exposed.Players.Clear(); exposed.Tournaments.Clear();
var exposedRound = copy.StartRound(); exposedRound.Matches.Clear();
Check(copy.State.Players.Count == 2 && copy.State.CurrentTournament!.Rounds[0].Matches.Count == 1, "Výstupní stav a kolo jsou izolované.");
var cloned = copy.State.Clone(); cloned.CurrentTournament!.Rounds[0].Matches[0].Result = MatchResult.Draw;
Check(copy.State.CurrentTournament!.Rounds[0].Matches[0].Result == MatchResult.Pending, "Clone je hluboký.");
int saves = 0;
TournamentService? saving = null;
saving = new TournamentService(save: state =>
{
    Check(saving!.State.Players.Count == 0, "Uložení probíhá před zveřejněním změny.");
    state.Players.Clear(); saves++;
});
saving.AddPlayer("A", "A");
Check(saves == 1 && saving.State.Players.Count == 1, "Callback nemůže změnit zveřejněný stav.");
var failing = new TournamentService(copy.State, _ => throw new IOException("Disk full"));
string rollback = Snapshot(failing);
try { failing.SetResult(1, MatchResult.FirstWin); throw new Exception("Chybí chyba uložení."); } catch (IOException) { }
Check(Snapshot(failing) == rollback, "Chyba uložení zachová původní stav.");

void Invalid(Action<TournamentState> corrupt, string message)
{
    var state = Setup(3).State;
    corrupt(state);
    Reject(() => TournamentService.Validate(state), message);
    Reject(() => new TournamentService(state), "konstruktor: " + message);
}
Invalid(s => s.Players = null!, "null seznam hráčů");
Invalid(s => s.Players[0] = null!, "null hráč");
Invalid(s => s.Players.Add(new Player("X", "p0")), "duplicitní přezdívka");
Invalid(s => s.Players[0] = new Player("", "P0"), "prázdné jméno hráče");
Invalid(s => s.Players[0] = new Player("X", " P0 "), "neoříznutá přezdívka");
Invalid(s => s.Tournaments = null!, "null seznam turnajů");
Invalid(s => s.Tournaments[0] = null!, "null turnaj");
Invalid(s => s.Tournaments[0].Id = 3, "mezera v ID");
Invalid(s => s.Tournaments[0].Name = " ", "prázdný název");
Invalid(s => s.Tournaments[0].Discipline = null!, "null disciplína");
Invalid(s => s.Tournaments[0].NumberOfRounds = 0, "nulová kola");
Invalid(s => s.Tournaments[0].Participants = null!, "null soupiska");
Invalid(s => s.Tournaments[0].Participants.Add("missing"), "neznámý účastník");
Invalid(s => s.Tournaments[0].Participants.Add("p0"), "duplicitní účastník");
Invalid(s => s.Tournaments[0].Rounds = null!, "null kola");
Invalid(s => s.Tournaments.Add(new Tournament { Id = 2, Name = "T", Discipline = "D", NumberOfRounds = 1 }), "starý nedokončený turnaj");
void InvalidStarted(Action<Tournament> corrupt, string message)
{
    var service = Setup(3); service.StartRound();
    var state = service.State; corrupt(state.CurrentTournament!);
    Reject(() => TournamentService.Validate(state), message);
}
InvalidStarted(t => t.NumberOfRounds = 2, "špatný celkový počet kol");
InvalidStarted(t => t.Participants.Reverse(), "změněná soupiska");
InvalidStarted(t => t.Rounds[0] = null!, "null kolo");
InvalidStarted(t => t.Rounds[0].Number = 2, "špatné číslo kola");
InvalidStarted(t => t.Rounds[0].Matches = null!, "null zápasy");
InvalidStarted(t => t.Rounds[0].Matches[0] = null!, "null zápas");
InvalidStarted(t => t.Rounds[0].Matches[0].Id = 2, "špatné ID zápasu");
InvalidStarted(t => t.Rounds[0].Matches[0].Result = (MatchResult)7, "neplatný enum");
InvalidStarted(t => t.Rounds[0].IsClosed = true, "uzavřené neúplné kolo");
InvalidStarted(t => t.Rounds[0].ByeNickname = "P1", "špatné volno");
InvalidStarted(t => t.Rounds[0].Matches[0].FirstNickname = "P0", "nesprávný rozpis");
InvalidStarted(t => t.Rounds[0].Matches[0].FirstNickname = "P2", "zápas sám se sebou");
InvalidStarted(t => t.Rounds.Add(t.Rounds[0]), "opakované otevřené kolo");
var valid = scoring.State;
string validBefore = SnapshotState(valid);
TournamentService.Validate(valid);
Check(SnapshotState(valid) == validBefore, "Validate nemění vstup.");
// Rovnost bodů s různou silou soupeřů, odvozená ručně z uvedených zápasů.
tie.SetResult(4, MatchResult.FirstWin);
table = tie.GetStandings();
Check(table.Select(x => x.Nickname).SequenceEqual(new[] { "P2", "P0", "P3", "P1" }), "Při shodě bodů rozhodují soupeři.");
Check(table.Single(x => x.Nickname == "P0").OpponentPoints == 2.5m && table.Single(x => x.Nickname == "P3").OpponentPoints == 1.5m, "Síla soupeřů používá jejich aktuální celkové body.");
Reject(() => tie.SetResult(1, MatchResult.Draw), "starý zápas v novém otevřeném kole");
Invalid(s => s.Players[0] = new Player("X", null!), "null přezdívka hráče");
Invalid(s => s.Tournaments[0].Participants[0] = null!, "null přezdívka účastníka");
InvalidStarted(t => t.Rounds[0].Matches[0].SecondNickname = null!, "null soupeř");
InvalidStarted(t => t.Rounds[0].Matches[0].SecondNickname = "missing", "osiřelý soupeř");
InvalidStarted(t => t.Rounds[0].Matches.Clear(), "chybějící zápas");
InvalidStarted(t => t.Rounds[0].Matches.Add(t.Rounds[0].Matches[0]), "nadbytečný opakovaný zápas");
Reject(() => TournamentService.Validate(null!), "null kořen stavu");
var empty = new TournamentService();
Check(empty.GetStandings().Count == 0, "Bez turnaje je tabulka prázdná.");
Reject(() => empty.StartRound(), "start bez turnaje");
Reject(() => empty.SetRoundCount(1), "počet kol bez turnaje");
Reject(() => empty.SetResult(1, MatchResult.Draw), "výsledek bez turnaje");
Reject(() => empty.CloseRound(), "uzavření bez turnaje");
Reject(() => empty.RegisterPlayer("P0"), "registrace bez turnaje");
Reject(() => empty.UnregisterPlayer("P0"), "odregistrace bez turnaje");
Console.WriteLine($"PASS: {checks} tournament assertions");
