# Teknisk design

Version 0.3 — fas 0. Unity **6000.3.22f1** (Unity 6.3 LTS), URP, C#.

---

## 0. Utvecklingsmiljö

Engine-versionen är **pinnad till 6000.3.22f1** i `ProjectSettings/ProjectVersion.txt`. Unity 6.3 LTS har support till december 2027, vilket täcker hela produktionen och en bit därefter.

Patchuppgraderingar inom 6000.3 är ofarliga. **Byte av major-version under fas 1–4 kräver uttryckligt beslut och en omkörning av determinismtesterna** — en engine-uppgradering mitt i innehållsproduktion kan förskjuta flyttalsberäkningar i generatorn, och då blir 1000 banor plötsligt 1000 *andra* banor. Det är också skälet till att `Arna.Gen` hålls fri från engine-beroenden: ju mindre av generatorn som rör Unity, desto mindre kan en uppgradering rubba.

**Krav på utvecklingsmaskinen**

| Komponent | Behövs för |
|---|---|
| Unity 6000.3.22f1 | — |
| Windows Standalone + IL2CPP | Daglig iteration under fas 1 |
| Git | Versionshantering |
| **Android Build Support** (+ SDK/NDK, OpenJDK) | Prestandaverifiering, fas 1 slut |
| **iOS Build Support** | Prestandaverifiering, fas 1 slut |

Android-modulen ska installeras med sina medföljande SDK/NDK/OpenJDK-komponenter. Sätt inte `ANDROID_HOME` eller `JAVA_HOME` manuellt mot en annan JDK — maskinen har redan en fristående Adoptium JDK 21 i `JAVA_HOME`, och att låta Unity plocka upp den i stället för sin egen är en klassisk källa till svårdiagnostiserade build-fel.

**iOS på Windows:** modulen låter oss bygga ut ett Xcode-projekt, men kompilering, signering och distribution till App Store kräver en Mac. Det behövs inte förrän fas 1 ska verifieras på iOS-hårdvara, men det måste finnas i planen — antingen en fysisk Mac eller en molnbyggtjänst.

Under fas 1 räcker **Windows Standalone** för all daglig iteration. Mobilbuildar behövs först vid prestandaverifieringen i slutet av fasen.

---

## 1. Prestandabudget

Budgeten är ett designkrav, inte ett optimeringsmål i efterhand. 1000 banor betyder att generatorn aldrig får producera något som spränger den.

| Mätvärde | Mellanklass (mål) | Lågklass (golv) |
|---|---|---|
| Bildfrekvens | 60 fps | 30 fps |
| Draw calls | ≤ 150 | ≤ 100 |
| Trianglar synliga | ≤ 250 k | ≤ 150 k |
| Aktiva skinnade meshar | ≤ 40 | ≤ 24 |
| Minne (managed + textures) | ≤ 700 MB | ≤ 450 MB |
| Banladdningstid | ≤ 2 s | ≤ 3.5 s |
| APK/IPA initial storlek | ≤ 150 MB | — |

**Konsekvenser för renderingen**

- URP mobilprofil. En riktad ljuskälla, bakad belysning där det går, inga realtidsskuggor på lågklass.
- Stiliserad low-poly med flat shading och en delad texturatlas per biom — får hela vegetationen att batcha.
- **GPU instancing** för all vegetation och klippor. Träd är instanser, aldrig individuella GameObjects med egna material.
- Enheter delar rigg och material per fraktion. LOD på 25 m / 45 m.

---

## 2. Arkitektur: simulering skild från presentation

Detta är arkitekturens viktigaste beslut och allt annat följer av det.

```
┌─────────────────────────────────────────────┐
│ SIM-lager   fast tidssteg 20 Hz             │
│ ren C#, inga UnityEngine-beroenden          │
│ deterministisk, seedstyrd                   │
│                                             │
│  BattleSim · UnitState · EnemyState         │
│  CaravanState · DetectionGrid · PathRunner  │
└──────────────────┬──────────────────────────┘
                   │ läser tillstånd, interpolerar
┌──────────────────▼──────────────────────────┐
│ VIEW-lager  variabelt, Update()             │
│ MonoBehaviours, animation, VFX, ljud, UI    │
│                                             │
│  UnitView · CaravanView · CameraRig · HUD   │
└─────────────────────────────────────────────┘
```

