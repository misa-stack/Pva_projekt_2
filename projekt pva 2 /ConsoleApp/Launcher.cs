namespace projekt_pva_2;

public static class Launcher
{
    public static string SavePath { get; private set; } = Path.Combine(AppContext.BaseDirectory, "data.pva");

    public static bool TryRunText(string[] args)
    {
        bool textMode = Console.IsInputRedirected;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "--text") textMode = true;
            else if (args[index] == "--save" && index + 1 < args.Length) SavePath = args[++index];
            else
            {
                Console.WriteLine("Použití: program [--text] [--save cesta.pva]");
                Environment.ExitCode = args[index] is "--help" or "-h" ? 0 : 2;
                return true;
            }
        }
        if (!textMode) return false;
        new TournamentConsole(SavePath).Run();
        return true;
    }
}
