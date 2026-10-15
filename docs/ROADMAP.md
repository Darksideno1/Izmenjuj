# Izmenjuj.si – razvojni načrt

## Faza 0 – tehnična osnova
- ASP.NET Core 8 MVC
- PostgreSQL + EF Core
- Identity uporabniški model
- osnovni responsive UI
- domenski model za zbirke, predmete, slike, osebno evidenco in menjave
- začetni matching algoritem
- GitHub Actions build

## Faza 1 – uporabniški računi in zbirke
- registracija, prijava, odjava, pozabljeno geslo
- javno prikazno ime, lokacija in načini menjave
- katalog albumov/zbirateljskih serij
- podrobnosti zbirke, glavna slika in galerija
- predmeti z uradno ali interno oznako
- dodatne slike predmeta (spredaj/zadaj/spodaj)
- variante predmetov

## Faza 2 – Moja zbirka
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
- objektna hramba slik (S3 kompatibilno)
- potrjevanje e-pošte in ponastavitev gesla
- rate limiting, audit log, anti-spam
- varnostne kopije PostgreSQL in slik
- GDPR: zasebnost, izvoz in izbris računa
- staging + production deployment
- monitoring in health checks
