# Faza 5 – ADMIN in moderacija

Faza 5 dodaja ločen administratorski sloj nad obstoječo aplikacijo. Uporabniški tokovi iz faz 1–4 ostanejo nespremenjeni, ADMIN pa uporablja vlogo ASP.NET Core Identity `Admin`.

## Implementirano

- ADMIN nadzorna plošča s ključnimi števci,
- varen administratorski dostop prek vloge `Admin`,
- konfiguracija prvega administratorja prek `Admin__Email` brez hardcodanega gesla,
- pregled vseh zbirk,
- iskanje in filtriranje zbirk,
- objava / skrivanje zbirke,
- ADMIN urejanje poljubne zbirke in predmeta prek obstoječih obrazcev,
- varno preprečevanje brisanja predmetov, ki so že uporabljeni v osebnih evidencah ali menjavah,
- množični vnos oštevilčenih predmetov v obliki `oznaka;naziv;varianta`,
- ponovljiv množični uvoz: obstoječa uradna oznaka se posodobi namesto podvoji,
- združevanje dveh podvojenih predmetov,
- pri združevanju se ohranijo osebne evidence, vrstice menjav in slike,
- pregled uporabnikov,
- iskanje po imenu, e-pošti in kraju,
- suspendiranje in ponovna aktivacija uporabnika,
- suspenzija invalidira obstoječo prijavno sejo in prepreči novo prijavo,
- administrator ne more suspendirati svojega ali drugega administratorskega računa iz običajnega pogleda,
- uporabniške prijave z razlogom, podrobnostmi in opcijsko povezavo na menjavo,
- obravnava prijave kot rešene ali zavržene,
- blokiranje in odblokiranje uporabnikov,
- blokirani uporabniki se ne pojavljajo v novih pametnih ujemanjih,
- nove menjave med blokiranima uporabnikoma so zavrnjene tudi na strežniški strani,
- suspendirani uporabniki se ne pojavljajo v ujemanjih,
- read-only ADMIN pregled vseh menjav,
- read-only pregled predmetov, dogodkov in zasebnih sporočil posamezne menjave za moderiranje,
- ADMIN iz read-only pogleda ne more spreminjati statusa ali odgovarjati v uporabniški pogovor,
- povezava na blokirane uporabnike v profilu,
- dejansko delujoče iskanje v glavnem katalogu po zbirki in predmetih,
- idempotenten MSSQL bootstrap za `UserBlocks`, `UserReports` in `UserModerations`,
- responsive ADMIN vmesnik.

## Pravila moderacije

- blokada vpliva na prihodnja ujemanja in nove predloge menjave,
- obstoječa menjava se ob blokadi ne izbriše in njena zgodovina ostane ohranjena,
- suspenzija blokira uporabo računa,
- ADMIN pregled menjave je namenjen podpori in obravnavi prijav ter je read-only,
- zbirke in predmeti, ki že sodelujejo v podatkovnih povezavah uporabnikov ali menjav, se ne brišejo na silo.

## Množični vnos

Privzeta vrstica:

`oznaka;naziv;varianta`

Varianta ni obvezna. Podprti ločilniki so podpičje, tabulator in `|`.

Primer:

```text
1;Lev
2;Tiger;Svetleča varianta
3;Slon
```

Če uradna oznaka že obstaja v isti zbirki, se naziv in varianta posodobita. To omogoča ponovljiv uvoz brez ustvarjanja novih dvojnikov.

## ADMIN konfiguracija

Produkcijsko nastavitev vpiši na strežniku:

`Admin__Email`

V GitHub se ne zapisuje administratorskega e-poštnega naslova, gesla ali drugih produkcijskih skrivnosti.

## Testiranje

Uporabnik je določil, da bo ročni end-to-end test celotne strani izveden šele po funkcionalnem zaključku projekta. Do takrat vsaka faza uporablja GitHub Actions za Restore, Release Build ter Windows/IIS Publish preverjanje.

Ročni scenariji Faze 5 se dodajo v končni testni seznam pred produkcijskim zagonom.