Sim-lagret får **inte** referera `Transform`, `Time.deltaTime`, `UnityEngine.Random` eller fysik. Det gör att vi får:

- **Determinism** — samma seed + samma indata ⇒ samma utfall, varje gång.
- **Hastighetskontroll gratis** — 2× och 4× är bara fler sim-steg per frame. Ingen extra kod.
- **Omspel** — spara seed + spelarens indata, återuppspela hela banan från 200 byte.
- **Testbarhet** — hela striden kan köras i ett enhetstest utan att starta Unity-scenen.
- **Serververifiering senare** om vi någonsin behöver det för leaderboards.

Presentationslagret interpolerar mellan två sim-tillstånd så att 20 Hz simulering ser ut som 60 fps rörelse.

---

## 3. Determinism

Regler som gäller undantagslöst i `Sim`-assemblyt:

1. All slump via en injicerad `DeterministicRandom` (xorshift128), aldrig `UnityEngine.Random`.
2. Seed per bana: `seed = chapter * 1000 + level`. Kapitel 7, bana 3 ⇒ `7003`.
3. Ingen iteration över `Dictionary` eller `HashSet` där ordningen påverkar utfallet — använd sorterade listor.
4. Inga flyttalsberoenden på plattformsspecifika matematikfunktioner i genereringen. Fixed-point (`int` i 1/1000-enheter) övervägs om driftsproblem uppstår mellan iOS och Android.

Ett automatiserat editortest genererar samma bana 100 gånger och jämför en hash av resultatet.

---

## 4. Data-driven design

All balansering ligger i `ScriptableObject`-tillgångar, aldrig hårdkodad. En designer ska kunna justera spelet utan att kompilera.

| Asset | Innehåll |
|---|---|
| `UnitTypeDef` | Kostnad, modellantal, HP, DPS, räckvidd, sikt, terrängmodifierare, positionspreferens |
| `EnemyDef` | HP, DPS, hastighet, `detectRadius`, gruppstorlek, målprioritet, beteendetyp |
| `TrapDef` | Skada, form (punkt/linje/radie), effekt, avslöjningsradie |
| `BiomeDef` | Tilepalett, prefablistor, väderalternativ, terrängfördelning, tillåtna fiender och fällor |
| `ChapterRecipe` | Biom, fiendepoängbudget, fälltäthet, ruttlängd, väder, specialregler |
| `TerrainTypeDef` | Hastighet, siktmultiplikator, bakhållsvikt, fälltäthet, särskilda regler |

Terrängmatrisen i `GDD.md` §3.2 finns som `TerrainTypeDef`-assets — tabellen i dokumentet är beskrivningen, assetsen är sanningen.

---

## 5. Kartrepresentation

**Inte Unity Terrain.** Den är för minneshungrig och för långsam att bygga vid runtime på mobil.

- Rutnät `64 × 64`, en `TileData` per ruta: terrängtyp, höjd, dekorationsindex.
- Vid banstart byggs **meshchunks om 16×16 tiles** (16 chunks totalt). Varje chunk är en mesh med vertexfärg per terrängtyp — ger mjuka övergångar utan splatmaps.
- Dekoration (träd, stenar) placeras deterministiskt från tileseed och renderas med `Graphics.DrawMeshInstanced`.
- Höjd är låg och mest kosmetisk; framkomlighet avgörs av terrängtyp, inte lutning. Det håller ruttvalideringen enkel och begriplig för spelaren.

---

## 6. Rutt och rörelse

**Ruttgenerering**

1. Spelaren sätter 5–6 waypoints (skärmtryck → raycast mot markplanet → närmaste tile).
2. Mellan varje waypointpar körs **A\* på rutnätet** med terrängkostnad som vikt. Det ger en väg som naturligt följer terrängen i stället för att skära rakt igenom kärr.
3. Resultatet jämnas ut till en Catmull-Rom-spline och projiceras på höjdkartan. Egen implementation — ett paketberoende för tjugo rader kod är inte värt det.
4. Validering: om A\* inte hittar väg mellan två waypoints markeras segmentet rött och start blockeras.

