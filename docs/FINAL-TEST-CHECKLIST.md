# Končni ročni test – Izmenjuj.si

Ta kontrolni seznam je namenoma prestavljen na konec razvoja, ko bo zaključena tudi UI/UX prenova.

## A. Vizualni pregled

- [ ] zgornji meni in sidebar ustrezata potrjenemu vizualnemu konceptu,
- [ ] mobilni pogled ima pravo navigacijo in nikoli ne ostane brez menija,
- [ ] tipografija, razmiki, kartice in barve so konsistentni,
- [ ] slike predmetov so prikazane samo tam, kjer je to smiselno za zbirko,
- [ ] številčeni albumi lahko delujejo brez slik vsake sličice,
- [ ] figurice/variante lahko uporabljajo glavno sliko in galerijo,
- [ ] desktop, telefon in tablica so pregledni.

## B. Račun

- [ ] registracija pošlje potrditveni e-mail,
- [ ] nepotrjen račun se ne more prijaviti v produkciji,
- [ ] ponovna potrditvena povezava deluje,
- [ ] prijava/odjava delujeta,
- [ ] lockout po neuspešnih prijavah deluje,
- [ ] pozabljeno geslo pošlje e-mail,
- [ ] reset gesla deluje,
- [ ] profil se shrani.

## C. Zbirke in predmeti

- [ ] ustvari/uredi/skrij/objavi zbirko,
- [ ] odpri zbirko iz kataloga,
- [ ] dodaj/uredi predmet s sliko in brez slike,
- [ ] galerija slik deluje,
- [ ] množični vnos je idempotenten,
- [ ] združevanje dvojnikov ohrani evidence in zgodovino.

## D. Moja zbirka

- [ ] Imam / Iščem / Za menjavo se pravilno shranjuje,
- [ ] količina za menjavo ne preseže količine Imam,
- [ ] napredek zbirke je pravilen,
- [ ] množično urejanje deluje.

## E. Ujemanja in menjave

- [ ] obojestransko ujemanje je pravilno,
- [ ] filtri zbirka/kraj/pošta/osebno delujejo,
- [ ] jasno se vidi kaj uporabnik da in kaj dobi,
- [ ] predlog menjave deluje,
- [ ] protipredlog deluje,
- [ ] sprejem/zavrnitev/preklic delujejo,
- [ ] zasebna sporočila delujejo,
- [ ] e-poštna obvestila pridejo drugemu uporabniku,
- [ ] oba potrdita zaključek,
- [ ] inventar se spremeni samo enkrat,
- [ ] ocena menjave deluje.

## F. Moderacija in ADMIN

- [ ] ADMIN dostop deluje samo za Admin vlogo,
- [ ] pregled uporabnikov deluje,
- [ ] suspendiranje prekine dostop,
- [ ] blokirani uporabniki se ne ujemajo,
- [ ] prijava uporabnika in obravnava prijave delujeta,
- [ ] ADMIN read-only pregled menjave ne omogoča posega v uporabniški pogovor.

## G. Zasebnost

- [ ] izvoz osebnih podatkov prenese veljaven JSON,
- [ ] račun z aktivno menjavo se ne more izbrisati,
- [ ] izbris z napačnim geslom je zavrnjen,
- [ ] uspešen izbris odstrani prijavni račun in osebno evidenco,
- [ ] zgodovinske menjave ostanejo anonimizirane.

## H. Gostovanje Domenca.com

- [ ] MSSQL connection string je samo na strežniku,
- [ ] SMTP skrivnosti so samo na strežniku,
- [ ] `Admin__Email` je samo na strežniku,
- [ ] IIS ima write dovoljenje za `wwwroot/uploads`,
- [ ] `/health` vrne aplikacijo in bazo kot OK,
- [ ] HTTPS deluje,
- [ ] restart aplikacije ne izgubi podatkov/slik,
- [ ] backup MSSQL + uploads je ustvarjen,
- [ ] restore je preverjen na testni kopiji.
