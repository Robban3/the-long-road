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

### Kartans markörer

Start, mål och varje vadställe ritas i **bildrummet**, ovanpå den färdiga bilden — inte
på marken. Det är inte en genväg utan hela poängen: planeringskartan är en karta, och
det spelaren planerar mot måste behålla sin storlek och sin plats vare sig det ligger på
solbelyst äng eller under gran.

Så var det inte förut, och kartan gick inte att använda för det. Start och mål var en
ruta färg var på marken — fyra meter, elva bildpunkter rakt uppifrån sjuttio meter — och
målet på 1-5 låg under ett berg och syntes inte alls. Vadställena var terrängfärg, alltså
en aning ljusare flod, fast GDD §3.3 kallar dem kartans viktigaste information. Spelaren
ombads dra en linje till ett mål hen inte såg, över passager hen inte hittade.

Vadställen ritas som ett brospann tvärs strömmen, dragna en bit upp på båda stränderna:
ett märke som slutar vid vattenlinjen läses som grunt vatten, vilket är precis den
läsningen §3.3 säger att man ska undvika. De ritas oavsett om örnen flugit — vatten är
terräng, inte något spaningen döljer.

**Rutten ritas också ovanpå allt**, utan djuptest. Linjen är spelarens egen och inte ett
föremål i världen; testad mot djupbufferten försvann den under lövverket på tre ställen
på 1-5 och nådde aldrig fram till målet, för sista sträckan gick bakom ett berg.

### Örnen och lagret

    python3 render_screens.py --chapter 1 --level 5 --eagle --out ../docs/screenshots

Med `--eagle` ligger planeringskartan under det grå lagret och bara det örnen flög
över har full färg. Grupperna den passerade över får en röd nål, och fågeln själv står
längst fram i spåret.

Lagret tar bort 88 % av färgen och lämnar formen. Mindre än så — 72 %, som första
versionen — blev en dis snarare än ett lager, och örnens spår stack knappt ut mot landet
omkring; förmågan måste synligt vara värd sitt guld. Mer kostar för mycket: vid 94 % och
uppåt slutar floden vara blå utanför spåret, och GDD §3.3 kräver att vatten och dess
övergångar går att läsa *innan* rutten dras. Det är hela skälet till att lagret är
genomskinligt.

**Fågeln är inte i skala, och ringen är därför inte heller det.** En kungsörn är två
meter bred, vilket är elva bildpunkter rakt uppifrån sjuttio meter — ingenting. Tjugoen
meter, som första utkastet hade, gjorde en hängglidare av den: bredare än granarna på
åtta meter som den flög över, vilket får kartan att se liten ut i stället för fågeln
stor. Tio meter är där den ligger nu — under lövverket den flyger över, vilket var
proportionen som skavde, och fortfarande en fågel snarare än en fläck.

Ringen omkring den är ritad i bildrummet efter att världen är skuggad, så den behåller
sin storlek över både ljus äng och mörk granskog. En ring på marken provades först och
åts av lövverket: en vit skära bakom en gran läser som ett ljusfel, inte som en markör.

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
