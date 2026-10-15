# Izmenjuj.si – Windows gostovanje (ASP.NET Core 8 + MSSQL)

Ciljno produkcijsko okolje je klasično Windows gostovanje z naslednjimi predpostavkami:

- ASP.NET Core 8 / .NET 8
- IIS
- Plesk
- Microsoft SQL Server 2022 (MSSQL)
- možnost pisanja v mapo aplikacije `wwwroot/uploads`

## 1. Baza MSSQL

V Plesku ustvari MSSQL bazo in uporabnika baze. Nato pridobi connection string v obliki, ki jo poda ponudnik gostovanja.

Produkcijskega uporabniškega imena, gesla ali connection stringa ne zapisuj v GitHub.

Aplikacija pričakuje nastavitev:

`ConnectionStrings__DefaultConnection`

Če gostovanje ne omogoča okoljske spremenljivke, se connection string vnese v produkcijski `appsettings.Production.json` neposredno na strežniku in ta datoteka se ne commita v GitHub.

Ob prvem zagonu aplikacija preveri, ali tabela `dbo.AspNetUsers` obstaja. Če je baza prazna, ustvari začetne tabele.

## 2. Slike

Naslovnice in galerijske slike se shranjujejo v:

`wwwroot/uploads`

Aplikacijski pool/IIS mora imeti dovoljenje za zapisovanje v to mapo. Mapo `uploads` je treba vključiti v redne varnostne kopije skupaj z MSSQL bazo.

Dovoljeni formati so JPG, PNG in WEBP; največja velikost posamezne slike je trenutno 6 MB.

## 3. Publish

Za gostovanje se uporablja framework-dependent .NET 8 publish. GitHub Actions preveri build; za ročni publish lahko uporabiš:

`dotnet publish src/Izmenjuj.Web/Izmenjuj.Web.csproj -c Release -o publish`

Vsebino mape `publish` nato naložiš v spletni koren, ki ga določi Plesk. ASP.NET Core publish ustvari potrebno IIS konfiguracijo (`web.config`).

## 4. Plesk / IIS

Pri nastavitvi gostovanja preveri:

- aplikacija uporablja .NET 8,
- Application Pool je nastavljen za ASP.NET Core aplikacijo,
- HTTPS/SSL je vključen,
- `wwwroot/uploads` ima dovoljenje za zapis,
- domena kaže na pravo spletno mesto,
- produkcijski MSSQL connection string je nastavljen samo na strežniku.

## 5. Lokalni razvoj na Windows

Privzeta razvojna baza je SQL Server LocalDB:

`Server=(localdb)\\MSSQLLocalDB;Database=Izmenjuj;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`

To omogoča lokalni razvoj brez zunanjega strežnika. Alternativno je v `docker-compose.yml` pripravljen SQL Server 2022 container; geslo se poda prek okoljske spremenljivke `MSSQL_SA_PASSWORD`.

## Varnostne kopije

Za obnovljiv sistem je treba varnostno kopirati oba dela:

1. MSSQL bazo,
2. mapo `wwwroot/uploads`.

Baza brez slik oziroma slike brez baze niso popolna varnostna kopija platforme.
