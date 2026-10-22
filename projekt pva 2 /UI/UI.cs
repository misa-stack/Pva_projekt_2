namespace UI;
using static System.Console;

class Menu
{
    private string[] Options;
    private string Prompt;

    private int SelectedIndex;
    public Menu(string Prompt = "", string[] Options = null)
    {
        this.Prompt = Prompt;
        this.Options = Options;
        this.SelectedIndex = 0;
    }

    private void DisplayOptions()
    {
        Console.WriteLine(Prompt);
        ResetColor();
        for (int i = 0; i < Options.Length; i++)
        {
            string CurrentOption = Options[i];
            string prefix;

            if (i == SelectedIndex)
            {
                prefix = "►";
                ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                prefix = " ";
                ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine($"|{prefix}| {CurrentOption}");
        }
        ResetColor();

    }

    public int Run()
    {
        ConsoleKey KeyPressed;
        do
        {
            Clear();
            Console.SetCursorPosition(0, 0); // místo Clear()
            DisplayOptions();
            ConsoleKeyInfo KeyInfo = ReadKey(true);
            KeyPressed = KeyInfo.Key;

            if (KeyPressed == ConsoleKey.UpArrow)
            {
                SelectedIndex--;
                if (SelectedIndex == -1)
                {
                    SelectedIndex = Options.Length - 1;
                }
            }
            else if (KeyPressed == ConsoleKey.DownArrow)
            {
                SelectedIndex++;
                if (SelectedIndex == Options.Length)
                {
                    SelectedIndex = 0;
                }
            }
        } while (KeyPressed != ConsoleKey.Enter);

        return SelectedIndex;
    }


    public int RunText()
    {
        ConsoleKey KeyPressed;
        do
        {
            DisplayOptions();
            ConsoleKeyInfo KeyInfo = ReadKey(true);
            KeyPressed = KeyInfo.Key;

            if (KeyPressed == ConsoleKey.UpArrow)
            {
                SelectedIndex--;
                if (SelectedIndex == -1)
                {
                    SelectedIndex = Options.Length - 1;
                }
            }
            else if (KeyPressed == ConsoleKey.DownArrow)
            {
                SelectedIndex++;
                if (SelectedIndex == Options.Length)
                {
                    SelectedIndex = 0;
                }
            }
        } while (KeyPressed != ConsoleKey.Enter);

        return SelectedIndex;
    }

}


