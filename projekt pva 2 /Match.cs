using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Threading;
public sealed class Player
{
    public required string name;
    public int score;
    private  string _password;
    public required string Password
    {
        set
        {

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            _password = Convert.ToBase64String(bytes);
        }
    }
    public record GameMatch(DateTime PlayedAt, int PlayerScore, int OpponentScore);
    private void CreatePassword()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine($"input password: ");
            string passwd = Console.ReadLine();
            if(passwd.Count(c => char.IsLetter(c)) > 0 && passwd.Count(c => char.IsDigit(c)) >= 4)
            {
                
            }
            
        
        }
    }
   
}


public class Match
{
    private int _rounds;
    private static readonly object _globalLock = new object();
    public string Discipline;
    private List<Player> _players;

    public int Rounds
    {
        get { return _rounds; }
        set
        {
            if (value > 0)
            {
                _rounds = value;
            }
            else
            {
                // ArgumentOutOfRangeException is perfect for numbers that are too low/high
                throw new ArgumentOutOfRangeException("value", "Rounds must be greater than 0.");
            }
        }
    }
    private Match(string Discipline, int Rounds)
    {
        this.Discipline = Discipline;
        this.Rounds = Rounds;
    }
    public static Match TryCreate(string Discipline, int Rounds)
    {
        bool lockIsUnlocked = Monitor.TryEnter(_globalLock, TimeSpan.Zero);
        
        if (!lockIsUnlocked)
        {
            return null; // Lock is busy, reject creation
        }
        try
        {

            return new Match(Discipline, Rounds);
        }
        finally
        {
            Monitor.Exit(_globalLock);
        }
    }

}