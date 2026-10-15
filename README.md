# Izmenjuj.si

Sodobna spletna platforma za zbiratelje in izmenjavo sličic, kart, figuric ter drugih zbirateljskih predmetov.

## Cilj projekta

Izmenjuj.si omogoča uporabnikom, da vodijo svoje zbirke, označijo kaj imajo, kaj iščejo in kaj ponujajo za menjavo. Sistem nato samodejno poišče najboljša ujemanja med uporabniki.

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

Projekt je pripravljen za klasično Windows gostovanje z ASP.NET Core 8, IIS/Plesk in MSSQL 2022. Produkcijski connection string se nastavi na gostovanju in se ne shranjuje v GitHub.

## Razvojne faze

1. Osnova aplikacije, podatkovni model in UI sistem
2. Registracija/prijava in uporabniški profil
3. Albumi/zbirke in galerije slik
4. Predmeti, variante in osebna evidenca (imam / iščem / za menjavo)
5. Samodejno iskanje ujemanj
6. Predlogi menjav in zasebna sporočila
7. Administracija in moderacija
8. Obvestila, ocene, varnost in produkcijska namestitev

## Status

Faza 1 je implementirana. Projekt se nadaljuje na obstoječi kodi z MSSQL kot ciljno podatkovno bazo.
