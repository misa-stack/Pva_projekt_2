# Použití třídy FileSystem

`FileSystem` ukládá záznamy do textového souboru. Každý záznam má neprázdný textový klíč a pole textových položek (`string[]`). Význam klíče, pořadí položek a převod na vlastní objekty řeší volající program. Samotné úložiště o turnajích ani jiných konkrétních datech nic neví.

Třídu najdete v [filesystem.cs](<../projekt pva 2 /filesystem.cs>). Nepoužívá JSON, XML ani knihovnu pro serializaci.

## Rychlý začátek

```csharp
using projekt_pva_2;

var soubor = new FileSystem("data.pva");
soubor.Set("poznamka", "Ahoj všem!");
soubor.Set("kontakt", "Jana", "jana@example.cz");
soubor.Set("bez-polozek");

string[] kontakt = soubor.Get("kontakt");
Console.WriteLine(kontakt[0]); // Jana
Console.WriteLine(kontakt[1]); // jana@example.cz
```

Pokud hlavní soubor ani záloha neexistují, konstruktor vytvoří soubor s prázdným úložištěm. Jinak data načte, případně obnoví platnou zálohu. Chybějící složky v cestě vytvoří automaticky.

Relativní cesta se počítá od pracovního adresáře programu. Cestu vedle sestavené aplikace můžete vytvořit takto:

```csharp
var soubor = new FileSystem(Path.Combine(AppContext.BaseDirectory, "data.pva"));
Console.WriteLine(soubor.FilePath); // Úplná cesta.
```

Program musí mít právo ve zvolené složce číst i zapisovat, protože i čtení používá soubor zámku.

## Běžné operace

`Set` přidá záznam nebo nahradí celé pole pod existujícím klíčem. Změnu uloží ihned, ještě před návratem z metody. `Get` vrátí kopii pole; pro neexistující klíč vyvolá `KeyNotFoundException`.

```csharp
soubor.Set("poznamka", "Původní text");
soubor.Set("poznamka", "Opravený text");

string[] polozky = soubor.Get("poznamka");
polozky[0] = "Další změna";
// Změna pole v paměti se sama neukládá:
soubor.Set("poznamka", polozky);

bool odstraneno = soubor.Remove("poznamka");
Console.WriteLine(odstraneno ? "Odstraněno." : "Klíč neexistuje.");

Console.WriteLine($"Počet záznamů: {soubor.Count}");
foreach (string klic in soubor.Keys)
    Console.WriteLine(klic);

soubor.Clear(); // Vymaže záznamy, soubor na disku zůstane.
```

Úspěšné odstranění přes `Remove` a vymazání neprázdného úložiště přes `Clear` se také hned ukládají. `Remove` pro chybějící klíč a `Clear` pro už prázdné úložiště nic nezapisují ani nemění zálohu. Čtení neukládá nový stav; při zjištění poškozeného hlavního souboru ale může proběhnout obnova ze zálohy.

`Set("a")` ukládá pole bez položek. `Set("a", "")` ukládá jednu položku s prázdným textem. Tyto stavy se liší. Prázdný klíč nebo klíč obsahující jen bílé znaky není povolený; pole ani jeho jednotlivé položky nesmějí být `null`.

Při dalším spuštění stačí zavolat `new FileSystem("data.pva")` a `Get`. Opakovaným voláním `Set` byste už uloženou hodnotu znovu nahradili.

## Načtení a nahrazení všech záznamů

`ReadAll` vrátí nový slovník s kopiemi všech polí. Změny tohoto slovníku na disku nic nezmění. `ReplaceAll` zkontroluje a zkopíruje všechny předané záznamy a pak uloží celé úložiště jednou operací.

```csharp
Dictionary<string, string[]> zaznamy = soubor.ReadAll();
zaznamy["kontakt"] = ["Petr", "petr@example.cz"];
zaznamy.Remove("stary-kontakt");
soubor.ReplaceAll(zaznamy);
```

