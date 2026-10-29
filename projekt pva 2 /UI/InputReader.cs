namespace projekt_pva_2;

public sealed class InputReader(TextReader input, TextWriter output)
{
    public string? ReadText(string prompt)
    {
        while (true)
        {
            output.Write(prompt);
            string? line = input.ReadLine();
            if (line is null) return null;
            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            output.WriteLine("Vstup nesmí být prázdný. Zadejte jej znovu.");
        }
    }

    public int? ReadInt(string prompt, int minimum, int maximum)
    {
        while (true)
        {
            string? line = ReadText(prompt);
            if (line is null) return null;
            if (int.TryParse(line, out int number) && number >= minimum && number <= maximum) return number;
            output.WriteLine($"Zadejte celé číslo od {minimum} do {maximum}.");
        }
    }
}