Att köra A\* mellan waypoints — i stället för att dra en rak linje — är det som gör att spelarens grova streck blir en trovärdig färdväg.

**Mätt kostnad.** `GridPathfinder` löser värsta fallet — hörn till hörn över en brusig 64×64-karta — på **1,4 ms** på utvecklingsmaskinen. Verkliga segment mellan två waypoints är betydligt kortare och därmed billigare, men en full omräkning av alla fem segmenten på varje frame under drag håller inte på mobil. Därför:

- Räkna bara om de **två segment som gränsar till den waypoint som dras**, inte hela rutten.
- Strypa omräkningen till ~10 Hz under drag, med en full lösning när fingret släpps.

Mätningen görs om på fysisk hårdvara i slutet av fas 1.

**Enhetsrörelse**

- **Ingen navmesh-bakning vid runtime.** För dyrt på mobil.
- Trupper håller sin position relativt karavanen med enkel styrning (seek + separation).
- Vid strid: rör sig mot målet med samma styrning, begränsad av leash på 10 m från posten.
- Fiender använder grid-A\* mot karavanen, omräknat var 0.5 s, inte varje steg.

---

## 7. Detektion

Systemet som bär hela fog-of-war-mekaniken.

- **Spatial hash-grid** med cellstorlek 8 m. Alla fiender, fällor och trupper registreras.
- Uppdateras **4 gånger per sekund** (var femte sim-steg), inte varje frame. Detektion behöver inte vara mer exakt än så och kostnaden faller med 80 %.
- Två oberoende frågor per uppdatering:
  - *Vaknar fienden?* — avstånd karavan → fiende < `enemy.detectRadius`
  - *Ser spelaren fienden?* — avstånd närmaste trupp → fiende < `unit.sightRadius`
- Inga raycasts för sikt i fas 1. Terrängens siktmultiplikator (`TerrainTypeDef.sightMultiplier`) skalar `sightRadius` i stället — billigare och lättare för spelaren att förutsäga.

En upptäckt fiende fadar in över 0.3 s och triggar slow motion om det är gruppens första kontakt.

---

## 8. Innehållsleverans

1000 banor får inte betyda 1000 filer. **En bana är ett recept plus ett seed** — den existerar inte på disk förrän den genereras.

- **Addressables** med ett paket per biom. Biomet laddas ned första gången spelaren når ett kapitel som använder det.
- Initial build innehåller bara skogsbiomet plus UI och kärnsystem.
- Sparfil: lokal JSON (`Application.persistentDataPath`), molnsparning i fas 4.

---

## 9. Assemblystruktur

```
Arna.Sim          ren C#, inga UnityEngine-beroenden — testbar utanför Unity
Arna.Data         ScriptableObject-definitioner
Arna.Gen          bangenerator (beror på Sim + Data)
Arna.View         MonoBehaviours, rendering, VFX
Arna.UI           HUD, menyer, lägret
Arna.App          bootstrap, scenhantering, sparning
Arna.Tests        edit mode-tester (beror på Sim + Gen)
```

Assembly-definitions används för att **tvinga fram** separationen — `Arna.Sim` refererar inte `UnityEngine`, och kompilatorn ser till att det förblir så. Utan den spärren kommer ett `Time.deltaTime` att smyga sig in i simuleringen inom en månad.

`Jobs`/`Burst` används endast på två ställen om profilering visar behov: enhetsstyrning och detektionsuppdatering. Inte som generell arkitektur — kodbasen ska vara enkel att arbeta i.

---

## 10. Teststrategi

| Nivå | Vad |
|---|---|
| Enhetstest | Sim-logik: skadeberäkning, detektion, terrängmodifierare |
| Determinismtest | Samma seed × 100 körningar ⇒ identisk resultathash |
| Generatortest | Batchgenerera 100 banor: alla ska vara genomförbara och inom prestandabudget |
| Balanstest | Kör hela banor headless med scriptade arméer, logga utfall |
| Enhetstest på hårdvara | Profilerkörning på fysisk iOS- och Android-enhet varje fas |

Balanstestet är det som gör 1000 banor hanterbart: när simuleringen kan köras utan rendering kan hela kapitel balanseras över natten i stället för för hand.
