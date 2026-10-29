namespace projekt_pva_2;

public enum MatchResult { Pending = 0, FirstWin = 1, SecondWin = 2, Draw = 3 }
public record Player(string Name, string Nickname);
public sealed class Match
{
    public int Id { get; set; }
    public string FirstNickname { get; set; } = "";
    public string SecondNickname { get; set; } = "";
    public MatchResult Result { get; set; }
}
public sealed class Round
{
    public int Number { get; set; }
    public bool IsClosed { get; set; }
    public string? ByeNickname { get; set; }
    public List<Match> Matches { get; set; } = [];
}
public sealed class Tournament
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Discipline { get; set; } = "";
    public int NumberOfRounds { get; set; }
    public List<string> Participants { get; set; } = [];
    public List<Round> Rounds { get; set; } = [];
    public bool IsFinished => Rounds.Count == NumberOfRounds && Rounds.All(r => r.IsClosed);
}
public sealed class TournamentState
{
    public List<Player> Players { get; set; } = [];
    public List<Tournament> Tournaments { get; set; } = [];
    public Tournament? CurrentTournament => Tournaments.LastOrDefault();
    public TournamentState Clone() => new()
    {
        Players = Players.Select(p => new Player(p.Name, p.Nickname)).ToList(),
        Tournaments = Tournaments.Select(t => new Tournament
        {
            Id = t.Id,
            Name = t.Name,
            Discipline = t.Discipline,
            NumberOfRounds = t.NumberOfRounds,
            Participants = [.. t.Participants],
            Rounds = t.Rounds.Select(CloneRound).ToList()
        }).ToList()
    };

    internal static Round CloneRound(Round round) => new()
    {
        Number = round.Number,
        IsClosed = round.IsClosed,
        ByeNickname = round.ByeNickname,
        Matches = round.Matches.Select(m => new Match
        {
            Id = m.Id,
            FirstNickname = m.FirstNickname,
            SecondNickname = m.SecondNickname,
            Result = m.Result
        }).ToList()
    };
}
public record Standing(int Rank, string Name, string Nickname, decimal Points, int Played, int Wins, decimal OpponentPoints);
