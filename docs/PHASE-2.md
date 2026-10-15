# Faza 2 – predmeti in osebna zbirka

Implementirano:

- predmeti znotraj zbirke,
- naziv, uradna oznaka/številka in stabilna interna oznaka,
- samodejno generiranje interne oznake za predmete brez uradne kode,
- variante predmetov,
- glavna slika predmeta,
- dodatna galerija slik,
- dodajanje, urejanje in brisanje predmetov za lastnika zbirke,
- vizualna mreža predmetov na strani zbirke,
- osebna evidenca `Imam / Iščem / Za menjavo`,
- količina primerkov in količina za menjavo,
- preverjanje, da količina za menjavo ne preseže količine `Imam`,
- množično urejanje celotne zbirke z enim shranjevanjem,
- prikaz napredka zbirke,
- statistika manjkajočih in predmetov za menjavo,
- responsive prikaz za oštevilčene albume in zbirke brez številk,
- čiščenje slik predmetov ob brisanju predmeta ali zbirke.

## Pravila osebne evidence

- `Imam` je količina primerkov predmeta, ki jih uporabnik poseduje.
- `Iščem` označuje predmet, ki ga uporabnik nima in ga želi pridobiti.
- `Za menjavo` je količina izmed posedovanih primerkov, ki jih ponuja za menjavo.
- Predmet ne more biti hkrati označen kot `Iščem` in imeti količino `Imam` večjo od 0.
- `Za menjavo` ne sme presegati količine `Imam`.

Naslednja faza je pametno obojestransko ujemanje uporabnikov in filtriranje kandidatov za menjavo.
