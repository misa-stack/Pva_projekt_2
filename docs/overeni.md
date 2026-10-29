# Ověření a předání týmu

## Opakování automatických kontrol

Z hlavní složky repozitáře spusťte:

```sh
bash verification/verify.sh
```

Skript sestaví hlavní i samostatný UI projekt v obou pořadích a spustí tři ověřovací programy bez externích testovacích balíčků:

| Kontroly | Rozsah |
| --- | --- |
| `verification/StorageChecks` | 50 kontrol vlastního formátu, znaků, duplicit, poškození, verzí, záloh, chyb zápisu a konfliktů snímků. |
| `verification/TournamentChecks` | 2 113 tvrzení o pravidlech, historii, izolaci stavu, ukládání a úplných rozpisech pro 2 až 15 účastníků. |
| `verification/IntegrationChecks` | 9 scénářů ukládání a pokračování uprostřed kola, vstupů, menu, chyb souboru a souběžných instancí. |

Původní soubor `UI/UI.cs` obsahuje upozornění nullable `CS8625`. Jeho zdroj se při převzetí zachoval; upozornění nebrání sestavení. Kontroly a technické AI review nenahrazují přezkoušení jiným členem týmu.

## Doložení losování

| Počet hráčů | Ověřené chování |
| --- | --- |
| 0 | Spuštění kola je odmítnuto, stav zůstává stejný. |
| 1 | Spuštění kola je odmítnuto, lze doplnit účastníky. |
| 2 | Jedno kolo a právě jeden vzájemný zápas. |
| 3 | Tři kola, tři různé dvojice, každý jeden volný los. |
| 5 | Pět kol, deset různých dvojic, každý jeden volný los. |
| 2 až 15 | Pro každý úplný turnaj je ověřeno `n × (n−1) / 2` různých dvojic, žádné opakování soupeřů a právě jedna účast hráče v každém kole včetně případného volna. |

Tyto kontroly jsou přímo v `verification/TournamentChecks/Program.cs`; nejde pouze o ruční ukázkový výpis.

## Ruční přezkoušení jiným členem týmu

- [ ] Zkontrolovat příslušné PR a akceptační kritéria ticketů.
- [ ] Přidat hráče a zkusit duplicitní přezdívku s jinou velikostí písmen.
- [ ] Vyzkoušet vstupy prázdné, textové a mimo rozsah.
- [ ] Odehrát turnaj tří hráčů včetně opravy výsledku před uzavřením kola.
- [ ] Zkusit přihlášení po startu, neexistující zápas, uzavření nekompletního kola a změnu výsledku uzavřeného kola.
- [ ] Ukončit program uprostřed kola, spustit se stejným souborem a pokračovat.
- [ ] Na kopii dat vyzkoušet chybějící a poškozený soubor.
- [ ] Zkontrolovat pořadí tabulky a body za volný los.
- [ ] Do ticketů doplnit jméno kontrolujícího, výsledek a skutečný čas přezkoušení.

## Co musí tým dokončit před odevzdáním

Zadání vyhrazuje návrh specifikace a architektury týmu bez AI. `SPECIFIKACE.md` a `NAVRH.md` proto v této implementaci nejsou vydávány za vlastní návrh studentů. Tým je musí sám zpracovat, ověřit soulad s kódem a umět rozhodnutí vysvětlit a upravit.

Původní autorství zachovává Git historie. UI vytvořil účet `A2kio` (Frederik Křížek); nové implementační commity používají aktuálně nastavený Git účet Michala. Práci Serafima ani jeho čas tato relace nedoplňuje za něj.

Feature větve se slučují přes PR do `Dev`, ověřený celek následně do `Stage`. `Main` slouží k odevzdání: finální sloučení a přesun ticketů do `Done` následují až po týmové kontrole a připravení odevzdání. Uvedené automatické počty kontrol nejsou odpracované hodiny členů týmu.
