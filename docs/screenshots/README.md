# Skärmbilder

Bilderna här är renderade utan Unity. Motorn är en Windows-binär och dess egna
capture-metoder — `Arna.Editor.ArnaSetup.CaptureLevelPreview` och `CapturePlayScene` —
kräver en installation med GPU. Generatorn kräver ingenting av det: en bana är ett
recept plus ett frö, och den aritmetiken finns nu även i `Tools/arna_level.py`.
`Tools/render_screens.py` ritar den med en egen rasteriserare, z-buffert och skuggkarta.

    cd Tools
    python3 render_screens.py --chapter 1 --level 5 --out ../docs/screenshots

PNG-filerna själva ligger inte i repot. `.gitattributes` lägger `*.png` i Git LFS, och
LFS-endpointen går inte att nå från miljön bilderna renderades i — en incheckning här
hade antingen misslyckats i pushen eller smugit in binärer förbi den konvention repot
valt. Kommandot ovan bygger om vilken bild som helst på en minut, deterministiskt: samma
frö ger samma bana och samma bild. Tabellerna nedan säger vad varje bild visar och vilka
siffror banan har, så texten står på egna ben även utan filerna bredvid sig.

## Vad som är på riktigt och vad som är en skiss

**Banan är på riktigt.** Terräng, höjdfält, floder och vadställen, de tre korridorerna
och varje fiende, fälla och silverdepå kommer ur porten av generatorn. Den reproducerar
siffrorna som `status.md` noterar för 1-5: snabbaste rutt 94.4 och 59 % korridoröverlapp.
Det är beviset på att det är samma banor som motorn bygger — porten räknar i enkel
precision just för att träffa dem exakt.

**Kameran och ljuset är på riktigt.** Vinklar, synfält, ortografisk storlek,
solriktningar, half-lambert-termen, trilight-ambient och den linjära dimman är avlästa
ur `ArnaSetup.cs` och `TerrainGround.shader`.

**Modellerna är det inte.** Varje FBX i repot är en Git LFS-pekare och packen är inte
hämtade i den här miljön, så träd, vagnar, trupper och byggnader ritas som procedurella
stand-ins — i rätt storlek och på rätt plats enligt `TerrainDecorator`, men en tall är
en kon och inte den tall spelaren ser. Markens detaljtextur är också en LFS-pekare, så
dess kornighet är värdebrus i shaderns två skalor i stället för fotografiet.

Kortfattat: landet är det riktiga, dräkten är en skiss av det.

## Bilderna

### Planeringskartan

Rakt uppifrån, ortografisk, med de tre rutterna lagda över marken — röd snabb, blå säker,
orange udda, ritad i den ordningen så att den snabba ligger överst där de sammanfaller.
Start är den gröna rutan i väst, mål den gula i öst.

| Bild | Bana | Snabb | Säker | Udda | Överlapp | Fiendegrupper | Fällor |
|---|---|---|---|---|---|---|---|
| `plan-1-1.png` | 1-1 (frö 1001) | 78.9 | 100.1 | 154.4 | 14 % | 12 | 5 |
| `plan-1-5.png` | 1-5 (frö 1005) | 94.4 | 96.1 | 137.1 | 59 % | 9 | 4 |
| `plan-1-10.png` | 1-10 (frö 1010) | 90.9 | 96.2 | 158.8 | 57 % | 12 | 7 |

Kostnaderna är restid i rutsteg. 1-5 är banan `status.md` pekar ut som problemet: 59 %
av rutorna delas mellan rutterna, och den säkra rutten är bara 2 % långsammare än den
snabba — vägvalet betyder nästan ingenting där. 1-1 är motsatsen och visar vad
generatorn gör när den lyckas.

### Spelvyn

Bakom och ovanför kolonnen, på det avstånd `LevelRunner` använder (46 m bakåt, 32 m upp,
50° synfält). Ingen ruttlinje är målad på marken här — den hör till kartan.

| Bild | Bana | Rutt | Var |
|---|---|---|---|
| `play-1-1-fast.png` | 1-1 | snabb | vid vadstället, halvvägs |
| `play-1-1-odd.png` | 1-1 | udda | genom kärret |
| `play-1-5-fast.png` | 1-5 | snabb | skogsbrynet |
| `play-1-10-safe.png` | 1-10 | säker | vid flodövergången |

Vagnarna går i kolonn med 8 m mellanrum: krigsvagnen först, förrådsvagnen i mitten,
skattvagnen sist och i egen färg — spelaren ska kunna se vilken kärra som bär bytet.
Eskorten står i formation 6 m ut från den ledande vagnen.

En skillnad mot ett riktigt spelläge: motorn visar fiender först när spaningen hittat
dem. Bilderna är av banan, inte av en bildruta ur en omgång, så fienderna som
generatorn lagt på rutten är utritade där de står.

## Vad bilderna inte visar

Det som saknas i spelet saknas också här, och det är samma lista som `status.md` för:
inga vägar (`TerrainType.Road` skrivs aldrig), därför inga hus och inga åkrar; inget
läger, ingen butik, inget gränssnitt; och otexturerade berg.
