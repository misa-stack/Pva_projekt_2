# Použití třídy FileSystem

Třída `FileSystem` slouží k ukládání dat do souboru a jejich načtení při dalším spuštění. Je obecná, takže ji můžete použít v různých částech programu. Najdete ji v souboru [filesystem.cs](<../projekt pva 2 /filesystem.cs>).

Data se ukládají pod textovými klíči. Klíč je název, podle kterého později najdete hodnotu, například `"poznamka"` nebo `"pocet"`.

## Rychlý začátek

Do souboru, ze kterého chcete třídu používat, přidejte `using projekt_pva_2;`. Následující příklad můžete vložit do `Program.cs`:

```csharp
using projekt_pva_2;

var soubor = new FileSystem("data.pva");

soubor.Set("poznamka", "Ahoj všem!");
soubor.Set("pocet", 5);

string? poznamka = soubor.Get<string>("poznamka");
int pocet = soubor.Get<int>("pocet");

Console.WriteLine(poznamka);
Console.WriteLine(pocet);
```

Pokud `data.pva` ještě neexistuje, vytvoří se prázdný soubor. Pokud už existuje, načtou se jeho data. Při použití cesty s podsložkami se chybějící složky vytvoří automaticky.

## Automatické ukládání

Každé úspěšné volání `Set`, odstranění existujícího klíče přes `Remove` a vymazání neprázdného úložiště přes `Clear` uloží změnu ještě před návratem z metody. Není potřeba volat další metodu `Save` ani čekat na ukončení programu.

Čtení pomocí `Get`, `Count` a `Keys` data nemění. Odstranění neexistujícího klíče a vymazání už prázdného úložiště také nový zápis neprovádí.

### Přidání nebo změna hodnoty

```csharp
soubor.Set("poznamka", "První text");
soubor.Set("poznamka", "Opravený text");
```

Druhé volání nahradí hodnotu pod stejným klíčem. Ostatní uložené klíče zůstanou zachované.

### Načtení hodnoty

```csharp
string? text = soubor.Get<string>("poznamka");
int pocet = soubor.Get<int>("pocet");
```

V `Get<T>` uveďte odpovídající datový typ. Text uložený přes `Set` například načítejte přes `Get<string>`. Pokud klíč neexistuje, metoda vyvolá `KeyNotFoundException`.

### Odstranění hodnoty

```csharp
bool odstraneno = soubor.Remove("poznamka");
Console.WriteLine(odstraneno ? "Hodnota byla odstraněna." : "Klíč neexistuje.");
```

### Vymazání všech hodnot

```csharp
soubor.Clear();
```

Tím se uloží prázdné úložiště. Samotný soubor zůstane na disku.

### Přehled uložených klíčů

```csharp
Console.WriteLine($"Počet uložených hodnot: {soubor.Count}");

foreach (string klic in soubor.Keys)
{
    Console.WriteLine(klic);
}
```

## Seznamy a vlastní objekty

Ukládat můžete také pole, seznamy a objekty, které lze převést do JSON. U vlastních tříd používejte veřejné vlastnosti s `get` a `set`; běžná pole se s aktuálním nastavením neukládají.

```csharp
soubor.Set("poznamky", new List<string> { "První poznámka" });

List<string> poznamky = soubor.Get<List<string>>("poznamky")!;
poznamky.Add("Druhá poznámka");

// Teprve tímto voláním se upravený seznam uloží:
soubor.Set("poznamky", poznamky);
```

**Důležité:** `Get` vrací samostatnou kopii hodnoty. Změna seznamu nebo vlastnosti objektu v paměti sama o sobě soubor neaktualizuje. Upravenou hodnotu vždy předejte zpět metodě `Set`. Totéž platí pro objekt, který změníte až po jeho uložení.

## Pokračování při dalším spuštění

Stačí znovu otevřít stejný soubor. Už uložené hodnoty nemusíte znovu nastavovat:

```csharp
using projekt_pva_2;

var soubor = new FileSystem("data.pva");
int pocet = soubor.Get<int>("pocet");
Console.WriteLine($"Načtená hodnota: {pocet}");
```

Tento příklad předpokládá, že klíč `"pocet"` byl uložen dříve. `Set` při každém spuštění znovu provede zápis, takže jím nepřepisujte hodnoty, které chcete pouze načíst.

Relativní cesta `"data.pva"` se počítá od aktuálního pracovního adresáře programu. Pokud chcete soubor vždy vedle sestavené aplikace, použijte:

```csharp
var soubor = new FileSystem(Path.Combine(AppContext.BaseDirectory, "data.pva"));
Console.WriteLine(soubor.FilePath); // Úplná cesta k otevřenému souboru.
```

Program musí mít právo do zvolené složky zapisovat.

## Soubory a obnova ze zálohy

Pro přehlednost používejte příponu `.pva`. Uvnitř je čitelný JSON s označením formátu, verzí a uloženými daty; třída příponu sama nepřidává ani nekontroluje.

| Soubor | K čemu slouží |
| --- | --- |
| `data.pva` | Aktuální uložená data. |
| `data.pva.bak` | Předchozí úspěšně uložený stav. Vzniká při nahrazení existujícího souboru. |
| `data.pva.lock` | Pomocný soubor pro zabránění souběžným zápisům. Může zůstat na disku i po ukončení programu. |
| `data.pva.<náhodný identifikátor>.tmp` | Dočasný soubor během ukládání. Po úspěšném uložení se přesune na místo hlavního souboru. |

Nový stav se nejprve celý zapíše do dočasného souboru a až potom nahradí hlavní soubor. Když hlavní soubor chybí nebo je poškozený a existuje platná záloha, třída ji automaticky obnoví. Obnova vrací předchozí uložený stav, takže poslední změna z hlavního souboru v záloze nebude.

```csharp
if (soubor.RecoveredFromBackup)
{
    Console.WriteLine("Data byla obnovena z předchozí zálohy.");
}
```

## Chyby, se kterými počítat

Chyby třída předává volajícímu kódu jako výjimky. V aplikaci je můžete zachytit pomocí `try` / `catch` a zobrazit uživateli zprávu.

| Výjimka | Co znamená |
| --- | --- |
| `ArgumentException` | Prázdná cesta nebo klíč obsahující pouze mezery či jiné bílé znaky. |
| `KeyNotFoundException` | Pokus o načtení neexistujícího klíče. |
| `System.Text.Json.JsonException` | Hodnotu nelze uložit do JSON nebo ji načíst jako požadovaný typ. |
| `InvalidDataException` | Soubor má neplatný obsah a nepodařilo se použít platnou zálohu. |
| `NotSupportedException` | Soubor používá nepodporovanou verzi formátu. Jeho obsah se automaticky nepřepíše starší zálohou. |
| `UnauthorizedAccessException` | Program nemá potřebná oprávnění. |
| `IOException` | Problém se čtením nebo zápisem, případně soubor právě používá jiná operace. |

Když ukládání vyvolá výjimku, nepovažujte změnu za uloženou. Při souběžném použití třída nečeká na uvolnění zámku; neúspěšnou operaci je potřeba zopakovat později.