To se hodí pro část aplikace, která převádí vlastní objekty na textové položky a potřebuje uložit jeden celý stav. Při neplatném vstupu se žádná část náhrady neuloží. Prázdný slovník přes `ReplaceAll` je platná úplná náhrada.

Pokud se stejným souborem pracuje více otevřených instancí aplikace, předejte jako druhý, nepovinný argument také původní načtený stav:

```csharp
Dictionary<string, string[]> puvodni = soubor.ReadAll();
var upravene = puvodni.ToDictionary(zaznam => zaznam.Key, zaznam => zaznam.Value.ToArray());
upravene["kontakt"] = ["Eva", "eva@example.cz"];

soubor.ReplaceAll(upravene, puvodni);
// Až po úspěšném uložení se nový stav stává výchozím pro další změny:
puvodni = upravene;
```

`ReplaceAll` zkontroluje aktuální soubor a očekávaný původní stav pod stejným zámkem, který chrání zápis. Musí souhlasit všechny klíče, počty položek i texty v jejich původním pořadí; pořadí klíčů ve slovníku nehraje roli. Porovnání rozlišuje velikost písmen. Pokud jiná instance mezitím data změnila, metoda vyvolá `IOException` a neprovede zápis ani otočení zálohy. Potom je nutné načíst aktuální stav a změnu nad ním znovu vyhodnotit.

Původní stav si uchovávejte jako samostatnou kopii a během úprav ho neměňte. Prázdný očekávaný slovník znamená, že aktuální úložiště musí být prázdné. Vynechaný druhý argument nebo `null` znamená nahrazení bez kontroly souběžné změny. Oba předané slovníky se před operací validují a kopírují.

Úložiště umí pouze text. Čísla či datum převádějte a ověřujte v aplikaci. U čísel použijte stejnou kulturu při zápisu i čtení:

```csharp
using System.Globalization;

soubor.Set("pocet", 5.ToString(CultureInfo.InvariantCulture));
int pocet = int.Parse(soubor.Get("pocet")[0], CultureInfo.InvariantCulture);
```

## Vlastní formát souboru

Doporučená přípona je `.pva`; třída ji sama nepřidává ani nekontroluje. Obsah je text v UTF-8 bez BOM. Například:

```text
PVA|1|3
D|poznamka|1|Ahoj všem!
D|kontakt|2|Jana|jana@example.cz
D|bez-polozek|0
END
```

Hlavička `PVA|1|3` znamená označení formátu `PVA`, verzi `1` a počet `3` záznamů. Každý záznam začíná značkou `D`, následuje klíč, počet položek a jednotlivé položky. Poslední řádek musí být `END`. Po něm smí být nejvýše jedno ukončení řádku. Zápis používá LF; čtení přijímá také CRLF. Počty jsou nezáporná celá čísla z číslic `0` až `9`, bez znaménka a mezer.

Některé znaky uvnitř klíče nebo položek se zapisují pomocí escape sekvence:

