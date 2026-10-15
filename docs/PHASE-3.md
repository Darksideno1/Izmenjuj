# Faza 3 – pametno ujemanje

Implementirano:

- dejansko ujemanje na podatkih `UserInventory` iz MSSQL baze,
- obojestransko pravilo: uporabnik A ponuja nekaj, kar B išče, in B ponuja nekaj, kar A išče,
- razvrščanje kandidatov po kakovosti in uravnoteženosti menjave,
- predlog velikosti uravnotežene menjave `N ↔ N`,
- prikaz vseh uporabnih predmetov na obeh straneh,
- filter po zbirki,
- filter po mestu/kraju,
- filter po načinu menjave: pošta ali osebno,
- samodejno izločanje uporabnikov, ki nimajo nobenega skupnega načina menjave,
- oznaka za uporabnika iz istega kraja,
- neposredne povezave iz ujemanja na podrobnosti predmetov,
- responsive prikaz ujemanj,
- povezava na ujemanja iz glavne navigacije in domače strani.

## Razvrščanje

Ujemanje mora imeti vsaj en predmet v obe smeri. Največjo težo dobi število predmetov, ki jih je mogoče vključiti v uravnoteženo menjavo. Dodatne uporabne možnosti na obeh straneh pomagajo pri razvrščanju, velika neuravnoteženost pa rezultat nekoliko zniža.

## Zasebnost

Faza 3 uporablja samo podatke, ki so potrebni za iskanje menjave: javno prikazno ime, kraj in dovoljena načina menjave. E-poštni naslov se na strani ujemanj ne prikazuje.

## Naslednja faza

Faza 4 bo dodala strukturiran predlog menjave, sprejem/zavrnitev oziroma protipredlog, zasebna sporočila, zgodovino menjave in posodobitev osebne evidence po zaključku.
