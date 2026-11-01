# Specifikace turnajového programu

Dokument popisuje současné chování aplikace k 29. 10. 2026.

## Účel a uživatel

Program používá pořadatel školního turnaje. Hráči odehrají vlastní hru mimo aplikaci, například šachy nebo piškvorky. Pořadatel eviduje účastníky, spouští kola a zadává výsledky. Program sestaví dvojice, vypočítá tabulku a uchová rozdělaný stav.

## Slovník pojmů

| Pojem | Význam |
| --- | --- |
| Pořadatel | Uživatel, který obsluhuje program a zadává výsledky. |
| Hráč | Osoba se jménem a jedinečnou přezdívkou v evidenci. |
| Přezdívka | Identifikátor hráče. Velikost písmen nerozhoduje: `anna` a `ANNA` jsou stejná přezdívka. |
| Účastník | Evidovaný hráč přihlášený do konkrétního turnaje. |
| Turnaj | Soutěž s názvem, disciplínou, počtem kol, účastníky a výsledky. |
| Kolo | Skupina zápasů, ve které má účastník nejvýše jeden zápas, případně volný los. |
| Zápas | Dvojice účastníků a jejich výsledek; samotná hra probíhá mimo program. |
| Volný los | Volno pro jednoho hráče v kole s lichým počtem účastníků. |
| Otevřené kolo | Kolo, do kterého lze zadávat a opravovat výsledky. |
| Uzavřené kolo | Kolo s potvrzenými úplnými výsledky, které již nelze měnit. |
| Dokončený turnaj | Turnaj, který má vytvořená a uzavřená všechna požadovaná kola. |
| Body soupeřů | Součet aktuálních bodů všech soupeřů, se kterými už má hráč zadaný výsledek. |

## Hráči a přihlášky

- Hráč musí mít neprázdné jméno a přezdívku. Krajní mezery se při zadání odstraní.
- Přezdívka je jedinečná v celé evidenci bez ohledu na velikost písmen.
- Do turnaje lze přihlásit pouze existujícího hráče a pouze jednou.
- Přihlášení, odhlášení a změna počtu kol jsou možné před začátkem prvního kola.
- Odhlášení z turnaje hráče nesmaže z evidence.
- Hráče ze soupisky již zahájeného nebo staršího turnaje nelze smazat z evidence, aby zůstaly platné rozpisy a historie. Před startem lze hráče odebrat i s jeho dosavadní přihláškou.

## Turnaj a losování

Otevřený je nejvýše jeden nedokončený turnaj. Nový se založí až po dokončení předchozího; dokončené turnaje zůstanou uložené v historii.

Používá se úplný systém každý s každým. Pro `n` účastníků má turnaj při sudém počtu `n−1` kol, při lichém počtu `n` kol. Tento počet se kontroluje při zahájení kola. Pořadatel jej může před startem opravit.

- S žádným nebo jediným účastníkem kolo nezačne a stav se nezmění.
- Dvojice určuje program podle pořadí přihlášení kruhovým rozpisem.
- Každá dvojice se v celém turnaji vyskytne právě jednou.
- V jednom kole se žádný hráč nevyskytne ve dvou zápasech.
- Při lichém počtu účastníků má jeden hráč volný los. V úplném turnaji jej má každý právě jednou.
- Další kolo lze spustit až po uzavření aktuálního. Po dokončení všech kol již další nepřibývá.

## Výsledky a uzavření kola

Zadaný výsledek je výhra prvního hráče, výhra druhého hráče nebo remíza. Nezadaný zápas má stav `Pending` a do bodů ani odehraných zápasů se zatím nepočítá.

Výsledek lze zadat a opravit pouze u existujícího zápasu aktuálního otevřeného kola. Kolo nelze uzavřít, dokud některému zápasu výsledek chybí. Po uzavření se výsledky kola nemění.

## Bodování a tabulka

| Událost | Body | Odehrané zápasy | Výhry |
| --- | --- | --- | --- |
| Výhra | 1 | +1 | +1 |
| Remíza | 0,5 | +1 | beze změny |
| Prohra | 0 | +1 | beze změny |
| Volný los | 1 | beze změny | beze změny |

Volný los se započítá při spuštění příslušného kola. Zadané výsledky se započítávají ihned, i když kolo ještě není uzavřené. Oprava výsledku se projeví v následujícím zobrazení tabulky.

Pořadí určuje postupně:

1. Počet bodů, sestupně.
2. Součet aktuálních bodů skutečně odehraných soupeřů, sestupně.
3. Počet výher, sestupně.
4. Přezdívka podle `OrdinalIgnoreCase`, pokud se shodují i předchozí hodnoty.

Tabulka ukazuje pořadí, přezdívku, jméno, body, odehrané zápasy, body soupeřů a výhry. Volný los nemá soupeře, takže body soupeřů nezvyšuje.

## Ukládání, pokračování a chyby

Každá úspěšná změna uloží celý stav: hráče, historii i aktuální turnaj, účastníky, kola, zápasy, výsledky a volné losy. Po restartu se stejným souborem lze pokračovat uprostřed kola.

Používá se vlastní textový formát `.pva`, popsaný v [NAVRH.md](NAVRH.md). Při novém chybějícím souboru vznikne prázdné úložiště. Poškozený hlavní soubor lze obnovit z platné předchozí zálohy. U neplatných dat nebo nepodporované verze program vysvětlí problém a umožní zvolit jinou cestu nebo se vrátit.

Zápis z druhé otevřené instance nesmí přepsat mezitím změněný stav bez nového načtení. Selhání ukládání se nehlásí jako úspěch a služba nepublikuje neuloženou změnu.

Text místo čísla, prázdný vstup a číslo mimo rozsah lze zadat znovu. Ukončený vstup správu korektně ukončí. Turnajová logika neobsahuje konzolové vypisování.

## Seznam funkcí

Evidence hráčů; založení turnaje; přihlášení a odhlášení; seznam účastníků; úprava počtu kol před startem; sestavení a spuštění kola; výpis zápasů a volna; zadání a oprava výsledku; uzavření kola; průběžná tabulka; přehled historie; automatické uložení; načtení; obnova zálohy; řešení chyb vstupu a souboru.

Návod: [použití programu](docs/pouziti-programu.md). Ověření a ruční kontrola: [docs/overeni.md](docs/overeni.md).