| Původní znak | Zápis v souboru |
| --- | --- |
| Zpětné lomítko `\` | `\\` |
| Svislá čára `|` | `\|` |
| Nový řádek | `\n` |
| Návrat vozíku | `\r` |

Tak lze uložit české znaky, svislé čáry, zpětná lomítka, prázdné texty i víceřádkový text. Čtení zpracovává znaky postupně; obyčejné rozdělení řádku podle každé svislé čáry by bylo chybné.

Nesouhlasící počty, duplicitní klíče, neznámé značky nebo escape sekvence, chybějící `END` a neplatné UTF-8 jsou chyby. Nepodporovaná verze se odmítne ještě před výkladem záznamů této verze a neobnoví se přes ni starší záloha.

**Starý JSON není s tímto formátem kompatibilní.** Třída jej neumí převádět. Neplatný původní soubor bez použitelné zálohy zůstane beze změny a načtení vyvolá výjimku. Neřešte ji tichým smazáním nebo vytvořením nového prázdného úložiště nad původními daty. Nejprve původní soubor uchovejte; případný převod musí být samostatná vědomá operace. Pokud je hlavní soubor neplatný a existuje platná záloha v novém formátu, platí běžná obnova popsaná níže.

## Zámek, zápis a záloha

| Soubor | Účel |
| --- | --- |
| `data.pva` | Aktuálně uložené záznamy. |
| `data.pva.bak` | Předchozí úspěšně uložený stav. Vznikne při nahrazení existujícího hlavního souboru. |
| `data.pva.lock` | Soubor používaný pro výhradní zámek během jedné operace. Zůstává na disku i po uvolnění zámku. |
| `data.pva.<náhodné-id>.tmp` | Dočasný soubor při ukládání; po operaci se přesune nebo odstraní. |

Každá operace s daty získá výhradní zámek a znovu přečte aktuální soubor. Dvě instance tak při postupných změnách přes `Set` nebo `Remove` nepřepisují zastaralou kopii. Když zámek právě drží jiná operace, třída nečeká a vyvolá `IOException`; volající může operaci zopakovat později. Samotná existence souboru `.lock` neznamená, že je zámek držený. Nemažte jej kvůli odemykání.

`ReadAll` a následné `ReplaceAll` jsou dvě samostatné operace. `ReplaceAll` atomicky ukládá celý předaný stav a neslučuje jej se změnami jiné instance. Pro ochranu před přepsáním mezitím změněných dat použijte druhý argument s původním stavem, jak ukazuje příklad výše.

Nový obsah se nejprve celý připraví a zapíše do dočasného souboru. `Flush(true)` požádá o jeho zápis na disk; teprve potom se nahradí hlavní soubor a předchozí stav se stane zálohou. Chyba validace, převodu textu nebo nahrazení souboru nesmí být považována za úspěšné uložení.

Pokud hlavní soubor chybí nebo je poškozený a záloha je platná, třída obnoví hlavní soubor ze zálohy. Při obnově zachová zálohu a neuloží do ní poškozený obsah. Obnovená data odpovídají předchozímu stavu, takže poslední změna může chybět. Pokud jsou oba soubory neplatné, zůstanou beze změny.

```csharp
if (soubor.RecoveredFromBackup)
    Console.WriteLine("Data byla obnovena ze zálohy.");
```

Příznak zůstává `true`, pokud během používání této instance proběhla obnova.

## Možné chyby

| Výjimka | Význam |
| --- | --- |
| `ArgumentException` nebo `ArgumentNullException` | Neplatná cesta, prázdný klíč, chybějící pole nebo položka `null`. |
| `KeyNotFoundException` | Čtený klíč neexistuje. |
| `InvalidDataException` | Neplatný obsah bez použitelné zálohy. |
| `NotSupportedException` | Nepodporovaná verze souboru nebo použité zálohy. |
| `System.Text.EncoderFallbackException` | Předaný řetězec obsahuje neplatnou posloupnost UTF-16 a nelze jej zapsat jako UTF-8. |
| `FileNotFoundException` | Za běhu zmizel hlavní soubor i záloha. |
| `UnauthorizedAccessException` | Chybí potřebná oprávnění. |
| `IOException` | Problém se souborem, kolize zámku nebo nesouhlas očekávaného stavu při podmíněném `ReplaceAll`. |

Výjimky zachyťte na vhodném místě aplikace a zobrazte srozumitelnou zprávu. Po chybě zápisu nepovažujte změny v objektech aplikace za uložené.

## Ověřovací program

Z kořene projektu spusťte:

```sh
dotnet run --project verification/StorageChecks --configuration Release -p:NuGetAudit=false
```

Kontroly používají skutečné soubory v dočasné složce systému a po skončení je uklidí. Nevyžadují testovací balíček z NuGetu.
