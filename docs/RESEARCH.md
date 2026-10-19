# Forrásellenőrzés és döntések

Ellenőrizve: 2026-10-18–19. Elsődleges forrásokat használtunk: a tényleges implementációk, a hardvergyártók SDK-i és az operációs rendszerek dokumentációja. A tesztelési állításokat elkülönítjük a saját környezetben megfigyelt eredményektől.

## A konkrét összeállítás

A felhasználó megerősítette: LG UltraFine **32U990A-S**, **M4 Mac mini macOS-szel/Thunderbolttal**, és **Mac Pro 6,1 Windowszal/HDMI-vel**. A [Mac Pro Late 2013 Apple-specifikációja](https://support.apple.com/en-mz/112025) két AMD FirePro D300, illetve választható D500/D700 GPU-t sorol. Ebből a gyári gépre AMD-backendet választottunk; a pontos FirePro-modell és a telepített Windows-driver verziója még nincs ellenőrizve.

## LG-protokoll

A [streamdeck-display-knob](https://github.com/pyang2045/streamdeck-display-knob/tree/641c3105b2999062af93dece14e44beafbd15a61) MIT-licencű. A [display-controller.ts](https://github.com/pyang2045/streamdeck-display-knob/blob/641c3105b2999062af93dece14e44beafbd15a61/streamdeck-plugin/src/display-controller.ts) és az [lg.sh](https://github.com/pyang2045/streamdeck-display-knob/blob/641c3105b2999062af93dece14e44beafbd15a61/lg.sh) közvetlenül az m1ddc `set input-alt` műveletét hívja: HDMI 1 = 144/0x90, Thunderbolt (USB-C slot) = 210/0xD2, DP1 = 208/0xD0. A szerző 32U990A-nál megbízhatatlan 0x60 olvasást és írásvédett/olvashatatlan 0xF4-et jelez, ezért feltételezett állapotot tárol és 5 másodperces védelmet használ. Ez szerzői hardvertapasztalat, nem LG-specifikáció és nem a saját S-példányon mért eredmény. Az InputSwitch kizárólag a küldött kérést jelzi, aktív bemenetet nem állít.

A döntő különbség az [m1ddc i2c.h](https://github.com/waydabber/m1ddc/blob/04d949794102eb8df01ad3681afff6464a3eede2/headers/i2c.h) és az [i2c.m](https://github.com/waydabber/m1ddc/blob/04d949794102eb8df01ad3681afff6464a3eede2/sources/i2c.m) forrásából igazolható: az `INPUT_ALT=0xF4` nem a normál `0x51`, hanem a `0x50` forráscímet kapja. Az I²C cél 7 biten `0x37` (8 bites íráscímként `0x6E`); az IOAVService külön paraméterben kapja a forráscímet és a hatbájtos törzset.

```text
Teljes írócsomag: 6E 50 84 03 F4 VH VL CC
VH/VL: 16 bites bemenetérték, big endian
CC = 6E XOR 50 XOR 84 XOR 03 XOR F4 XOR VH XOR VL
HDMI 1:           6E 50 84 03 F4 00 90 DD
Thunderbolt:      6E 50 84 03 F4 00 D2 9F
DisplayPort 1:    6E 50 84 03 F4 00 D0 9D
```

A `0x50` itt DDC forráscím; nem az EDID-eszköz 7 bites I²C-címe. Az alkalmazás egyetlen írást végez kérésenként, és megőrzi a csomag teljes hosszát akkor is, ha az ellenőrzőösszeg nulla. Mindkét saját backendhez vannak ismert bájtsorozatokat és az összes 16 bites értéket ellenőrző tesztek.

## macOS / Apple Silicon

Az [m1ddc README](https://github.com/waydabber/m1ddc/blob/04d949794102eb8df01ad3681afff6464a3eede2/README.md) Apple Siliconhoz, USB-C/DP Alt Mode-hoz és támogatott beépített HDMI-hez kínál megoldást; Intel Macet nem támogat. A forrás privát `IOAVServiceCreateWithService`, `IOAVServiceWriteI2C` és CoreDisplay hívásokat deklarál. A monitor szolgáltatását UUID/IORegistry alapján választja. A beépített MCDP29xx HDMI proxyhoz külön cím is van a kódban; a felhasználó Mac-oldali Thunderbolt kapcsolata a normál útvonal célja.

Ezért forrásból fordított és pontos commitra rögzített helper került a csomagba. A felsorolt privát hívásokhoz nincs az Apple nyilvános SDK-jában stabil kompatibilitási szerződés; a kompatibilitást a tényleges implementációból állapítottuk meg. OS-frissítés és különböző adapterek mellett fizikai ellenőrzés szükséges. A csak olvasó felderítés nem bizonyítja a monitorhoz eljutó DDC-írást.

A bejelentkezési indítást az Apple [SMAppService.mainApp](https://developer.apple.com/documentation/servicemanagement/smappservice/mainapp) objektuma kezeli. Az app ezt kizárólag a jelölő felhasználói módosításakor regisztrálja/törli; az állapotot az OS-ből olvassa. A hotkey a Carbon `RegisterEventHotKey` SDK-interfészét használja, globális billentyűfigyelés vagy Accessibility-hozzáférés nélkül. A helyi fordítás a telepített Apple SDK deklarációival ellenőrizte a hívásokat.

## Windows

A Microsoft [SetVCPFeature](https://learn.microsoft.com/en-us/windows/win32/api/lowlevelmonitorconfigurationapi/nf-lowlevelmonitorconfigurationapi-setvcpfeature) API-ja VCP-kódot és értéket fogad. Nincs benne forráscímet megadó paraméter; a dokumentáció szerint a DDC/CI Set VCP Feature parancsnak felel meg. Következtetés: a referencia nyers `0x50` csomagja nem állítható elő vele bizonyíthatóan. Az InputSwitch ezt csak a generikus `0x60` profilhoz használja. A Microsoft fizikai validálást kér, mert a monitorok MCCS-megvalósítása eltérhet.

A monitorok friss `EnumDisplayMonitors`/eszközinterfész-felderítéssel kapnak azonosítót. A [fizikai monitorok dokumentációja](https://learn.microsoft.com/en-us/windows/win32/api/physicalmonitorenumerationapi/nf-physicalmonitorenumerationapi-getphysicalmonitorsfromhmonitor) szerint egy HMONITOR több fizikai monitort is jelenthet, és a kapott handle-öket fel kell szabadítani. A generikus backend ezt megteszi, és kétértelmű topológiát elutasít. A [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) `MOD_NOREPEAT` opcióját használjuk; a sikertelen regisztráció hibát eredményez, F12-t nem engedünk választani.

### AMD / FirePro — a Mac Pro 6,1 útvonala

Az [AMD ADL hivatalos SDK](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/tree/84c881cb2136f04287eb3af12de00a9e2867aa9e) és különösen a [DDCBlockAccess mintakód](https://github.com/GPUOpen-LibrariesAndSDKs/display-library/blob/84c881cb2136f04287eb3af12de00a9e2867aa9e/Sample/DDCBlockAccess/DDCBlockAccessDlg.cpp) mutatja a teljes, nyolcbájtos Set VCP csomagot, benne a célcímmel és a forráscímmel. A minta normál csomagjának forráscímét `0x50`-re, VCP-kódját `0xF4`-re állítjuk, és újraszámítjuk az XOR-t.

Az [ADL2_Display_DDCBlockAccess_Get dokumentációja](https://gpuopen-librariesandsdks.github.io/adl/group__I2CDDCEDIDAPI.html) kifejezetten megengedi a küldést fogadás nélkül. `option=0`, `commandIndex=0`, fogadási hossz 0; így a driverbe nem mentünk helyreállítási parancsot. Saját ADL2 kontextust használunk. Az adaptert az OS display-nevéhez kötjük, majd csak az egyetlen aktív/logikailag hozzárendelt kijelző logikai indexét fogadjuk el. Több jelölt esetén nincs találgatás.

A `ADL_Display_WriteAndReadI2C` alacsonyabb szintű `iLine` mezője hardveres busz, amelyhez a nyilvános mezőleírás nem ad monitorhoz kötött leképezést. Ezért nem használtunk feltételezett buszszámot. A DDCBlockAccess célzott kijelzőindexet fogad. Az ADL2 API megléte és a FirePro driver tényleges működése a cél-Windows gépen még nincs futás közben igazolva.

### NVIDIA — külön választható profil

Az [NVIDIA nvapi.h](https://github.com/NVIDIA/nvapi/blob/70d337db9186e968eab622f7e786de7e437faf3d/nvapi.h) `NV_I2C_INFO_V3` struktúrája külön regisztercímet és adatot fogad. A dokumentáció szerint regisztercím megadásakor az adat közvetlenül, új restart nélkül követi azt. `i2cDevAddress=0x6E`, egy regiszterbájt `0x50`, majd a hatbájtos DDC-törzs kerül átadásra. A cél kijelzőhöz pontosan egy GPU és egyetlen output-bit szükséges. A [hivatalos interfésztáblából](https://github.com/NVIDIA/nvapi/blob/70d337db9186e968eab622f7e786de7e437faf3d/nvapi_interface.h) származnak a dinamikus függvényazonosítók. A gyári Mac Pro 6,1 ezt a profilt nem használja.

## Mi marad mérési kérdés?

Az S modell firmware-je elfogadja-e a kódokat; a FirePro driver átengedi-e az LG forráscímet; a nem kiválasztott bemenet DDC-útvonala és az OS-monitorobjektum megmarad-e; feléledés után helyreáll-e a kapcsolat. Ezekre egyik SDK dokumentációja vagy a referencia másik gépen végzett tesztje sem ad teljes választ. A [fizikai tesztlista](PHYSICAL-TESTS.md) rögzíti az elfogadási lépéseket.
