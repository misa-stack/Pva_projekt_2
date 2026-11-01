# Návrh a formát ukládání

Dokument zachycuje uspořádání současné implementace k 29. 10. 2026.

## Oddělení částí

Konzolové ovládání načítá vstupy a vypisuje výsledky. `TournamentService` provádí změny a hlídá pravidla. `TournamentStore` převádí celý stav na řetězcové záznamy. `FileSystem` řeší vlastní souborový formát a bezpečný zápis. Turnajová logika nevolá `Console.WriteLine`.

## Přehled tříd

| Typ | Uchovávaná data a odpovědnost |
| --- | --- |
| `Player` | Jméno a přezdívka hráče. |
| `MatchResult` | Výčet nezadaného výsledku, obou výher a remízy. |
| `Match` | ID v rámci turnaje, přezdívky soupeřů, výsledek. |
| `Round` | Číslo kola, uzavření, případný volný hráč, seznam zápasů. |
| `Tournament` | ID, název, disciplína, počet kol, soupiska a kola; dokončení se odvozuje z kol. |
| `TournamentState` | Globální evidence hráčů a seznam všech turnajů; aktuální je poslední turnaj. |
| `Standing` | Vypočítaný řádek tabulky. |
| `TournamentService` | Interní stav, jeho ověřování, změny a předaná ukládací funkce. |
| `TournamentStore` | Převod stavu na záznamy a zpět, poslední úspěšný snímek pro kontrolu zápisu. |
| `FileSystem` | Cesta souboru, příznak obnovy, čtení/zápis obecných klíčů s poli řetězců. |
| `InputReader` | Čte text a celá čísla, opakuje chybný vstup. |
| `TournamentConsole` | Číselné menu, dotazy, výpisy a zpracování očekávaných chyb. |
| `Launcher` | Parametry spuštění, cesta k datům a volba textového režimu. |
| `Menu`, `MenuFunctions`, `Program` | Původní šipkové menu a jeho napojení na správu turnaje. |

Turnaj obsahuje kola a kolo obsahuje zápasy. Hráči se v rozpisech odkazují přezdívkou. Stav předávaný ven se kopíruje, aby nešlo obejít pravidla přímou úpravou interních seznamů.

## Změna dat a automatické uložení

Služba nejprve upraví kopii stavu, ověří ji a předá ukládací funkci. Teprve po úspěšném zápisu se kopie stane aktuálním stavem. Selhání zápisu tak nezpůsobí rozdíl mezi stavem, který aplikace považuje za uložený, a posledním úspěšným souborem.

Tabulka se znovu vypočítává z výsledků; body se neukládají jako další ručně udržovaný součet. Pravidla bodování jsou ve [SPECIFIKACE.md](SPECIFIKACE.md).

## Vlastní textový formát PVA

Soubor je text UTF-8 bez BOM. Zapisují se řádky oddělené LF. Čtečka přijímá i CRLF a poslední řádek s koncovým odřádkováním nebo bez něj. JSON, XML ani serializační knihovny se při ukládání nepoužívají.

```text
PVA|1|2
D|state|2|STATE|1
D|player/anna|3|PLAYER|Anna|anna
END
```

První řádek obsahuje označení `PVA`, verzi obecného formátu a počet datových záznamů. Každý datový řádek má tvar:

```text
D|klíč|počet-položek|položka-1|položka-2|...
```

`D` určuje typ obecného datového řádku. Jedinečný klíč slouží k vyhledání. Počet říká, kolik řetězcových položek má následovat; nula položek je něco jiného než jedna prázdná položka. Samostatný řádek `END` ukončuje soubor.

### Oddělovače a speciální znaky

Oddělovačem položek je `|`. Uvnitř hodnot se převádí:

| Původní znak | Zápis v souboru |
| --- | --- |
| Zpětné lomítko | `\\` |
| Svislá čára | `\|` |
| Nový řádek LF | `\n` |
| Návrat vozíku CR | `\r` |

Čtečka prochází řádek po znacích, aby nezaměnila escapovanou čáru za oddělovač. Neznámá nebo nedokončená escape sekvence je chyba. Díky tomu lze uložit češtinu, mezery, oddělovače i víceřádkový text bez ztráty obsahu.

### Turnajové záznamy

Význam první položky určuje `TournamentStore`. Obecný `FileSystem` tyto typy znát nemusí.

| Klíč | Položky v pořadí |
| --- | --- |
| `state` | `STATE`, verze turnajového stavu `1`. |
| `player/přezdívka` | `PLAYER`, jméno, přezdívka. |
| `tournament/id` | `TOURNAMENT`, ID, název, disciplína, počet kol. |
| `participant/id/index` | `PARTICIPANT`, ID turnaje, pořadí od nuly, přezdívka. |
| `round/id/číslo` | `ROUND`, ID turnaje, číslo kola, uzavření `0`/`1`, volná přezdívka nebo prázdný řetězec. |
| `match/id/IDzápasu` | `MATCH`, ID turnaje, číslo kola, ID zápasu, první soupeř, druhý soupeř, výsledek `0` až `3`. |

Výsledky odpovídají `MatchResult`: `0` nezadaný, `1` výhra prvního, `2` výhra druhého, `3` remíza. Čísla se zapisují a čtou nezávisle na jazykovém nastavení počítače.

### Vyhledávání, pořadí a duplicity

Načtené záznamy jsou v `Dictionary<string, string[]>`. Přístup podle klíče má běžně průměrnou složitost O(1); každá veřejná souborová operace ale nejprve načítá soubor, takže její celková cena zahrnuje průchod souborem. Nejde o index pro přímé skákání na pozici na disku.

ID turnajů, kol a zápasů a pořadí účastníků umožňují obnovit původní návaznost. Při načtení se nejprve sestaví hráči a turnaje, potom účastníci, kola a zápasy.

- Duplicitní klíč v souboru se odmítne; žádná varianta se potichu nezahodí.
- Přezdívky se navíc kontrolují bez ohledu na velikost písmen v turnajové validaci.
- Ověřuje se soulad klíče s údaji záznamu, existence odkazovaných hráčů/turnajů/kol, pořadí ID, úplnost uzavřených kol a správnost rozpisu.
- Opakované `Set` stejného klíče je naopak výslovná změna hodnoty v API, nikoli přijetí duplicitního řádku ze souboru.

### Zápis, záloha a chyby

Celý nový text vznikne před zápisem. Zapíše se do jedinečného dočasného souboru vedle hlavního, vyprázdní se jeho buffery a poté nahradí hlavní soubor. Předchozí hlavní stav se uchová jako `.bak`. Nové úložiště se vytvoří přesunem dočasného souboru na cílovou cestu.

Záloha slouží k obnově předchozího stavu při chybějícím nebo poškozeném hlavním souboru. Při neplatném obsahu hlavního souboru i zálohy se předá chyba a soubory se bez platné obnovy nepřepisují. Nepodporovaná verze má jiný typ chyby než poškození, aby se novější data nenahradila starší zálohou.

Každá operace drží výhradní zámek přes `.lock`. Při nahrazení celého stavu se v témže zámku porovná také původní snímek. Pokud mezitím jiná instance uložila změny, starý zápis se odmítne a je potřeba znovu načíst stav.

Formát je zvolený jako textový a kontrolovatelný: řádky lze prohlédnout, počty a koncová značka pomáhají odhalit neúplné zápisy, escapování řeší oddělovače v hodnotách a verze umožní odlišit budoucí změnu formátu.

Návod k obecnému úložišti: [docs/pouziti-filesystem.md](docs/pouziti-filesystem.md). Ověření: [docs/overeni.md](docs/overeni.md).
