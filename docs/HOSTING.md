# Izmenjuj.si – Domenca.com Windows gostovanje

Ciljno produkcijsko okolje je Windows gostovanje Domenca.com. Ponudnik je potrdil okolje, ki je skladno z aplikacijo:

- Plesk Obsidian 18,
- Windows Server 2019,
- IIS 10,
- Microsoft SQL Server 2022 / MSSQL 16,
- ASP.NET Core,
- Microsoft ASP.NET Core Module V2,
- Microsoft .NET Core 8.0,
- Microsoft Web Deploy 4.0,
- FTP in Git for Windows.

Izmenjuj.si zato ostaja na **ASP.NET Core 8 + EF Core SQL Server + MSSQL 2022**. Prehod na ASP.NET Framework 4.8 ni potreben.

## 1. Baza MSSQL

V Plesku ustvari MSSQL bazo in uporabnika baze. Nato uporabi connection string, ki ga poda ponudnik gostovanja.

Produkcijskega uporabniškega imena, gesla ali connection stringa ne zapisuj v GitHub ali v klepet.

Aplikacija pričakuje nastavitev:

`ConnectionStrings__DefaultConnection`

Če Plesk za aplikacijo ne ponudi okoljske spremenljivke, se connection string vnese v produkcijski `appsettings.Production.json` neposredno na strežniku. Ta datoteka se ne commita v GitHub.

Ob prvem zagonu aplikacija preveri, ali tabela `dbo.AspNetUsers` obstaja. Če je baza prazna, ustvari začetno shemo. Pri obstoječi bazi trenutni bootstrap idempotentno doda manjkajoče tabele novejših faz.

## 2. Prvi ADMIN račun

Administratorski e-poštni naslov se ne zapisuje v repozitorij. Na strežniku nastavi:

`Admin__Email`

na e-pošto svojega uporabniškega računa Izmenjuj.si.

Postopek:

1. nastavi `Admin__Email` v Plesku oziroma v produkcijski konfiguraciji,
2. registriraj račun z isto e-pošto ali ponovno zaženi aplikacijo, če račun že obstaja,
3. obstoječi račun bo ob zagonu dodan v vlogo `Admin`,
4. če je bil račun že prijavljen pred dodelitvijo vloge, se odjavi in ponovno prijavi, da se osveži prijavna seja.

Geslo administratorja se nikoli ne zapisuje v konfiguracijo ali GitHub.

## 3. Slike

Naslovnice in galerijske slike se shranjujejo v:

`wwwroot/uploads`

IIS Application Pool mora imeti dovoljenje za zapisovanje v to mapo. Mapo `uploads` je treba vključiti v redne varnostne kopije skupaj z MSSQL bazo.

Dovoljeni formati so JPG, PNG in WEBP; največja velikost posamezne slike je trenutno 6 MB.

Pri dejanski namestitvi je treba v Plesku preveriti, da redeploy ne izbriše obstoječe mape `wwwroot/uploads`.

## 4. Publish in namestitev

Za Domenca.com uporabljamo framework-dependent .NET 8 publish, ker ima strežnik nameščen .NET 8 in ASP.NET Core Module V2.

GitHub Actions izdela artifact:

`izmenjuj-windows-hosting`

Ročni ukaz je:

`dotnet publish src/Izmenjuj.Web/Izmenjuj.Web.csproj -c Release -o publish`

Vsebino publish mape je mogoče namestiti z možnostmi, ki jih ponuja Domenca/Plesk, na primer Web Deploy ali FTP. ASP.NET Core publish vsebuje tudi `web.config`, ki ga uporablja IIS/ASP.NET Core Module.

Pri kasnejših nadgradnjah ne kopiraj stare produkcijske konfiguracije ali uporabniških slik iz GitHuba čez strežnik. Produkcijski connection string in `Admin__Email` ostaneta samo na strežniku, `wwwroot/uploads` pa se ohrani.

## 5. Plesk / IIS kontrolni seznam

Pred prvim javnim zagonom preveri:

- .NET 8 je izbrani runtime,
- ASP.NET Core aplikacija teče prek IIS / ASP.NET Core Module V2,
- HTTPS/SSL je vključen,
- `wwwroot/uploads` ima dovoljenje za zapis,
- domena kaže na pravo spletno mesto,
- produkcijski MSSQL connection string je nastavljen samo na strežniku,
- `Admin__Email` je nastavljen samo na strežniku,
- po publishu `/health` vrne uspešen odgovor.

## 6. Lokalni razvoj na Windows

Privzeta razvojna baza je SQL Server LocalDB:

`Server=(localdb)\\MSSQLLocalDB;Database=Izmenjuj;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`

To omogoča lokalni razvoj brez zunanjega strežnika. Alternativno je v `docker-compose.yml` pripravljen SQL Server 2022 container; geslo se poda prek okoljske spremenljivke `MSSQL_SA_PASSWORD`.

## 7. Varnostne kopije

Za obnovljiv sistem je treba varnostno kopirati oba dela:

1. MSSQL bazo,
2. mapo `wwwroot/uploads`.

Baza brez slik oziroma slike brez baze niso popolna varnostna kopija platforme.
