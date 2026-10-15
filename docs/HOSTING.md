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
- FTP in Git for Windows,
- MailEnable Professional 10.

Izmenjuj.si zato ostaja na **ASP.NET Core 8 + EF Core SQL Server + MSSQL 2022**. Prehod na ASP.NET Framework 4.8 ni potreben.

## 1. Baza MSSQL

V Plesku ustvari MSSQL bazo in uporabnika baze. Nato uporabi connection string, ki ga poda ponudnik gostovanja.

Produkcijskega uporabniškega imena, gesla ali connection stringa ne zapisuj v GitHub ali v klepet.

Aplikacija pričakuje nastavitev:

`ConnectionStrings__DefaultConnection`

V repozitoriju je lahko samo neobčutljiva produkcijska konfiguracija. Gesla in connection string se nastavijo kot okoljske spremenljivke v Plesku oziroma neposredno v strežniški konfiguraciji, ki ni v GitHubu.

Ob prvem zagonu aplikacija preveri, ali tabela `dbo.AspNetUsers` obstaja. Če je baza prazna, ustvari začetno shemo. Pri obstoječi bazi trenutni bootstrap idempotentno doda manjkajoče tabele novejših faz.

## 2. Prvi ADMIN račun

Administratorski e-poštni naslov se ne zapisuje v repozitorij. Na strežniku nastavi:

`Admin__Email`

na e-pošto svojega uporabniškega računa Izmenjuj.si.

Postopek:

1. nastavi `Admin__Email`,
2. registriraj račun z isto e-pošto ali ponovno zaženi aplikacijo, če račun že obstaja,
3. obstoječi račun bo ob zagonu dodan v vlogo `Admin`,
4. če je bil račun že prijavljen pred dodelitvijo vloge, se odjavi in ponovno prijavi.

Geslo administratorja se nikoli ne zapisuje v konfiguracijo ali GitHub.

## 3. E-pošta / SMTP

Produkcija zahteva potrjen e-poštni naslov uporabnika (`Identity__RequireConfirmedAccount=true`). Za registracijo, reset gesla in obvestila o menjavah mora biti nastavljen SMTP.

Aplikacija uporablja naslednje nastavitve:

- `Email__FromAddress` – priporočeno `noreply@izmenjuj.si`,
- `Email__FromName` – `Izmenjuj.si`,
- `Email__Smtp__Host`,
- `Email__Smtp__Port`,
- `Email__Smtp__EnableSsl`,
- `Email__Smtp__Username`,
- `Email__Smtp__Password`,
- `App__PublicBaseUrl` – `https://izmenjuj.si`.

Natančen SMTP host, vrata, TLS in prijavne podatke vzemi iz Domenca/Plesk nastavitev poštnega predala. Gesla ne pošiljaj v klepet in ga ne committaj.

Če je potrjevanje računov vključeno, SMTP pa ni konfiguriran, aplikacija registracijo zavrne, da ne ustvari računa, v katerega se uporabnik ne more prijaviti.

## 4. Slike

Naslovnice in galerijske slike se shranjujejo v:

`wwwroot/uploads`

IIS Application Pool mora imeti dovoljenje za zapisovanje v to mapo. Mapo `uploads` je treba vključiti v redne varnostne kopije skupaj z MSSQL bazo.

Dovoljeni formati so JPG, PNG in WEBP; največja velikost posamezne slike je trenutno 6 MB.

Slika predmeta je podatkovno **opcijska**. Pri končni UI prenovi bo prikaz slik določen glede na tip oziroma nastavitev zbirke; zbirke brez potrebe po vizualni identifikaciji ne smejo biti prisiljene prikazovati slik ali placeholderjev povsod.

Pri dejanski namestitvi je treba v Plesku preveriti, da redeploy ne izbriše obstoječe mape `wwwroot/uploads`.

## 5. Publish in namestitev

