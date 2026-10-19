# Ellenőrzési jegyzőkönyv

Környezet: Apple Silicon arm64 macOS 27.0.1, Apple Swift 6.4 / Xcode SDK, helyben a `work/` alá telepített .NET SDK 8.0.426. Munka: 2026-10-18–19. A tesztek nem küldenek bemenetváltó parancsot fizikai monitorra.

| Ellenőrzés | Eredmény és jelentése |
| --- | --- |
| Mac release build | Sikeres Swift/AppKit executable és Objective-C helper, valódi Carbon/ServiceManagement/IOAVService linkeléssel. Apple Silicon csomag. |
| Csomagellenőrzés | `.app` Info.plist érvényes; `codesign --verify --deep --strict` sikeres. Ad hoc aláírás, nem notarizált kiadás. |
| Swift tesztek | 4 teszt, 0 hiba: explicit monitor és érvényes konfiguráció, hotkey hibás értékek, monitorlista-parszolás automatikus helyettesítés nélkül, 5 másodperces védelem. |
| Natív packet-teszt | A tényleges vendorizált `i2c.m` kód mind a 65 536 bemenetértékét ellenőriztük; külön a pontos címzést/hosszt, nulla checksum megőrzését és az egyszeri írást. Az IOAVService-függvények tesztstubok; hardver nem kapott írást. |
| Mac UI | A beállítóablak AppKit felülete megnyílt; a szerkeszthető shortcut, monitor/profil/cél/kód, kézi gomb és kikapcsolt login-jelölő látható, elrendezése vizuálisan ellenőrizve. A teszt nem kattintott a váltásra vagy az automatikus indításra. |
| Valódi monitorfelderítés | A kész `.app --diagnose` olvasó útvonala sandboxon kívül sikeresen listázta az **LG ULTRAFINE** kijelzőt (GSM, vendor 0x1E6D, model 0x77E1). Ez IORegistry/CoreDisplay-felderítés, nem DDC-írási vagy GetVCP-választeszt. Korlátozott shell-sandboxon belül nem látszott külső kijelző; a normál natív appban látszott. |
| Windows build | `dotnet publish` sikeres, Windows x64 GUI PE32+ EXE, saját .NET runtime-mal. Fordítási hiba nem maradt. macOS-ről történt keresztfordítás. |
| C# tesztek | Sikeres, külső tesztcsomag nélküli konzolos futtatás macOS-en: az összes 16 bites LG-csomag, HDMI/TB ismert bájtsorozat, big endian, checksum, hotkey-validáció, konfiguráció, idővédelem, ADL/NVAPI 64 bites struktúraméretek/offsetek. Natív Windows API nem futott. |

## Nem ellenőrzött képességek

Nincs még fizikai bizonyíték az M4 mini → HDMI és Mac Pro/Windows/FirePro → Thunderbolt váltásra. Nem futott Windows UI-/hotkey-/driver- vagy ADL/NVAPI-hardverteszt, bejelentkezési indítási teszt, alvás/ébredéses ciklus, inaktív bemeneti elérhetőségi mérés, többmonitoros topológia vagy tíz oda-vissza ciklus. A Windows PowerShell csomagolóscriptjét PowerShell hiányában itt nem futtattuk; a .NET publish parancsot macOS-ről végrehajtottuk, a ZIP csomagot helyben összeállítottuk és ellenőriztük. Ezek pontos fizikai lépései: [PHYSICAL-TESTS.md](PHYSICAL-TESTS.md).

A hardver felderítése, a sikeres build és a mockolt transportteszt önmagukban nem jelentenek igazolt monitorvezérlést. Az alkalmazás ezért a sikeres API-visszatérést is csak elküldött kérésként jelzi.

## Helyi build során javított hibák

- A m1ddc `/dev/stdout`-újranyitása a korlátozott környezetben elnyelte a diagnosztikát; örökölt stdout-descriptorra írásra cseréltük.
- A régi m1ddc hosszbecslése nulla végbájtot kihagyhatott; a hosszmezőt használjuk, ezt külön teszt védi.
- A két alapértelmezett upstream írást egyszerire csökkentettük.
- A Swift 6.4 alapértelmezett buildmotorának dSYM-lépése a helyi sandboxban nem futott; a script a támogatott, de elavult `native` buildmotorral sikeresen buildel. A korlátozást és a jövőbeli migrációt a README jelzi.
