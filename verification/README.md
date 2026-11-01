# Automatické ověření napsané AI

Ověřovací kód a skripty v této složce **napsala AI (Codex)** na pomoc s rychlejším hledáním chyb a opakováním kontrol po úpravách. Nejde o ruční přezkoušení spolužáky ani o jejich odpracované hodiny. Tým musí výsledky přečíst, pochopit očekávané chování a program sám vyzkoušet.

Z kořenové složky repozitáře:

```sh
bash verification/verify.sh
```

- `StorageChecks`: 50 kontrol skutečných dočasných souborů, vlastního formátu, poškození, záloh a konfliktů zápisů.
- `TournamentChecks`: 2 113 tvrzení o hráčích, turnajích, losování, výsledcích a tabulce.
- `IntegrationChecks`: 10 scénářů propojení programu, ukládání, vstupů a konkrétních výjimek.
- `build-projects.sh`: sestavení hlavního i UI projektu v obou pořadích.

Kontroly nepoužívají testovací balíčky, JSON ani XML. Pomocný otisk v `TournamentChecks` porovnává hodnoty stavu před a po operaci pomocí obyčejných řetězců; není to formát ukládání aplikace.

Podrobnosti: [ověření a ruční kontrola](../docs/overeni.md), [použití programu](../docs/pouziti-programu.md).
