# LG InputSwitch 0.1.0

Kis, helyben futó menüsor-/tálcaalkalmazás, konfigurálható globális billentyűparanccsal és beállítóablakkal. Valódi DDC rendszerhívásokat használ. A fizikai bemenetváltás ezen a hardverpáron még nincs igazolva.

**Letöltés:** [Mac / Apple Silicon](https://github.com/vereczitibor-gif/lg-inputswitch/releases/download/v0.1.0/LGInputSwitch-macOS-arm64.zip) · [Windows x64](https://github.com/vereczitibor-gif/lg-inputswitch/releases/download/v0.1.0/LGInputSwitch-Windows-x64.zip) · [Kiadások és ellenőrzőösszegek](https://github.com/vereczitibor-gif/lg-inputswitch/releases). Az első kiadás előzetes, a fizikai tesztek még hátravannak.

A felhasználó által megadott célösszeállítás:

| Gép | Kapcsolat az LG UltraFine 32U990A-S monitorhoz | Alapértelmezett cél |
| --- | --- | --- |
| M4 Mac mini, macOS | Thunderbolt | HDMI 1, LG-kód **144** |
| Mac Pro 6,1, Windows | HDMI, gyári AMD FirePro | Thunderbolt/USB-C, LG-kód **210** |

Az S változaton a kódok és a FirePro/Boot Camp driver működése helyszíni ellenőrzést igényel. A modell a felhasználó közlése, a kódok a hivatkozott 32U990A-referenciából származnak. Részletek: [kutatás](docs/RESEARCH.md), [fizikai tesztlista](docs/PHYSICAL-TESTS.md), [elvégzett ellenőrzések](docs/VALIDATION.md).

## Gyors kipróbálás

### M4 Mac mini

1. A `outputs/LGInputSwitch-macOS-arm64.zip` fájlt bontsd ki; az `LG InputSwitch.app` alkalmazást másold az Applications mappába, majd indítsd el. A helyi build ad hoc aláírású, nincs Developer ID-val aláírva vagy notarizálva. A rendszer saját megnyitási jóváhagyását szükség esetén neked kell elvégezned.
2. Első indításkor megnyílik a beállítóablak. Később a két kijelzőt ábrázoló menüsorikon → **Beállítások…** nyitja meg.
3. Válaszd ki az **LG ULTRAFINE** monitort. A felderítés és a **Lista frissítése** csak olvas.
4. Protokoll: **LG 32U990A-S / 0xF4**. Cél: **HDMI 1**; a kód legyen **144**.
5. A shortcut alapból `Ctrl+Alt+S` (Macen **Control+Option+S**). Átírható például `Ctrl+Alt+F8`-ra, ha ütközik. **Mentés** után érvényesül. Foglalt kombinációnál hibát jelez, és megtartja a régi shortcutot.
6. A mentés nem vált bemenetet. A menü váltási pontja, az ablak **Váltási kérés** gombja és a shortcut a **legutóbb mentett** célra küld parancsot. Ezt csak akkor próbáld, ha a másik gép is készen áll, és kéznél van a monitor kézi bemenetválasztója.
7. Az **Indítás bejelentkezéskor** külön, azonnal alkalmazódó opció. Csak saját kérésedre kapcsold be, már a végleges helyére másolt alkalmazásnál. Alapból kikapcsolt.

### Mac Pro 6,1, Windows

1. Bontsd ki a `outputs/LGInputSwitch-Windows-x64.zip` csomagot egy végleges helyre, és indítsd a `LGInputSwitch.exe` fájlt. A mellékelt önálló csomag saját .NET-futtatókörnyezetet tartalmaz; külön .NET-telepítés nem kell. A csomag nincs Authenticode-aláírva.
2. A Windows AMD FirePro/Boot Camp driverének kell biztosítania az `atiadlxx.dll` könyvtárat. Az alkalmazás nem telepít és nem frissít drivert. Rendszergazdai futtatásra nincs tervezve.
3. A tálcaikon dupla kattintása vagy helyi menüjének **Beállítások…** pontja megnyitja a kis ablakot.
4. Válaszd ki a monitort, protokollnak az **LG / AMD FirePro (Mac Pro 6,1)** profilt, célnak **Thunderbolt / USB-C**-t (**210**).
5. Állítsd be a shortcutot, majd **Mentés**. Az **Indítás a Windowszal** jelölő külön, azonnal módosítja a saját felhasználó bejelentkezési indítását. A build és a normál első indítás ezt nem kapcsolja be.
6. A shortcut vagy a **Váltási kérés** a mentett célra küld. A tálcamenü állapota és a **Diagnosztika…** megmutatja a hibákat.

## Beállítások és használat

- **Monitor:** Macen rendszer-UUID, Windowson eszközinterfész-azonosító. Nincs „első LG” vagy másik monitorra történő automatikus visszaesés. Kábel/port/topológia-változás után frissítsd a listát és szükség esetén válassz újra.
- **Bemenet:** HDMI 1, Thunderbolt/USB-C, DisplayPort 1 előbeállítások vagy 0–65535 közötti egyedi decimális kód. A profil határozza meg a parancsformátumot. A generikus **VCP 0x60** profil más monitorokhoz való; az LG-specifikus csomagot nem helyettesíti.
- **Hotkey:** A–Z, 0–9, F1–F11; Ctrl/Alt/Shift és platformfüggően Cmd vagy Win. Legalább Ctrl, Alt vagy Cmd/Win szükséges. Macen a betűk fizikai ANSI billentyűpozíciókhoz kötődnek; a konkrét magyar/egyéb kiosztással is teszteld. A rendszer által fenntartott vagy más alkalmazás által elfogott kombinációk tényleges működését fizikailag ellenőrizni kell.
- **Váltási védelem:** legalább 5 másodperc két kérés között, párhuzamos írás nélkül. Egy kérés egy írás; hiba, időtúllépés vagy kábelváltás után sincs automatikus újraküldés.
- **Eredmény:** „Parancs elküldve; a fizikai váltás NINCS igazolva.” Az alkalmazás nem mutat feltételezett aktív bemenetet. A driver/API visszatérése nem bizonyítja a panel átkapcsolását. Időtúllépésnél az eredmény ismeretlen.
- **Billentyűzet/egér:** a program a videobemenetet vezérli. Az USB/Bluetooth eszközök hostváltásához külön KVM vagy saját átkapcsolási lehetőség kellhet.

## Diagnosztika és adatfájlok

A diagnosztika csak kijelző-/driveradatokat és helyi naplót olvas; nem küld VCP-írást vagy bemenet-olvasó DDC-kérést. A Mac ablak szövege kijelölhető/másolható; Windows alatt **Mentés fájlba…** is van. Az eszközazonosítók és sorozatszámok szerepelhetnek benne; megosztás előtt nézd át.

| Rendszer | Konfiguráció | Napló |
| --- | --- | --- |
| macOS | `~/Library/Application Support/LGInputSwitch/config.json` | ugyanitt `diagnostics.log` |
| Windows | `%LOCALAPPDATA%\LGInputSwitch\config.json` | ugyanitt `diagnostics.log`, legfeljebb egy `.old` forgatott napló |

A konfiguráció csak sikeres **Mentés** után íródik, a helyi napló mérete korlátozott. Nincs telemetria, felhő, hálózati vezérlés vagy háttérben küldött kapcsolóparancs. Macen az SMAppService állapotát, Windowsban a saját Run-bejegyzést olvassuk. Windows alatt a Feladatkezelő indítási tiltása vagy rendszerházirend ezt felülbírálhatja; az első bejelentkezéses tesztnél ezt is ellenőrizd.

Ha nem vált: nézd meg a naplót; ellenőrizd a mentett monitort, a bemenetkódot, a monitor DDC/CI lehetőségét, a közvetlen kábelt és Windows alatt az AMD-driver ADL-elérhetőségét. Ha a másik bemenetre váltva a gép már nem látja a monitort, ezen az útvonalon nem lehet megbízhatóan visszakapcsolni; a kézi OSD-választás marad a helyreállítás.

## Platformkorlátok

- **Mac:** macOS 13+, Apple Silicon. Az m1ddc privát IOAVService/CoreDisplay API-kat használ; macOS-frissítés vagy dokkoló megszakíthatja a kompatibilitást. A referencia M4 Pro/macOS 26 tesztje nem bizonyíték az M4 mini és a monitorod teljes működésére. Intel macOS-backend nincs ebben a kiadásban; a Mac Pro Windows-verziót használ.
- **Windows:** x64 Windows 10/11 technikai cél; AMD ADL-driver szükséges az LG/FirePro profilhoz. A Mac Pro 6,1 Windows-/driverkombinációja helyszíni tesztet igényel. Az OS/GPU gyártói támogatási státuszát ez az alkalmazás nem változtatja meg.
- **AMD:** a kiválasztott OS-kijelzőhöz pontosan egy ADL logikai adapter és azon pontosan egy aktív kijelző szükséges. Az első kiadás kétértelmű klón/MST/többkijelzős hozzárendelést hibával elutasít.
- **NVIDIA:** külön LG-profil nyers NVAPI I²C-írással, közvetlen NVIDIA-meghajtású kijelzőhöz. Ez a konkrét Mac Pro gyári konfigurációjához nem szükséges. Intel GPU-hoz nincs nyers LG-backend.
- A szabványos Windows Dxva2 `SetVCPFeature(0x60)` útvonal nem tudja a LG `0x50` forráscímét megadni; ezért nincs megtévesztő `SetVCPFeature(0xF4)` fallback.
- Az inaktív bemenetről elérhető DDC, az alvás/ébredés, a firmware, a KVM és a kábelek működése nem következik a sikeres fordításból. Lásd a fizikai tesztlistát.

## Fordítás forrásból

Nincsenek npm/Electron vagy online futási függőségek. A Mac UI Swift/AppKit, a Windows UI C#/WinForms. A Mac segédprogram MIT-licencű, forrásból mellékelt Objective-C m1ddc.

Macen Apple Command Line Tools/Xcode és Swift 5.9 vagy újabb szükséges:

```sh
bash scripts/test-macos.sh
bash scripts/build-macos.sh
```

A script Apple Siliconra készít ad hoc aláírt `.app`-ot és ZIP-et az `outputs/` mappába. A modulcache a projekt `work/` mappájában marad. A script a régi SwiftPM `native` buildmotort használja, amely Swift 6.4-ben még elérhető, de elavult; újabb eszközkészletnél ezt szükség lehet átállítani.

Windows PowerShellben .NET 8 SDK-val:

```powershell
dotnet run --project windows/Tests -c Release
powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1
```

Az utolsó parancs csak az adott PowerShell-folyamat scriptfuttatását engedélyezi, nem módosít tartós rendszerházirendet. A .NET restore internetet igényelhet. macOS-ről is keresztfordítható a Windows alkalmazás:

```sh
dotnet publish windows/LGInputSwitch/LGInputSwitch.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o outputs/windows-x64
```

Csak olvasó parancssori felderítés a kész Mac-csomaggal:

```sh
"outputs/LG InputSwitch.app/Contents/MacOS/LGInputSwitch" --diagnose
```

Windows belső segédfolyamatok: `--worker-list`, `--worker-diagnose` csak olvasnak. A `--worker-switch` kifejezetten író fejlesztői belépési pont; normál használatban kizárólag a váltógomb/hotkey indítja, a mentett konfiguráció pillanatképével. A DDC-driverhívások külön folyamatban, 10 másodperces időkorláttal futnak, hogy egy elakadt driver ne fagyassza meg az ablakot. Macen a helper időkorlátja 8 másodperc.

## Eltávolítás

Először kapcsold ki az automatikus indítást a beállítóablakban, majd lépj ki a menüből. Ezután törölhető az app/csomagmappa, és igény szerint a fenti konfiguráció-/naplómappa. Windowsban az opcionális indítási érték a saját felhasználó `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\LGInputSwitch` bejegyzése; Macen az alkalmazás `SMAppService.mainApp` login itemét használjuk.

## Licenc

Saját kód: [MIT](LICENSE). A referencia, az m1ddc, az AMD ADL- és az NVIDIA NVAPI-definíciók licencadatai és pontos verziói: [THIRD-PARTY.md](docs/THIRD-PARTY.md). A megőrzött licencek a bináris csomagokban is szerepelnek.
