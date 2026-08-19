# Innehållspipeline — hur 1000 banor produceras

Version 0.1 — fas 0.

---

## 1. Grundprincipen

En bana **lagras aldrig**. Den beskrivs av två saker:

```
bana = ChapterRecipe + seed
seed = chapter * 1000 + level
```

Bana `7-3` är recept nr 7 kört med seed `7003`. Generatorn är deterministisk, så banan är identisk för varje spelare, varje gång — utan att en enda nivåfil behöver distribueras. Det är enda realistiska vägen till 1000 banor, och det är också det som håller nedladdningen liten.

---

## 2. ChapterRecipe

Ett `ScriptableObject` per kapitel. Det är här designern arbetar — inte i en nivåeditor.

| Fält | Exempel (kapitel 1, skog) | Roll |
|---|---|---|
| `biome` | `Forest` | Tilepalett, prefabs, väder |
| `terrainMix` | skog 45 %, slätt 30 %, kärr 10 %, bergspass 8 %, vatten 7 % | Naturlig terräng. Vägar och vadställen är linjära drag som ristas in efteråt, inte brus — de ingår därför inte i blandningen. Andelarna normaliseras, de behöver inte summera till 1. |
| `enemyBudget` | 40 → 120 poäng (bana 1 → 10) | Total fiendestyrka |
| `enemyPool` | Varg, Skogsbandit, Bågbandit | Tillåtna fiendetyper |
| `trapDensity` | 0.4 → 1.2 | Multiplikator mot terrängens fälltäthet |
| `routeLength` | 60 → 95 tiles | Kartans längd start → mål |
| `weather` | Klart, Dimma, Regn | Påverkar sikt och hastighet |
| `squadBudget` | 12 → 18 poäng | Spelarens truppbudget |
| `silverMultiplier` | 1.0 | Skalar fiendernas silverdrop — tuningratt för uppgraderingstakten |
| `parTime` | Beräknas | Underlag för ★★★ |
| `specialRules` | t.ex. `Night`, `PursuingHorde` | Kapitelmekanik |

Budgetarna anges som start- och slutvärde; generatorn interpolerar över kapitlets 10 banor. Att balansera ett kapitel är alltså att justera ungefär åtta tal — inte att bygga tio banor för hand.

---

## 3. Genereringsstegen

Körs i fast ordning. Varje steg konsumerar samma `DeterministicRandom`-ström, vilket gör att ett ändrat steg påverkar allt efter det (viktigt att veta vid balansering).

**1. Terräng** *(implementerad)*
Value noise med fBm ger ett höjdfält. Terrängtyperna sorteras längs en höjdaxel — vatten lägst, bergspass högst — och tilldelas genom att **kvantilklyva de sorterade brusvärdena** vid receptets kumulativa andelar.

Kvantiler i stället för fasta trösklar är avgörande: med fasta trösklar avgörs den faktiska fördelningen av hur bruset råkade falla, så ett recept som ber om 10 % kärr kan ge 2 % eller 25 %. Med kvantilklyvning kommer den efterfrågade blandningen ut rätt oavsett brusets form — och först då går ett kapitel att balansera genom att ändra siffror. Verifierat inom 2 procentenheter i `TerrainGeneratorTests`.

**2. Start och mål** *(implementerad)*
Placeras i motsatta kantband (3 tiles breda), minst `MinRouteTiles` isär mätt som A\*-väg, inte fågelvägen. Generatorn provar upp till 64 kandidatpar och tar det längsta den hittar om inget når minimikravet.

Är ett kantband helt blockerat av vatten öppnas en tile i stället för att seedet förkastas — att förkasta här hade systematiskt gynnat kartor med torra kanter. Når inget på västkanten östkanten ristas en korridor. Generatorn får aldrig returnera en ospelbar bana.

**3. Vägnät**
En huvudväg dras mellan start och mål och genar delvis. Den är den snabba rutten — och därför den farliga.

**4. Ruttkorridorer**
Generatorn identifierar **minst tre distinkt olika rutter** från start till mål och verifierar att de skiljer sig meningsfullt i tid och risk:

- en **snabb** (kort, mycket väg/slätt, hög bakhållsvikt)
- en **säker** (lång, undviker högriskterräng)
- en **udda** (går genom kärr eller bergspass — långsam men få fiender)

Klarar en bana inte detta test förkastas den och seedet inkrementeras. **Det här steget är kvalitetsgrinden.** Utan det producerar generatorn banor där vägvalet inte spelar någon roll — och då finns inget spel kvar, bara en armévalsskärm.

**5. Fiendeplacering** *(implementerad)*
`enemyBudget` fördelas över korridorerna i **omvänd proportion mot restid**. Snabb rutt får flest fiender, långsam rutt får färre:

```
korridorAndel = (1 / restid) / Σ(1 / restid)
fiendepoäng   = enemyBudget × korridorAndel
```

Inom en korridor placeras grupperna på rutor viktade efter terrängens bakhållsvikt, med minst fem rutors mellanrum så att striderna kommer en i taget, och med de sex första och sista rutorna fredade.

Uppmätt över 30 banor: **den snabba rutten är farligare i 30 fall av 30**, med 0,22 hotpoäng per ruta mot den långa ruttens 0,08.

**Balansgränsen som inte var uppenbar.** `enemyBudget` fungerar bara inom ett band, och båda ändarna slår sönder designen:

