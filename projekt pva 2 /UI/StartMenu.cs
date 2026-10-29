namespace StartMenu;

using UI;
class Program
{
    static void Main(string[] args)
    {
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