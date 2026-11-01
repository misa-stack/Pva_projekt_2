namespace projekt_pva_2;

/// <summary>Pravidla turnaje a změny ukládané před zveřejněním nového stavu.</summary>
public sealed class TournamentService
{
    private static readonly StringComparer Nicknames = StringComparer.OrdinalIgnoreCase;
    private TournamentState state;
    private readonly Action<TournamentState>? save;

    public TournamentService(TournamentState? state = null, Action<TournamentState>? save = null)
    {
        state ??= new TournamentState();
        Validate(state);
        this.state = state.Clone();
        this.save = save;
    }

    public TournamentState State => state.Clone();

    private static string Text(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{description} nesmí být prázdné.");
        return value.Trim();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Change(Action<TournamentState> change)
    {
        var candidate = state.Clone();
        change(candidate);
        Validate(candidate);
        save?.Invoke(candidate.Clone());
        state = candidate;
    }

    private static Tournament Current(TournamentState state)
        => state.CurrentTournament ?? throw new InvalidOperationException("Nejprve vytvořte turnaj.");

    private static Tournament BeforeStart(TournamentState state)
    {
        var tournament = Current(state);
        Require(tournament.Rounds.Count == 0, "Po zahájení turnaje již nelze měnit soupisku ani počet kol.");
        return tournament;
    }

    private static Player FindPlayer(TournamentState state, string nickname)
        => state.Players.FirstOrDefault(p => Nicknames.Equals(p.Nickname, nickname))
           ?? throw new ArgumentException("Hráč s touto přezdívkou neexistuje.");

    public void AddPlayer(string name, string nickname)
    {
        name = Text(name, "Jméno");
        nickname = Text(nickname, "Přezdívka");
        Change(s =>
        {
            Require(!s.Players.Any(p => Nicknames.Equals(p.Nickname, nickname)), "Tato přezdívka je již obsazená.");
            s.Players.Add(new Player(name, nickname));
        });
    }

    public void RemovePlayer(string nickname)
    {
        nickname = Text(nickname, "Přezdívka");
        Change(s =>
        {
            var player = FindPlayer(s, nickname);
            Require(!s.Tournaments.Any(t => t.Rounds.Count > 0 && t.Participants.Contains(player.Nickname, Nicknames)),
                "Hráče nelze smazat, protože je účastníkem již zahájeného turnaje.");
            foreach (var tournament in s.Tournaments)
                tournament.Participants.RemoveAll(n => Nicknames.Equals(n, player.Nickname));
            s.Players.Remove(player);
        });
    }

    public void CreateTournament(string name, string discipline, int numberOfRounds)
    {
        name = Text(name, "Název turnaje");
        discipline = Text(discipline, "Disciplína");
        Require(numberOfRounds > 0, "Počet kol musí být kladný.");
        Change(s =>
        {
            Require(s.CurrentTournament == null || s.CurrentTournament.IsFinished, "Nejprve dokončete rozehraný turnaj.");
            s.Tournaments.Add(new Tournament
            {
                Id = s.Tournaments.Count + 1,
                Name = name,
                Discipline = discipline,
                NumberOfRounds = numberOfRounds
            });
        });
    }

    public void RegisterPlayer(string nickname)
    {
        nickname = Text(nickname, "Přezdívka");
        Change(s =>
        {
            var tournament = BeforeStart(s);
            var player = FindPlayer(s, nickname);
            Require(!tournament.Participants.Contains(player.Nickname, Nicknames), "Hráč je již přihlášený.");
            tournament.Participants.Add(player.Nickname);
        });
    }

    public void UnregisterPlayer(string nickname)
    {
        nickname = Text(nickname, "Přezdívka");
        Change(s =>
        {
            var tournament = BeforeStart(s);
            var player = FindPlayer(s, nickname);
            Require(tournament.Participants.RemoveAll(n => Nicknames.Equals(n, player.Nickname)) > 0, "Hráč není přihlášený do turnaje.");
        });
    }

    public void SetRoundCount(int numberOfRounds)
    {
        Require(numberOfRounds > 0, "Počet kol musí být kladný.");
        Change(s => BeforeStart(s).NumberOfRounds = numberOfRounds);
    }

    private static int FullRoundCount(int playerCount) => playerCount % 2 == 0 ? playerCount - 1 : playerCount;

    // První účastník stojí; ostatní rotují. null je volno pro lichý počet hráčů.
    private static Round ScheduleRound(IReadOnlyList<string> participants, int number, int firstMatchId)
    {
        var circle = participants.Select(n => (string?)n).ToList();
        if (circle.Count % 2 == 1) circle.Add(null);
        for (int r = 1; r < number; r++)
        {
            var last = circle[^1];
            circle.RemoveAt(circle.Count - 1);
            circle.Insert(1, last);
        }
        var round = new Round { Number = number };
        for (int i = 0; i < circle.Count / 2; i++)
        {
            string? first = circle[i];
            string? second = circle[^(i + 1)];
            if (first == null || second == null)
                round.ByeNickname = first ?? second;
            else
                round.Matches.Add(new Match { Id = firstMatchId++, FirstNickname = first, SecondNickname = second });
        }
        return round;
    }

    public Round StartRound()
    {
        Change(s =>
        {
            var tournament = Current(s);
            Require(tournament.Participants.Count >= 2, "Pro zahájení turnaje jsou potřeba alespoň dva hráči.");
            int required = FullRoundCount(tournament.Participants.Count);
            Require(tournament.NumberOfRounds == required, $"Pro tento počet hráčů nastavte přesně {required} kol (každý s každým).");
            Require(!tournament.IsFinished, "Turnaj je již dokončený.");
            Require(tournament.Rounds.Count == 0 || tournament.Rounds[^1].IsClosed, "Nejprve uzavřete aktuální kolo.");
            int firstMatchId = tournament.Rounds.Sum(r => r.Matches.Count) + 1;
            tournament.Rounds.Add(ScheduleRound(tournament.Participants, tournament.Rounds.Count + 1, firstMatchId));
        });
        return TournamentState.CloneRound(state.CurrentTournament!.Rounds[^1]);
    }

    private static Round OpenRound(TournamentState state)
    {
        var tournament = Current(state);
        Require(tournament.Rounds.Count > 0 && !tournament.Rounds[^1].IsClosed, "Žádné kolo není otevřené.");
        return tournament.Rounds[^1];
    }

    public void SetResult(int matchId, MatchResult result)
    {
        Require(result is MatchResult.FirstWin or MatchResult.SecondWin or MatchResult.Draw, "Vyberte platný výsledek zápasu.");
        Change(s =>
        {
            var match = OpenRound(s).Matches.FirstOrDefault(m => m.Id == matchId)
                ?? throw new ArgumentException("Zápas v aktuálním otevřeném kole neexistuje.");
            match.Result = result;
        });
    }

    public void CloseRound() => Change(s =>
    {
        var round = OpenRound(s);
        Require(round.Matches.All(m => m.Result != MatchResult.Pending), "Nejprve vyplňte všechny výsledky kola.");
        round.IsClosed = true;
    });

    public IReadOnlyList<Standing> GetStandings()
    {
        var tournament = state.CurrentTournament;
        if (tournament == null) return Array.Empty<Standing>();
        var points = tournament.Participants.ToDictionary(n => n, _ => 0m, Nicknames);
        var played = tournament.Participants.ToDictionary(n => n, _ => 0, Nicknames);
        var wins = tournament.Participants.ToDictionary(n => n, _ => 0, Nicknames);
        var opponents = tournament.Participants.ToDictionary(n => n, _ => new List<string>(), Nicknames);
        foreach (var round in tournament.Rounds)
        {
            if (round.ByeNickname != null) points[round.ByeNickname] += 1m;
            foreach (var match in round.Matches.Where(m => m.Result != MatchResult.Pending))
            {
                string first = match.FirstNickname, second = match.SecondNickname;
                played[first]++; played[second]++;
                opponents[first].Add(second); opponents[second].Add(first);
                if (match.Result == MatchResult.Draw) { points[first] += .5m; points[second] += .5m; }
                else
                {
                    string winner = match.Result == MatchResult.FirstWin ? first : second;
                    points[winner] += 1m; wins[winner]++;
                }
            }
        }
        var players = state.Players.ToDictionary(p => p.Nickname, Nicknames);
        return tournament.Participants.Select(n => new Standing(0, players[n].Name, n, points[n], played[n], wins[n], opponents[n].Sum(o => points[o])))
            .OrderByDescending(s => s.Points).ThenByDescending(s => s.OpponentPoints).ThenByDescending(s => s.Wins)
            .ThenBy(s => s.Nickname, Nicknames).Select((s, index) => s with { Rank = index + 1 }).ToArray();
    }

    /// <summary>Ověří i rozpis a historii. Předaný stav nemění.</summary>
    public static void Validate(TournamentState state)
    {
        if (state == null) throw new ArgumentException("Stav turnaje chybí.");
        Require(state.Players != null && state.Tournaments != null, "Stav musí obsahovat seznam hráčů a turnajů.");
        var global = new HashSet<string>(Nicknames);
        foreach (var player in state.Players!)
        {
            Require(player != null, "Seznam obsahuje chybějícího hráče.");
            Text(player!.Name, "Jméno hráče");
            string nickname = Text(player.Nickname, "Přezdívka");
            Require(nickname == player.Nickname && global.Add(nickname), "Přezdívky musí být oříznuté a jedinečné.");
        }
        for (int i = 0; i < state.Tournaments!.Count; i++)
        {
            var tournament = state.Tournaments[i];
            Require(tournament != null, "Seznam obsahuje chybějící turnaj.");
            Require(tournament!.Id == i + 1, "ID turnajů musí navazovat od jedné.");
            Text(tournament.Name, "Název turnaje");
            Text(tournament.Discipline, "Disciplína");
            Require(tournament.NumberOfRounds > 0, "Počet kol musí být kladný.");
            Require(tournament.Participants != null && tournament.Rounds != null, "Turnaj musí obsahovat soupisku a seznam kol.");
            var participants = new HashSet<string>(Nicknames);
            foreach (string nickname in tournament.Participants!)
            {
                Require(nickname != null && global.Contains(nickname) && participants.Add(nickname), "Soupiska obsahuje neznámého nebo duplicitního hráče.");
            }
            var rounds = tournament.Rounds!;
            if (rounds.Count > 0)
            {
                Require(participants.Count >= 2, "Zahájený turnaj musí mít alespoň dva hráče.");
                Require(tournament.NumberOfRounds == FullRoundCount(participants.Count), "Počet kol neodpovídá úplnému turnaji každý s každým.");
                Require(rounds.Count <= tournament.NumberOfRounds, "Turnaj obsahuje příliš mnoho kol.");
            }
            int nextMatchId = 1;
            for (int r = 0; r < rounds.Count; r++)
            {
                var round = rounds[r];
                Require(round != null && round.Number == r + 1, "Kola musí být neprázdná a navazovat od jedné.");
                Require(round!.Matches != null, "Kolo musí obsahovat seznam zápasů.");
                Require(r == rounds.Count - 1 || round.IsClosed, "Předchozí kola musí být uzavřená.");
                var expected = ScheduleRound(tournament.Participants!, r + 1, nextMatchId);
                Require(Nicknames.Equals(round.ByeNickname, expected.ByeNickname), "Volno neodpovídá rozpisu turnaje.");
                Require(round.Matches!.Count == expected.Matches.Count, "Počet zápasů neodpovídá rozpisu turnaje.");
                for (int m = 0; m < round.Matches.Count; m++)
                {
                    var match = round.Matches[m];
                    var expectedMatch = expected.Matches[m];
                    Require(match != null && match.Id == nextMatchId++, "ID zápasů musí navazovat od jedné.");
                    Require(Nicknames.Equals(match!.FirstNickname, expectedMatch.FirstNickname)
                        && Nicknames.Equals(match.SecondNickname, expectedMatch.SecondNickname), "Zápas neodpovídá rozpisu turnaje.");
                    Require(match.Result is MatchResult.Pending or MatchResult.FirstWin or MatchResult.SecondWin or MatchResult.Draw, "Zápas obsahuje neplatný výsledek.");
                    Require(!round.IsClosed || match.Result != MatchResult.Pending, "Uzavřené kolo musí mít všechny výsledky.");
                }
            }
            Require(i == state.Tournaments.Count - 1 || tournament.IsFinished, "Všechny starší turnaje musí být dokončené.");
        }
    }
}
