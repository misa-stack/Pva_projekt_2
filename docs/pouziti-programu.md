# Použití turnajového programu

Projekt potřebuje .NET SDK 10. Příkazy spouštějte z hlavní složky repozitáře, ve které je soubor řešení `.sln`.

```sh
dotnet run --project "projekt pva 2 /projekt pva 2.csproj"
```

Původní startovací menu ovládáte šipkami a Enterem. Volba **play** otevře správu turnaje. V ní se volby zadávají číslem a potvrzují Enterem. Volba `0` správu ukončí.

Pro přímé spuštění číselného menu a vlastní umístění dat:

```sh
dotnet run --project "projekt pva 2 /projekt pva 2.csproj" -- --text --save "data/turnaj.pva"
```

Bez `--save` se používá `data.pva` vedle sestavené aplikace. Parametr `--save` přijímá i úplnou cestu; relativní cesta vychází z pracovního adresáře. Při přesměrovaném vstupu se číselné menu použije automaticky.

## První turnaj

1. Volbou `2` přidejte hráče: jméno a jedinečnou přezdívku. Přezdívky se porovnávají bez ohledu na velikost písmen.
2. Volbou `4` založte turnaj s názvem, disciplínou a počtem kol.
3. Volbou `5` přihlaste evidované hráče podle přezdívky. Seznam zkontrolujte volbou `7`.
4. Zkontrolujte počet kol: pro sudý počet účastníků `n` je potřeba `n−1` kol, pro lichý počet `n` kol. Před začátkem jej můžete opravit volbou `14`.
5. Volbou `8` spusťte kolo. Program připraví dvojice a případný volný los. S méně než dvěma účastníky kolo nezačne a data se nezmění.
6. Po odehrání zadejte volbou `10` číslo zápasu a výsledek: `1` výhra prvního hráče, `2` výhra druhého, `3` remíza. Čísla zápasů zobrazí volba `9`.
7. Chybný výsledek lze stejnou volbou opravit, dokud kolo není uzavřené.
8. Volbou `11` uzavřete kolo. Všechny zápasy už musí mít výsledek. Potom můžete spustit další kolo.
9. Volba `12` kdykoli zobrazí aktuální tabulku. Po uzavření posledního kola je turnaj dokončen a lze založit další. Starší turnaje zůstanou uložené v historii, jejíž přehled zobrazí volba `13`.

Příklad: se třemi účastníky nastavte tři kola. V každém bude jeden zápas a jeden volný los; každý hráč jednou vynechá a s každým soupeřem se utká právě jednou.

## Přehled voleb

| Volba | Funkce |
| --- | --- |
| 1 / 2 / 3 | Výpis / přidání / odebrání hráče. |
| 4 | Založení turnaje. |
| 5 / 6 / 7 | Přihlášení / odhlášení / výpis účastníků. |
| 8 / 9 | Spuštění kola / zobrazení zápasů posledního kola. |
| 10 / 11 | Zadání nebo oprava výsledku / uzavření kola. |
| 12 / 13 | Tabulka / přehled historie turnajů. |
| 14 | Změna počtu kol před zahájením turnaje. |
| 15 | Znovunačtení uloženého stavu, například po změně z jiné instance. |
| 0 | Ukončení správy. |

## Aktuální chování bodování

Implementace připisuje 1 bod za výhru, 0,5 bodu za remízu, 0 bodů za prohru a 1 bod za volný los. Volný los nezvyšuje počet odehraných zápasů ani výher. Tabulka započítává už zadané výsledky i v otevřeném kole.

Pořadí určuje postupně počet bodů, součet bodů skutečně odehraných soupeřů a počet výher, vždy sestupně. Pokud se shodují i tato kritéria, používá se přezdívka podle `OrdinalIgnoreCase`, aby byl výpis jednoznačný. Toto je popis chování kódu; vlastní školní specifikaci a návrh zpracuje a obhájí tým podle pravidel zadání.

## Ukládání a chyby

Každá úspěšná změna uloží celý stav jedním zápisem. Po opětovném spuštění se stejnou cestou se obnoví hráči, historie, aktuální turnaj, účastníci, kola, zápasy a výsledky. Není potřeba ukládat při ukončení zvlášť.

Po prvním spuštěném kole už nelze měnit účastníky. Hráče v rozehraném nebo dokončeném rozpisu nelze odebrat z evidence. Výsledky uzavřených kol jsou zamčené.

Prázdný vstup, text místo čísla a číslo mimo rozsah lze zadat znovu. Chyba operace se vypíše a vrátí vás do menu. Konec vstupu program ukončí bez další změny dat.

Chybějící nový soubor se vytvoří. Při poškození se zkusí platná předchozí záloha `.bak`; ta obsahuje předchozí uložený stav. Když soubor nejde otevřít nebo obsahuje neplatná turnajová data, můžete zadat jinou cestu, nebo `0` pro návrat. Nepodporovaná verze se automaticky nepřepisuje.

Při konfliktu dvou otevřených instancí se zastaralá změna odmítne. Volbou `15` načtěte aktuální stav a zadejte změnu znovu. Při selhání zápisu program neohlásí úspěšné uložení.

Původní soubory založené na JSON nejsou novým formátem. Uchovejte si jejich kopii a zvolte nový `.pva` soubor; program je nepřevede pomocí JSON knihovny. Podrobnosti samotné ukládací třídy jsou v [návodu FileSystem](pouziti-filesystem.md).
