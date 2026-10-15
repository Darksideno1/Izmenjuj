# Izmenjuj.si – razvojni načrt

## Faza 0 – tehnična osnova
- ASP.NET Core 8 MVC
- Microsoft SQL Server / MSSQL + EF Core
- Identity uporabniški model
- osnovni responsive UI
- domenski model za zbirke, predmete, slike, osebno evidenco in menjave
- začetni matching algoritem
- GitHub Actions build in hosting publish paket

## Faza 1 – uporabniški računi in zbirke
- registracija, prijava in odjava
- javno prikazno ime, lokacija in načini menjave
- katalog albumov/zbirateljskih serij
- podrobnosti zbirke, glavna slika in galerija
- dodajanje, urejanje in brisanje lastnih zbirk
- lokalna shramba slik za Windows/IIS gostovanje

## Faza 2 – predmeti in Moja zbirka
- predmeti z uradno ali interno oznako
- dodatne slike predmeta (spredaj/zadaj/spodaj)
- variante predmetov
- Imam / Iščem / Za menjavo
- količina dvojnikov
- hitro množično urejanje oštevilčenih albumov
- vizualna mreža za figurice in zbirke brez številk
- napredek zbirke

## Faza 3 – Pametno ujemanje
- obojestransko ujemanje potreb in viškov
- razvrščanje najboljših menjav
- filtri po albumu, lokaciji in načinu menjave
- predlog uravnotežene menjave

## Faza 4 – Menjave in komunikacija
- predlog / sprememba / sprejem / zavrnitev
- zasebna sporočila
- zgodovina menjave
- po zaključku možnost posodobitve osebne evidence
- ocena uporabnika

## Faza 5 – ADMIN
- CRUD albumov, predmetov in slik
- uvoz večjega števila oštevilčenih predmetov
- variante in združevanje podvojenih vnosov
- uporabniki, prijave vsebine in moderacija

## Faza 6 – produkcija
- Windows/IIS/Plesk deployment na ASP.NET Core 8
- MSSQL 2022 produkcijska baza
- potrjevanje e-pošte in ponastavitev gesla
- rate limiting, audit log, anti-spam
- varnostne kopije MSSQL baze in `wwwroot/uploads`
- po potrebi kasnejša objektna hramba slik
- GDPR: zasebnost, izvoz in izbris računa
- staging + production deployment
- monitoring in health checks
