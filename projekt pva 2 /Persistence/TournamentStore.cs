using System.Globalization;

namespace projekt_pva_2;

public sealed class TournamentStore
{
    private readonly FileSystem file;
    private Dictionary<string, string[]> baseline = [];
    public string FilePath => file.FilePath;
    public bool RecoveredFromBackup => file.RecoveredFromBackup;

    public TournamentStore(string path)
    {
        file = new FileSystem(path);
        Load();
    }

    public TournamentState Load()
    {
        var records = file.ReadAll();
        if (records.Count == 0)
        {
            baseline = records;
            return new TournamentState();
        }
        if (!records.TryGetValue("state", out var header) || header.Length != 2 || header[0] != "STATE")
            throw new InvalidDataException("Soubor neobsahuje hlavičku stavu turnajů.");
        if (header[1] != "1")
            throw new NotSupportedException($"Verze stavu {header[1]} není podporována.");

        var state = new TournamentState();
        var tournaments = new Dictionary<int, Tournament>();
        var rounds = new Dictionary<(int Tournament, int Number), Round>();
        var rows = records.Where(pair => pair.Key != "state").ToArray();
        foreach (var row in rows)
        {
            var fields = row.Value;
            int count = fields.Length == 0 ? -1 : fields[0] switch
            {
                "PLAYER" => 3, "TOURNAMENT" => 5, "PARTICIPANT" => 4,
                "ROUND" => 5, "MATCH" => 7, _ => -1
            };
            if (fields.Length != count) throw Invalid(row.Key);
        }

        try
        {
            foreach (var row in rows.Where(row => row.Value[0] == "PLAYER"))
            {
                ExpectKey(row.Key, "player/" + row.Value[2]);
                state.Players.Add(new Player(row.Value[1], row.Value[2]));
            }
            foreach (var row in rows.Where(row => row.Value[0] == "TOURNAMENT"))
            {
                var fields = row.Value;
                int id = Number(fields[1]);
                ExpectKey(row.Key, $"tournament/{id}");
                tournaments.Add(id, new Tournament
                {
                    Id = id, Name = fields[2], Discipline = fields[3], NumberOfRounds = Number(fields[4])
                });
            }
            state.Tournaments.AddRange(tournaments.Values.OrderBy(t => t.Id));
            foreach (var row in rows.Where(row => row.Value[0] == "PARTICIPANT")
                         .OrderBy(row => Number(row.Value[1])).ThenBy(row => Number(row.Value[2], allowZero: true)))
            {
                var fields = row.Value;
                int id = Number(fields[1]);
                int index = Number(fields[2], allowZero: true);
                ExpectKey(row.Key, $"participant/{id}/{index}");
                var tournament = FindTournament(id);
                if (index != tournament.Participants.Count) throw Invalid(row.Key);
                tournament.Participants.Add(fields[3]);
            }
            foreach (var row in rows.Where(row => row.Value[0] == "ROUND")
                         .OrderBy(row => Number(row.Value[1])).ThenBy(row => Number(row.Value[2])))
            {
                var fields = row.Value;
                int id = Number(fields[1]);
                int number = Number(fields[2]);
                ExpectKey(row.Key, $"round/{id}/{number}");
                var round = new Round
                {
                    Number = number,
                    IsClosed = fields[3] switch { "0" => false, "1" => true, _ => throw Invalid(row.Key) },
                    ByeNickname = fields[4].Length == 0 ? null : fields[4]
                };
                rounds.Add((id, number), round);
                FindTournament(id).Rounds.Add(round);
            }
            foreach (var row in rows.Where(row => row.Value[0] == "MATCH")
                         .OrderBy(row => Number(row.Value[1])).ThenBy(row => Number(row.Value[3])))
            {
                var fields = row.Value;
                int tournamentId = Number(fields[1]);
                int roundNumber = Number(fields[2]);
                int matchId = Number(fields[3]);
                ExpectKey(row.Key, $"match/{tournamentId}/{matchId}");
                if (!rounds.TryGetValue((tournamentId, roundNumber), out var round)) throw Invalid(row.Key);
                round.Matches.Add(new Match
                {
                    Id = matchId, FirstNickname = fields[4], SecondNickname = fields[5],
                    Result = (MatchResult)Number(fields[6], allowZero: true)
                });
            }
            TournamentService.Validate(state);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Uložený stav turnaje je neplatný: " + error.Message, error);
        }
        baseline = records;
        return state;

        Tournament FindTournament(int id) => tournaments.TryGetValue(id, out var value)
            ? value : throw new InvalidDataException($"Záznam odkazuje na neexistující turnaj {id}.");
    }

    public void Save(TournamentState state)
    {
        TournamentService.Validate(state);
        var records = new Dictionary<string, string[]> { ["state"] = ["STATE", "1"] };
        foreach (var player in state.Players)
            records.Add("player/" + player.Nickname, ["PLAYER", player.Name, player.Nickname]);
        foreach (var tournament in state.Tournaments)
        {
            string id = Text(tournament.Id);
            records.Add($"tournament/{id}", ["TOURNAMENT", id, tournament.Name, tournament.Discipline, Text(tournament.NumberOfRounds)]);
            for (int index = 0; index < tournament.Participants.Count; index++)
                records.Add($"participant/{id}/{index}", ["PARTICIPANT", id, Text(index), tournament.Participants[index]]);
            foreach (var round in tournament.Rounds)
            {
                string number = Text(round.Number);
                records.Add($"round/{id}/{number}", ["ROUND", id, number, round.IsClosed ? "1" : "0", round.ByeNickname ?? ""]);
                foreach (var match in round.Matches)
                    records.Add($"match/{id}/{match.Id}", ["MATCH", id, number, Text(match.Id), match.FirstNickname, match.SecondNickname, Text((int)match.Result)]);
            }
        }
        file.ReplaceAll(records, baseline);
        baseline = records;
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
    private static int Number(string text, bool allowZero = false) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= (allowZero ? 0 : 1)
            ? number : throw new InvalidDataException($"Neplatné celé číslo v uloženém stavu: '{text}'.");
    private static void ExpectKey(string actual, string expected)
    {
        if (actual != expected) throw Invalid(actual);
    }
    private static InvalidDataException Invalid(string key) => new($"Neplatný záznam '{key}'.");
}
