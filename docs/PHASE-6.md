# Faza 6 – produkcijska priprava

Faza 6 utrjuje obstoječo aplikacijo pred končnim vizualnim usklajevanjem, ročnim testom in namestitvijo na Domenca.com.

## Implementirano

- produkcijsko potrjevanje e-pošte prek ASP.NET Core Identity tokenov,
- ponovno pošiljanje potrditvene povezave,
- pozabljeno geslo in varna ponastavitev gesla,
- SMTP pošiljanje brez skrivnosti v repozitoriju,
- e-poštna obvestila za dogodke menjav in zasebna sporočila,
- rate limiting za prijavo/registracijo/reset gesla in občutljive uporabniške operacije,
- utrjen auth cookie za produkcijo,
- `/health` preverja tudi dosegljivost MSSQL baze,
- GDPR izvoz uporabniških podatkov v JSON,
- izbris računa z anonimizacijo zgodovinskih referenc,
- blokada izbrisa pri aktivni menjavi,
- dokumentiran backup/restore za MSSQL + `wwwroot/uploads`,
- produkcijski `appsettings.Production.json` samo z neobčutljivimi privzetimi vrednostmi,
- posodobljen Domenca/Plesk runbook.

## E-pošta

Produkcija uporablja `Identity__RequireConfirmedAccount=true`. SMTP skrivnosti se vnesejo na strežniku. Če je potrjevanje obvezno, SMTP pa ni konfiguriran, registracija namerno ne ustvari neuporabnega računa.

## GDPR izbris

Ob izbrisu se odstrani Identity račun in osebna inventarna evidenca. Zaključena zgodovina menjav se ne izbriše iz evidenc drugih uporabnikov; identifikator uporabnika se zamenja z anonimnim ID-jem. Aktivne menjave morajo biti pred izbrisom zaključene ali preklicane.

## Backup

Popoln backup vedno vsebuje MSSQL bazo in `wwwroot/uploads`. Produkcijska obnova se izvaja z orodji ponudnika/Pleska, ne z lastnim JSON restore mehanizmom.

## Namenoma še odprto

Vizualna prenova ni del Faze 6. GitHub issue #9 beleži obvezno uskladitev menija in celotnega UI s potrjeno vizualno smerjo ter opcijski prikaz slik predmetov glede na zbirko.

Ročni end-to-end test ostaja po dogovoru prestavljen na konec razvoja.
