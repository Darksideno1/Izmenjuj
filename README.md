# Izmenjuj.si

Sodobna spletna platforma za zbiratelje in izmenjavo sličic, kart, figuric ter drugih zbirateljskih predmetov.

## Cilj projekta

Izmenjuj.si omogoča uporabnikom, da vodijo svoje zbirke, označijo kaj imajo, kaj iščejo in kaj ponujajo za menjavo. Sistem samodejno poišče obojestranska ujemanja, uporabnika pa lahko iz ujemanja pripravita konkreten predlog menjave.

Projekt je zasnovan tudi za zbirke brez uradnega oštevilčenja: vsak predmet lahko uporablja naziv, interno oznako, glavno sliko in dodatno galerijo za vizualno identifikacijo.

## Tehnologija

- ASP.NET Core 8 MVC
- Entity Framework Core 8
- Microsoft SQL Server / MSSQL 2022
- ASP.NET Core Identity
- lokalna shramba slik v `wwwroot/uploads`
- Windows/IIS/Plesk produkcijsko gostovanje
- Responsive HTML/CSS/JavaScript
- GitHub Actions CI

## Lokalni razvoj

Na Windows računalniku je privzeta povezava nastavljena na SQL Server LocalDB (`MSSQLLocalDB`). Po želji se lahko uporabi tudi SQL Server 2022 prek `docker-compose.yml` ali lasten SQL Server.

## Ciljno gostovanje

Ciljno okolje je Domenca.com Windows gostovanje z .NET 8, IIS 10 / ASP.NET Core Module V2, Plesk in MSSQL 2022. Produkcijski connection string in administratorska konfiguracija se nastavita samo na gostovanju in se ne shranjujeta v GitHub.

## Razvojne faze

- **Faza 0 – osnova:** aplikacija, podatkovni model, UI sistem in CI.
- **Faza 1 – računi in zbirke:** registracija/prijava, profil, zbirke in galerije.
- **Faza 2 – predmeti in osebna zbirka:** variante, slike, Imam / Iščem / Za menjavo, količine in napredek.
- **Faza 3 – pametno ujemanje:** obojestranska ujemanja, rangiranje ter filtri.
- **Faza 4 – menjave in komunikacija:** predlogi, protipredlogi, zasebna sporočila, dvostranski zaključek, zgodovina in ocene.
- **Faza 5 – ADMIN in moderacija:** administracija kataloga/uporabnikov, množični vnos, združevanje dvojnikov, prijave, blokade, suspenzije in read-only pregled menjav.
- **Faza 6 – produkcijska priprava:** e-poštna potrditev/reset gesla, zaščite, backup/restore, zasebnost/GDPR, monitoring, končni test in namestitev.

## Status

Faze 0–5 so implementirane v razvojni osnovi. Ročni end-to-end test je po dogovoru odložen do funkcionalnega zaključka projekta; vsaka faza se vmes preverja z GitHub Actions Release buildom in Windows/IIS publishom.