Za Domenca.com uporabljamo framework-dependent .NET 8 publish, ker ima strežnik nameščen .NET 8 in ASP.NET Core Module V2.

GitHub Actions izdela artifact:

`izmenjuj-windows-hosting`

Ročni ukaz je:

`dotnet publish src/Izmenjuj.Web/Izmenjuj.Web.csproj -c Release -o publish`

Vsebino publish mape je mogoče namestiti z možnostmi, ki jih ponuja Domenca/Plesk, na primer Web Deploy ali FTP. ASP.NET Core publish vsebuje tudi `web.config`, ki ga uporablja IIS/ASP.NET Core Module.

Pri kasnejših nadgradnjah ne prepiši produkcijskih skrivnosti ali uporabniških slik. `wwwroot/uploads` mora ostati trajna podatkovna mapa.

## 6. Plesk / IIS kontrolni seznam

Pred prvim javnim zagonom preveri:

- .NET 8 je izbrani runtime,
- ASP.NET Core aplikacija teče prek IIS / ASP.NET Core Module V2,
- HTTPS/SSL je vključen,
- `wwwroot/uploads` ima dovoljenje za zapis,
- domena kaže na pravo spletno mesto,
- MSSQL connection string je nastavljen samo na strežniku,
- `Admin__Email` je nastavljen samo na strežniku,
- SMTP nastavitve so vpisane samo na strežniku,
- `App__PublicBaseUrl=https://izmenjuj.si`,
- `Identity__RequireConfirmedAccount=true`,
- po publishu `/health` vrne `status=ok` in `database=ok`.

## 7. Lokalni razvoj na Windows

Privzeta razvojna baza je SQL Server LocalDB:

`Server=(localdb)\\MSSQLLocalDB;Database=Izmenjuj;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`

Lokalno potrjevanje e-pošte ni obvezno. Produkcijska nastavitev ga vključi posebej.

Alternativno je v `docker-compose.yml` pripravljen SQL Server 2022 container; geslo se poda prek okoljske spremenljivke `MSSQL_SA_PASSWORD`.

## 8. Backup in restore

Popolna varnostna kopija ima dva neločljiva dela:

1. **MSSQL baza**, ki vsebuje račune, zbirke, evidence, menjave, sporočila in moderacijo,
2. **`wwwroot/uploads`**, ki vsebuje uporabniško naložene slike.

Produkcijski backup se dela z orodjem ponudnika/Pleska za MSSQL oziroma z njihovim podprtim izvozom baze ter ločeno kopijo `wwwroot/uploads`. Ne uporabljamo lastnega nepreverjenega JSON restore postopka za produkcijsko bazo.

Priporočen postopek pred vsako večjo nadgradnjo:

1. ustvari svež backup MSSQL,
2. skopiraj/zipaj `wwwroot/uploads`,
3. zabeleži commit oziroma verzijo aplikacije,
4. šele nato izvedi publish,
5. preveri `/health`, prijavo in eno bralno stran.

Postopek obnove:

1. začasno ustavi aplikacijo ali onemogoči javni dostop,
2. obnovi MSSQL backup,
3. obnovi ujemajočo se kopijo `wwwroot/uploads`,
4. namesti aplikacijski build, ki ustreza času backupa,
5. zaženi aplikacijo in preveri `/health`,
6. nato nadgradi na novejšo verzijo po običajnem postopku.

Restore se prvič preveri na testni/staging kopiji, ne neposredno na edini produkcijski bazi.

## 9. Monitoring in varnost

- `/health` preverja tudi dejansko povezavo do MSSQL in ob težavi vrne HTTP 503,
- prijava, registracija, reset gesla in občutljive operacije imajo omejevanje zahtevkov,
- produkcijski auth cookie je `HttpOnly` in `Secure`,
- po šestih neuspelih prijavah se račun začasno zaklene,
- skrivnosti niso del GitHub repozitorija.
