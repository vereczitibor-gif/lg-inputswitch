# Fizikai elfogadási tesztek

Ezeket az ellenőrzéseket a célhardveren kell elvégezni. A fejlesztés nem hajtotta végre a bemenetváltásokat és nem kapcsolta be az automatikus indítást.

Jegyezd fel: dátum, monitor hátlapi típusa/firmware-e, kábelek/dokkolók, macOS/Windows-verzió, FirePro-modell és driververzió. Célpár: M4 Mac mini / Thunderbolt ↔ Mac Pro 6,1 / Windows / HDMI, LG 32U990A-S. A monitor kézi bemenetválasztója legyen elérhető.

| Ellenőrzés | Lépések és elvárt eredmény | Eredmény |
| --- | --- | --- |
| Modell és port | OSD/hátlapi címke alapján 32U990A-S; a HDMI és TB kábelek valóban a megadott gépekhez mennek. | ☐ |
| Felderítés mindkét gépen | Aktív bemeneten frissítsd a monitorlistát. Az LG választható, azonosítója megmarad újraindítás után. Macen a csak olvasó LG-felderítés már sikerült a fejlesztés során. | ☐ |
| AMD/Boot Camp | Windows Diagnosztika: `atiadlxx.dll`, ADL adapter, kijelzőnév és egyértelmű aktív logikai kijelző megjelenik. Nem kell ismeretlen adapter-/buszszámot megadni. | ☐ |
| Első Mac → HDMI | Macen LG-profil, HDMI1/144; mentés után egyetlen kézi kérés. Ellenőrizd az OSD-n és a képen, hogy Windows látható. A program csak elküldött parancsot jelezhet. | ☐ |
| Windows → TB | Windowsban AMD LG-profil, TB/210; egyetlen kérés. Az M4 képe és a TB bemenet jelenjen meg. | ☐ |
| Kódok igazolása | A 144/210 értéket az S-példányon külön erősítsd meg. Ha nem működik, őrizd meg a hibakódot; ismeretlen VCP-ket/értékeket ne próbálgass automatikusan. A DP1/208 csak akkor tesztelendő, ha azt ténylegesen használod. | ☐ |
| Oda-vissza tízszer | Mindkét irányban 10 ciklus, legalább 5 másodperc várakozással. Ne legyen elakadt kézrázás, tartós fekete képernyő vagy rossz monitorra küldés. | ☐ |
| Inaktív bemenet: Mac | HDMI aktív állapotban a Mac tovább látja-e az LG-t, és az onnan kezdeményezett kérés eljut-e? Ha a kijelző offline, dokumentáld a korlátot. | ☐ |
| Inaktív bemenet: Windows | TB aktív állapotban a Windows/FirePro tovább látja-e a kijelzőt és a DDC-útvonalat? Ha eltűnik, nincs igazolt szoftveres visszaváltás ebből az állapotból. | ☐ |
| Alvás/ébredés | Mindkét gépet külön altasd/ébreszd, aktív és inaktív bemenet mellett is. Ébredés után a következő kérés friss felderítést használjon. Szükség esetén lista frissítése; soha ne válasszon automatikusan más monitort. | ☐ |
| Kábel ki/be, portcsere | Választott monitor leválasztva → érthető hiba. Visszadugás után azonosító egyezés vagy tudatos újrakiválasztás. Másik monitor ne kapjon parancsot. | ☐ |
| Globális hotkey | Ctrl+Alt+S más app előterében, magyar kiosztáson is. Macen Control+Option. Windowsban AltGr-ütközést is ellenőrizd. Gomb nyomva tartása/gyors ismétlése ne küldjön ismételt kéréseket 5 másodpercen belül. | ☐ |
| Hotkey átírása | Mentett új kombináció működik; régi megszűnik. Más appban foglalt kombináció mentése hibát jelez, a régi shortcut megmarad. Rendszer által elfogott hotkeynél másikat kell választani. | ☐ |
| Automatikus indítás — csak választásra | Bekapcsolás után kijelentkezés/bejelentkezés: az ikon megjelenik, bemenetváltás nélkül. Kikapcsolás után következő bejelentkezéskor nem indul. Macen a végleges Applications helyről teszteld; OS-jóváhagyási állapotot is nézd meg. | ☐ |
| Több monitor/klón | Külön monitor tudatos kiválasztása; AMD/NVIDIA kétértelmű topológiánál az első kiadás elutasít. Klón/MST esetén ne állíts működő támogatást pusztán a listázásból. | ☐ |
| Hibák és helyreállítás | DDC tiltva/driverhiba/időtúllépés → látható hiba vagy ismeretlen eredmény, automatikus újrapróba nélkül. Helyreállítás a monitor kézi OSD-jével. | ☐ |
| USB/billentyűzet | Mindkét gépen elérhető billentyűzet biztosítja a visszaváltást. A videobemenet-váltás önmagában nem bizonyít USB/KVM-hostváltást. | ☐ |

Egy tesztet csak a monitor OSD-jének és/vagy a megjelenő képnek a megfigyelésével jelölj sikeresnek. A naplóban szereplő elküldés nem fizikai bizonyíték.

## Mérési napló sablon

```text
Dátum / tesztelő:
Monitor modell / firmware:
Mac mini macOS-verzió / kábel:
Mac Pro Windows-verzió / FirePro / driver:
Irány / profil / bemenetkód:
Aktív vagy inaktív forrásról indult:
API eredmény / hibakód:
OSD tényleges bemenet / megjelenő gép:
Siker / hiba / visszaállítás módja:
```
