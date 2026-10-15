# Faza 4 – menjave in komunikacija

Implementirano:

- ustvarjanje konkretnega predloga menjave iz pametnega ujemanja,
- izbira predmetov in količin na obeh straneh,
- ponovni strežniški pregled razpoložljivosti pred pošiljanjem in sprejemom,
- sprejem predloga,
- zavrnitev predloga,
- preklic lastnega zadnjega predloga,
- protipredlog z novo izbiro predmetov,
- zasebna sporočila znotraj posamezne menjave,
- zgodovina dogodkov menjave,
- stanje aktivnih in zaključenih menjav,
- dvostranska potrditev zaključka,
- atomska posodobitev osebne evidence po drugi potrditvi,
- zmanjšanje `Imam` in `Za menjavo` pri pošiljatelju,
- odstranitev `Iščem` in povečanje `Imam` pri prejemniku,
- zaščita pred dvojno obdelavo skoraj sočasnih potrditev,
- ocena 1–5 in neobvezen komentar po zaključeni menjavi,
- ohranjanje zgodovine predmetov, ki so že uporabljeni v menjavi,
- responsive prikaz predlogov, pogovora, zgodovine in ocen.

## Pravila zaključka

Menjava po sprejemu še ne spremeni osebne evidence. Oba uporabnika morata po dejansko izvedeni menjavi potrditi zaključek. Šele druga potrditev sproži transakcijsko posodobitev evidence obeh uporabnikov.

## Zasebnost pogovora

Sporočila so dostopna samo obema udeležencema konkretne menjave. E-poštni naslovi se v pogledu menjave ne razkrivajo.

## Baza

Faza 4 doda tabele `TradeMessages`, `TradeEvents` in `TradeRatings`. Za že ustvarjeno MSSQL bazo jih `DatabaseBootstrap` doda idempotentno; pri novi bazi jih EF model ustvari skupaj z ostalo shemo.

Ročno funkcionalno testiranje bo izvedeno na koncu razvoja celotne strani, po dogovoru z uporabnikom. Do takrat vsaka faza uporablja GitHub Actions build in Windows/IIS publish kot tehnično kontrolo.
