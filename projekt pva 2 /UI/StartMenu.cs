namespace StartMenu;

using UI;
class Program
{
    static void Main(string[] args)
    {
        if (projekt_pva_2.Launcher.TryRunText(args)) return;
        MenuFunctions menu = new MenuFunctions();
        string prompt = @"
████████╗██╗███╗   ██╗██████╗ ███████╗██████╗ 
╚══██╔══╝██║████╗  ██║██╔══██╗██╔════╝██╔══██╗
   ██║   ██║██╔██╗ ██║██║  ██║█████╗  ██████╔╝
   ██║   ██║██║╚██╗██║██║  ██║██╔══╝  ██╔══██╗
   ██║   ██║██║ ╚████║██████╔╝███████╗██║  ██║
   ╚═╝   ╚═╝╚═╝  ╚═══╝╚═════╝ ╚══════╝╚═╝  ╚═╝            ";
        string[] options = ["play", "settings", "quit"];
        UI.Menu StartMenu = new UI.Menu(prompt, options);
        int index = StartMenu.Run();

        switch (index)
        {
            case 0:
                menu.start();
                break;
            case 1:
                menu.Controls();
                break;
            case 2:
                menu.quit();
                break;
        }
    }
}
class MenuFunctions
{   
    public void start()
    {
        new projekt_pva_2.TournamentConsole(projekt_pva_2.Launcher.SavePath).Run();
    }
    public void Controls()
    {
        Console.Clear();
        Console.Write(@"");
    }
    public void quit()
    {
        Console.Clear();
        Console.Write($"quiting");
        for (int i = 0; i < 3; i++)
        {
            Console.Write(".");
            Thread.Sleep(2000);
        }
    }

}