| Budget | Vad som händer |
|---|---|
| Under ~80 | Silvergolvet binder på flera korridorer och jämnar ut inkomsten. Belöningen för att ta risken försvinner. |
| 80–140 | Fungerar. Vid 100 ger snabb rutt 99 silver mot långa ruttens 65 — **52 % mer** — och golvet triggar på 0,3 korridorer per bana. |
| Över ~140 | Den snabba korridoren **mättas** — den är kort och gruppavståndet begränsar hur många möten som får plats. Överskottet hamnar på de längre rutterna, och den långsamma vägen blir den rikaste. Vid budget 200 är den snabba rutten rikare i bara 37 % av banorna. |

**Silvergolvet ska sitta lågt.** Det sattes först till 105 ("tre uppgraderingsnivåer") och band då på nästan varje korridor, vilket toppade upp allihop till exakt samma summa. Effekten blev att alla rutter gav lika mycket och hela skälet att ta den farliga vägen försvann. Vid 55 triggar det bara i det verkligt trasiga fallet, och skillnaden mellan rutterna får stå kvar.

Konsekvensen för progressionen: **senare kapitel kan inte bli svårare genom fler fiender.** Bortom ungefär 140 poäng måste svårigheten komma från tåligare fiendetyper, inte från fler av samma.

**6. Fällplacering**
`trapDensity × TerrainTypeDef.trapDensity` per tile. Kärr blir automatiskt fällrikt, slätt nästan fritt. Fällpoäng dras av från korridorens fiendepoäng — en fällrik sträcka får färre fiender, precis som avsett.

**6b. Silverbudget per korridor**
Eftersom fiender släpper silver som spelaren uppgraderar med under banan (`GDD.md` §6) blir fiendefördelningen automatiskt också en fördelning av *spelarens styrka*. Den snabba rutten ger mer silver och därmed en starkare armé mot slutet; den säkra rutten ger mindre. Det är önskvärt — men det måste valideras:

```
silverPotential(korridor) = Σ(fiende.silver) + Σ(fälla.silver) + spaningsbonus
```

Kravet är att **varje korridors silverpotential räcker för minst 5 uppgraderingsnivåer** (≈ 105 silver i kapitel 1). En säker rutt som ger så lite silver att banans sista strid blir omöjlig är en trasig bana, inte ett svårt val. Klarar en korridor inte kravet kompenseras den med en fristående silverkälla — ett övergivet läger eller en fällsamling värd att desarmera — hellre än med fler fiender, som skulle radera själva poängen med den säkra rutten.

**7. Mjuka signaler**
Placeras utifrån vad som faktiskt genererats, med de falska positiva som anges i `GDD.md` §3.5. Signalerna genereras **efter** hoten, aldrig tvärtom.

**8. Dekoration**
Träd, stenar, vrak. Rent kosmetiskt, deterministiskt från tileseed.

**9. Validering**
- Finns minst en genomförbar rutt? (annars: förkasta, nytt seed)
- Är de tre korridorerna fortfarande distinkta efter fiendeplacering?
- Når varje korridors silverpotential minst 5 uppgraderingsnivåer?
- Ligger triangelantal och instansantal inom prestandabudget?
- Är `parTime` rimlig mot snabbaste rutten? (par = snabbaste rutt × 1.35)

---

## 4. Handkurerade banor

Tre banor per kapitel byggs för hand: `x-1`, `x-5`, `x-10`. Det är 300 av 1000 — men bara de tre viktigaste per kapitel.

| Bana | Syfte | Regler |
|---|---|---|
| `x-1` | Lär ut kapitlets nya mekanik | Förlåtande. Ny mekanik ska förekomma minst tre gånger. Ingen ny mekanik dyker upp först i en genererad bana. |
| `x-5` | Vänd på mekaniken | En stark twist — natt, dubbel längd, en trupptyp förbjuden |
| `x-10` | Boss | Handbyggd karta, unik fiende, scriptade faser |

Handbyggda banor lagras som `HandcraftedLevel`-assets med samma dataformat som generatorns utdata. Runtime skiljer inte på dem — samma laddningsväg, samma validering.

---

## 5. Editorverktyg som måste byggas

Utan dessa går innehållsskalningen inte att genomföra:

| Verktyg | Vad det gör |
|---|---|
| **Level Previewer** | Ange kapitel + bana, generera, se kartan med alla hot synliga och de tre korridorerna utritade |
| **Batch Generator** | Generera alla 10 banor i ett kapitel, rapportera valideringsfel och prestandasiffror |
| **Headless Balancer** | Kör N simuleringar per bana med scriptade arméer, rapportera vinstfrekvens och stjärnfördelning |
| **Recipe Diff** | Visa hur en receptändring påverkar samtliga 10 banors nyckeltal |

Headless Balancer är den viktigaste av dem. När simuleringen kan köras utan rendering (se `technical-design.md` §2) kan ett helt kapitel balanseras över natten i stället för genom manuellt spelande — och först då blir 100 kapitel praktiskt möjligt.

---

## 6. Biomer

~15 biomer återanvänds över de 100 kapitlen med varierande palett, väder och modifierare. Kapitel som delar biom skiljer sig genom `terrainMix`, fiendepool och specialregler.

Skogsbiomet räcker för fas 1 och 2. Fullständig biomlista fastställs innan fas 3.
