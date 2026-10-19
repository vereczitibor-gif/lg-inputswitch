# Külső források és licencek

| Forrás | Rögzített commit | Felhasználás | Licenc |
| --- | --- | --- | --- |
| [pyang2045/streamdeck-display-knob](https://github.com/pyang2045/streamdeck-display-knob) | `641c3105b2999062af93dece14e44beafbd15a61` | LG-kódok, protokollkutatás és 5 másodperces védelmi elv; a Stream Deck SDK-t nem használjuk | MIT, copyright 2026 pyang2045; `third_party/licenses/streamdeck-display-knob.txt` |
| [waydabber/m1ddc](https://github.com/waydabber/m1ddc) | `04d949794102eb8df01ad3681afff6464a3eede2` | Forrásból fordított Apple Silicon helper | MIT, copyright 2021 waydabber; `third_party/m1ddc/LICENSE` |
| [AMD display-library](https://github.com/GPUOpen-LibrariesAndSDKs/display-library) | `84c881cb2136f04287eb3af12de00a9e2867aa9e` | ADL ABI-definíciók, célzott DDCBlockAccess csomagformátum hivatalos mintája | A használt SDK-fejlécek MIT-licence; `third_party/licenses/AMD-ADL.txt` |
| [NVIDIA/nvapi](https://github.com/NVIDIA/nvapi) | `70d337db9186e968eab622f7e786de7e437faf3d` | NVAPI ABI-definíciók és hivatalos függvényazonosítók | MIT; `third_party/licenses/NVIDIA-NVAPI.txt` |

Az AMD/NVIDIA driverkönyvtárakat nem terjesztjük, a már telepített driver System32 könyvtárából töltjük be. Az AMD SDK teljes csomagját és mintaprogramját nem mellékeljük; a szükséges interfészdefiníciókat saját C#-kódra ültettük át a fejlécek licencének megőrzésével. A Windows önálló csomag Microsoft .NET futtatókörnyezetet tartalmaz; az SDK által a publish kimenetbe tett runtime-licencadatokat meg kell őrizni.

## Az m1ddc helyi módosításai

Az eredeti források a `third_party/m1ddc/` könyvtárban vannak, a licenc változatlan. A saját build eltérései:

1. Egyetlen írás tranzakciónként (`DDC_ITERATIONS=1`), hogy bemenetváltáskor ne legyen automatikus megismétlés.
2. A csomaghosszt a DDC hosszmezőből számítjuk (`(data[0] & 0x7F) + 2`), nem az utolsó nem nulla bájtból. Így a nulla ellenőrzőösszeg sem marad le.
3. A kijelzőstruktúrát inicializáljuk, és ismeretlen terméknévhez alapértéket adunk.
4. Numerikus monitorindexnél az alsó korlát is ellenőrzött (`>=1`). Az alkalmazás mindig explicit UUID-t ad át.
5. A stdout örökölt fájlleírójára `fputs`/`fflush` ír. A `/dev/stdout` újranyitása korlátozott környezetben hibát okozott; átirányított fájlnál csonkolhatta is a kimenetet.
6. A fordítási script `MAX_DISPLAYS=32` felső korlátot állít be, macOS 13 minimális célverzióval.

A packet-tesztek a módosított, tényleges `i2c.m` forrást linkelik, az IOAVService író/olvasó függvényét tesztstub váltja ki; a teszt nem érinti a hardvert.
