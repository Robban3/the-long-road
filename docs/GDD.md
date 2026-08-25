# The Long Road — Game Design Document

Version 0.2 — fas 0. Alla siffror är startvärden för balansering, inte slutgiltiga.

**The Long Road** är spelet. **Legacy of Arna** är världen det utspelar sig i och kan rymma fler titlar. Det här dokumentet beskriver spelet; världsmaterial som överlever enskilda titlar — biomer, folkslag, geografi, historia — hör hemma i ett separat världsdokument när det växer sig stort nog.

---

## 1. Sammanfattning

| | |
|---|---|
| **Genre** | Tower defense med rörligt försvarsobjekt |
| **Plattform** | iOS + Android (Unity 6 LTS, URP) |
| **Sessionslängd** | 90–180 sekunder per bana |
| **Perspektiv** | 3D, stiliserad low-poly, snedställd ovanifrånvy |
| **Affärsmodell** | F2P — rewarded ads + IAP |
| **Innehåll** | 100 kapitel × 10 banor = 1000 banor |

Spelaren eskorterar en karavan om tre vagnar genom en fientlig värld. Före varje bana väljer man **vilka trupper** som följer med och **vilken väg** karavanen tar. Terrängen är synlig från start; fiender och fällor är det inte — de avslöjas först inom upptäcktsradie. Kärnspänningen är hastighet mot säkerhet.

---

## 2. Kärnloop

```
LÄGRET  →  FÖRBEREDELSE  →  BANAN  →  RESULTAT  →  LÄGRET
```

1. **Lägret** — reparera vagnar, läka trupper, rekrytera, uppgradera, köpa information.
2. **Förberedelse** (ingen tidspress)
   - Välj upp till 6 truppgrupper inom en poängbudget.
   - Placera dem på de 6 positionerna runt karavanen.
   - Rita rutten genom att trycka ut 5–6 waypoints. Rutten visar uppskattad tid och terrängkostnad live.
   - Valfritt: köp förhandsspaning eller hyr vägvisare.
3. **Banan** — karavanen följer rutten automatiskt. Trupperna slåss automatiskt. Spelaren griper in med formationsbyte, 2–3 förmågor, och **uppgraderar sina trupper löpande för silvret som dödade fiender släpper** (§6).
4. **Resultat** — 1–3 stjärnor, byte, veteranpoäng. Skador följer med till nästa bana i kapitlet.

---

## 3. Banan i detalj

### 3.1 Kartformat

- Rutnät **64 × 64 tiles**, 1 tile = 4 världsmeter → 256 m × 256 m.
- Start i ena kanten, mål i den motsatta. Typisk ruttlängd 60–100 tiles.
- Kamera: fast lutning ~55°, spelaren kan panorera och zooma inom kartans gränser.

### 3.2 Terrängmatris

Varje terrängtyp påverkar minst tre system. Detta är spelets viktigaste balanstabell.

| Terräng | Hastighet | Sikt | Bakhållsvikt | Fälltäthet | Särskilt |
|---|---|---|---|---|---|
| **Väg** | ×1.25 | ×1.00 | 1.2 | 0.6 | Banditer patrullerar vägar |
| **Slätt** | ×1.00 | ×1.30 | 0.8 | 0.5 | Kavalleri +25 % skada; fiender konvergerar från längre håll |
| **Skog** | ×0.70 | ×0.55 | 1.5 | 1.0 | Bågskyttar −40 % räckvidd (skottlinje), kavalleri −50 % chargebonus |
| **Kärr** | ×0.45 | ×0.75 | 1.0 | 2.5 | Formationsradien växer 50 % (truppen splittras) |
| **Vadställe** | ×0.50 | ×1.10 | 1.3 | 0.8 | Trupperna delas i två grupper under passage |
| **Bergspass** | ×0.60 | ×0.90 | 0.9 | 1.4 | Max 3 fiender kan anfalla samtidigt; risk för stenras |
| **Vatten / brant** | — | — | — | — | Ofarbart |

Karavanens grundhastighet: **2.0 tiles/sekund**. På väg 2.5, i kärr 0.9.

### 3.3 Vägritning

Rutten är spelarens, inte generatorns. Kartan visar ingen färdig väg och inga alternativ att välja mellan — den visar landet, och spelaren drar sin egen linje genom det. Det är banans aktiva beslut.

- Spelaren trycker ut upp till 6 waypoints. Varje etapp löses med terrängviktad A*, så en grovt dragen linje blir en väg en karavanförare faktiskt hade tagit: den kramar snabb mark och skyr kärr i stället för att skära rakt igenom.
- Rutten valideras: den får inte korsa ofarbar terräng. Ogiltiga etapper visas röda och går inte att starta med.
- **Ruttförhandsvisning** uppdateras live och visar: total sträcka, uppskattad restid, andel per terrängtyp, och en risk-indikator baserad på bakhållsvikt. Detta är spelarens enda hårda beslutsunderlag — det måste vara tydligt. Risktalet läses **enbart av terrängen**: ett tal som konsulterade fiendelistan hade gett bort gratis det örnen säljs för (§3.6). Det säger *det här är bakhållsmark*, aldrig *det står fyra bakom åsen*.
- Vissa waypoints kan sättas som **specialpunkter**: `Rasta` (läker trupper, kostar 15 s), `Spana` (avslöjar 25 m radie, kostar 8 s).

**Passagerna måste synas.** Floden går tvärs färdriktningen och kan bara korsas vid sina vadställen, så vadställena är kartans viktigaste information. De markeras som byggda saker — en bro, ett märke — inte som en ljusare ruta vatten. Den etapp som korsar floden lyfter fram vilket vadställe den använder, för det är där beslutet ligger.

**Omvägar ska inte komma som en överraskning.** Ritar spelaren tvärs en flod utan vadställe stannar ingenting — A* går runt — men karavanen tar en omväg spelaren aldrig menade. En etapp som blir mer än 40 % längre än fågelvägen ritas därför avvikande: *detta blev inte vad du trodde*.

Tröskeln mäts som *gången sträcka delat med fågelvägen*, i rutor och inte i restid. Restid hade blandat ihop två olika saker: en etapp genom kärr är dyr utan att vara en överraskning, och en etapp den långa vägen runt en flod är en överraskning även på god mark. Att 40 % räcker är mätt: A\* på åttagrannars mark går aldrig fågelvägen, så en ostörd etapp ligger redan på 1,05–1,19, och en etapp som tvingas runt till fel vadställe läser 1,8–2,5. En tröskel inne i det första spannet hade varnat för varje rutt.

Generatorn räknar fortfarande fram tre korridorer — snabb, säker och udda — men de visas aldrig. De är mätinstrument: kvalitetsgrinden som förkastar frön där ingen korsning skiljer sig från någon annan, och partiden som tredje stjärnan mäts mot.

### 3.4 Dold information — de två radierna

Systemet vilar på att två radier hålls isär:

- **Fiendens `detectRadius`** — när karavanen kommer innanför denna vaknar fienden och anfaller.
- **Spelarens `sightRadius`** — högsta värdet bland trupperna, mätt från respektive truppposition. Innanför denna ritas fienden ut.

När `sightRadius > detectRadius` ser du fienden innan den vaknar och hinner omgruppera. Det är hela existensberättigandet för spejartruppen — och skälet till att spaning konkurrerar med rå stridskraft om dina 6 platser.

**Varje grupp bevakar en sträcka, inte en ruta.** `detectRadius` i tabellen är golvet; den faktiska väckningsradien är gruppens *revir* — halva avståndet till närmaste grannagrupp, mellan 24 och 52 meter. Det är vad som gör att tolv grupper täcker en karta spelaren får korsa var som helst: de håller landet mellan sig, och korsar du någons sträcka kommer de. Utan revir hade en fritt ritad linje behövt ungefär tjugoåtta grupper för att garantera fyra möten, och budgeten räcker till tolv. Det är också den sannare fiktionen — rövarband håller en vägsträcka, de köar inte på en ruta.

**Löftet, och vad det är värt.** Ingen ritad rutt ska möta färre än **fem** grupper — det är vad som gör ritfriheten till ett val i stället för en chansning. Placeraren bevisar det genom att sampla 32 rutter och flytta hot till den sämsta av dem, och den siktar på sex, inte fem: den kan bara mäta rutter den själv drog, och en linje ritad mellan dem kommer regelmässigt ut en grupp kort. Reparerat till exakt fem mötte varje osedd rutt fyra; siktat på sex möter de fem eller sex. Kvar står ett mätt undantag — en osedd rutt kan möta fyra. Högre marginal köper inte bort det, den flyttar bara vilka banor som faller (se `docs/content-pipeline.md` §3 steg 9).

Första gången en ny fiendegrupp upptäcks körs **0,4 sekunder slow motion** med en markör. Det är spelarens signal att reagera.

### 3.5 Mjuka signaler

Synliga i terrängöversikten redan under planeringen. De antyder fara utan att bekräfta den — de är vad som gör dold information rättvis i stället för godtycklig.

| Signal | Antyder | Falskt positivt |
|---|---|---|
| Cirklande kråkor | Fiendegrupp inom ~6 tiles | 20 % |
| Brända vagnsvrak | Bakhållsplats på tilen | 10 % |
| Spår i leran | Trupp har passerat nyligen | 25 % |
| Benhögar / totem | Fällfält i närheten | 15 % |
| Övergiven lägerplats | Säker tile, bra rastplats | 0 % |

Falska positiva är avsiktliga: signaler ska vara *information*, inte *facit*.

**Vilt.** Rävar, hjortar och vildsvin betar över banan och skingras när karavanen kommer
inom 26 m eller när en strid bryter ut inom 55 m. De går inte att döda och har inga
poäng — i samma stund ett djur går att fälla blir det en resurs, och en spelare som
stannar karavanen för att jaga hjort spelar ett annat spel än det här.

De finns av två skäl. Det första är att signalerna ovan ber spelaren läsa landet, och i
ett land där ingenting rör sig utom det som jagar dig lär sig ögat att rörelse betyder
fara. Djur som skenar av egna skäl lägger brus i den kanalen, och bruset är vad som gör
avläsningen till en färdighet i stället för en uppslagning.

Det andra är att stridsradien är **större** än karavanradien — 55 mot 26 meter. Varje
strid i spelet sker vid karavanen, eftersom det är eskorten fienderna kommer för, så det
är inte två olika platser utan två olika ljudnivåer: samma punkt skrämmer djur ut till
55 m när klingorna är framme och bara 26 m medan kärrorna rullar förbi. Effekten är att
striden får en synligt bredare ring av landet att bryta upp — landet reagerar, inte bara
det du slåss mot.

**Kråkornas radie var 20 tiles och sa ingenting.** Med sexton grupper på en 64-rutors
karta har 96 % av marken redan en grupp inom tjugo rutor, så påståendet var sant nästan
överallt av ren tur — en spelare som struntade i kråkorna hade haft rätt lika ofta. Mätt
över nio banor:

| radie | 3 | 4 | 5 | 6 | 8 | 10 | 12 | 15 | 20 |
|---|---|---|---|---|---|---|---|---|---|
| andel av kartan som täcks | 12 % | 20 % | 30 % | **39 %** | 56 % | 71 % | 79 % | 89 % | 96 % |

Sex är där signalen börjar vara en signal: slumpmässig mark har en grupp inom sex rutor
39 % av tiden, en flock säger 80 %. Det är en verklig uppdatering, vilket är hela testet
på om något är värt att läsa. Sex rutor är också ungefär en grupps eget revir, så
"kråkor över den skogen" betyder "du vore inne i någons räckvidd däromkring".

**Inte alla grupper får en flock** — ungefär hälften. Hade varje grupp en vore antalet
flockar antalet grupper, och att räkna dem hade gett bort banans hela slagordning gratis.
Mätt landar kvoten mellan 0,27 och 1,07 flockar per grupp.

**Kråkorna ritas på två sätt.** I spelvyn är de fåglar: en kråka är 12–20 bildpunkter
bakom karavanen och flockens ring hundra, alltså ett föremål på skärmen. På
planeringskartan rakt uppifrån är samma kråka fyra bildpunkter, och tre mörka prickar på
en ring säger "fåglar som cirklar" bättre än en modell gör. Samma signal, samma plats,
två ritsätt — vyn avgör vilket, inte smaken.

### 3.6 Spaning att köpa

Information är en resurs, och spejartruppen är bara ett av sätten att betala för den. Före banan går det att köpa spaning i guld — metavalutan, inte silvret som dör med banan — så att den konkurrerar med truppuppbyggnad över tid.

**Kartan ligger under ett grått lager från början.** Inte en dimma som döljer landet — terrängen är vad spelaren planerar mot, och att gömma den hade tagit bort beslutet i stället för ovissheten. Lagret tar färgen ur marken och lämnar formen: floden, skogen och bergen syns, men marken säger *du har inte tittat här*.

| Köp | Vad den ger | Varför den är begränsad |
|---|---|---|
| **Örnen** | Flyger tio sekunder över kartan före ritandet, i en irrande bana med sex vändningar. Spåret efter den återfår full färg, och varje grupp den passerade över markeras. Det står kvar medan rutten dras. | Lyfter 17–25 % av lagret och hittar två till fem av tolv grupper. Spåret är **smalt** — en bred korridor täcker lika mycket mark som en enda strimma tvärs kartan, en smal hinner vandra och sprider det den ser. **Banan är inte riktad:** du köper en blick på en fjärdedel av landet, inte på den fjärdedel du helst ville se. |
| **Rykten** | En grov varning per landsdel: "skogen i norr är tjock av vargar". Inga positioner. | Billigare och grövre. Pekar ut en riktning, inte en ruta. |
| **Vägvisaren** | Avslöjar vakten vid exakt ett vadställe. | Ett vadställe av tre. Valet av vilket är hela köpet. |

Örnen flyger **före** ritandet, inte under färden. Köpt till körningen vore den bara en avslöjningsbuff; köpt till planeringen är den information som blir ett beslut. Vill man använda förmågan på en bana måste den alltså spenderas innan pennan tas fram — efteråt finns inget beslut kvar att informera.

**Flygningen är låst till banans frö.** En bana som slumpades om vid varje tryck hade låtit spelaren starta om tills örnen råkade svepa över just den mark hen brydde sig om, och en förmåga man kan slå om gratis är inte ett beslut utan en enarmad bandit. Samma bana, samma flygning — slumpen ligger i kartan, inte i omtaget.

Markören säger **att** något står där, aldrig **vad**. Antal och styrka är fortfarande något spelaren får reda på genom att gå dit.

Regeln som håller dem ärliga: köpt information får minska *överraskningen*, inte ta bort *beslutet*. Örnen visar var något står — aldrig hur starkt det är. Blir spaning billig nog att köpas varje bana är dimman borta, och terrängläsningen som §3.4 och §3.5 bygger upp slutar spela roll.

---

## 4. Trupper

### 4.1 Grundregler

- En trupp är en **grupp modeller** som rör sig och slåss som en enhet. Skada fördelas på gruppen; modeller dör en i taget.
- Max **6 trupper** per bana, en per position.
- Varje trupp kostar poäng. Budgeten börjar på **12 poäng** i kapitel 1 och växer med progressionen.
- Trupper har en **leash på 10 m** från sin position — de möter fienden och återvänder sedan.
- **Varje trupp vänder sig mot sin egen motståndare och slår bara när den har en.** Både riktning och attack var tidigare hela truppens: alla vände sig åt färdriktningen och alla högg i samma stund någon fick kontakt. Sex figurer som slår i luften medan en varg biter i eftertruppen är ingen strid, det är en formering som får kramp.

### 4.2 Positioner

Ring med radie 6 m runt karavanens mittvagn:

```
        5 ─── 0 ─── 1
        │             │
   (vänster)    (höger)
        │             │
        4 ─── 3 ─── 2
```

| Index | Namn | Karaktär |
|---|---|---|
| 0 | Tät | Möter allt framifrån. Först in i fällor. |
| 1 | Höger förtrupp | Flank |
| 2 | Höger eftertrupp | Flank |
| 3 | Eftertrupp | Möter förföljare. Sist genom fällfält. |
| 4 | Vänster eftertrupp | Flank |
| 5 | Vänster förtrupp | Flank |

Position 0 och 3 tar merparten av skadan. Flankerna är rätt plats för räckviddstrupper.

### 4.3 Trupproster

| Trupp | Kostnad | Modeller | HP/modell | DPS | Räckvidd | Sikt | Särskilt |
|---|---|---|---|---|---|---|---|
| **Spjutmän** | 3 | 4 | 120 | 18 | 2.5 m | 12 m | ×2 skada mot kavalleri; blockerar frammarsch |
| **Svärdsmän** | 3 | 4 | 150 | 26 | 1.8 m | 12 m | Allround, inga svagheter |
| **Bågskyttar** | 4 | 3 | 70 | 22 | 22 m | 18 m | Kräver skottlinje; −40 % räckvidd i skog |
| **Kavalleri** | 5 | 3 | 180 | 34 | 2.2 m | 16 m | Charge ×2.5 första träffen; −50 % i skog, −70 % i kärr |
| **Trollkarl** | 6 | 1 | 90 | 40 (AoE 6 m) | 18 m | 14 m | Mana 100, 35/kast, regen 4/s |
| **Spejare** | 2 | 2 | 60 | 10 | 12 m | **34 m** | Avslöjar fällor inom 10 m |
| **Sköldbärare** | 4 | 3 | 220 | 12 | 1.8 m | 12 m | −40 % inkommande skada; absorberar fällskada |
| **Präst** | 5 | 1 | 80 | — | 12 m | 12 m | Läker 15 HP/s till en trupp; ×3 utanför strid |
| **Ingenjör** | 4 | 2 | 90 | 8 | 8 m | 14 m | Desarmerar fälla på 2 s; reparerar vagn 20 HP/s |

Designprincip: **ingen trupp är bäst överallt.** Kavalleri dominerar på slätt och är nästan värdelöst i kärr. Bågskyttar är starka på öppen mark och svaga i skog. Spejaren har nästan ingen stridskraft alls men gör hela resten av armén effektivare. Terrängvalet i vägritningen ska styra armévalet — det är kopplingen som får de två besluten att hänga ihop.

### 4.4 Aktiva förmågor

Två knappar under bana, plus en global:

| Förmåga | Cooldown | Effekt |
|---|---|---|
| **Halt** | 20 s | Karavanen stannar, formationen sluter sig. Alla trupper +25 % skada och −20 % inkommande. |
| **Omgruppera** | 25 s | Öppnar formationsvyn; byt plats på två trupper. Trupperna förflyttar sig under 3 s. |
| Kapitelspecifik #1 | 30 s | Ex. Eldregn — 120 skada i 8 m radie |
| Kapitelspecifik #2 | 45 s | Ex. Rökridå — fiender tappar mål i 5 s |

---

## 5. Karavanen

Tre vagnar i kolonn, 8 m mellan varje. Karavanen förstörd (alla tre på 0 HP) = uppdraget misslyckat.

| Vagn | HP | Funktion | Vid förlust |
|---|---|---|---|
| **Förrådsvagn** | 400 | Läker alla trupper 8 HP/s utanför strid | Ingen läkning resten av banan |
| **Skattevagn** | 350 | Bär bytet | Byte skalar med kvarvarande HP% — vid 0 förloras allt |
| **Krigsvagn** | 450 | Ballista: 30 DPS, 25 m räckvidd, siktar på den fiende med högst HP | Inget eldunderstöd |

**Delnederlag är avsiktligt.** Att förlora en vagn avslutar inte banan — du klarar den med sämre resultat. Det gör omspel lockande i stället för frustrerande, och skapar det naturliga läget för en rewarded ad ("rädda vagnen").

---

## 6. Stridsbyte och uppgraderingar under bana

### 6.1 Silver

Varje dödad fiende släpper **silver**, som samlas in automatiskt. Ingen tapping — spelet ska gå att spela med en tumme.

| Källa | Silver |
|---|---|
| Varg | 3 |
| Skogsbandit | 6 |
| Bågbandit | 5 |
| Desarmerad fälla (ingenjör) | 8 |
| Fiendegrupp upptäckt innan den vaknat (spejare) | 10 |
| Elit / miniboss | 25–60 |

Silver **nollställs vid banans slut**. Oanvänt silver växlas till guld i kurs 4:1 — alltid sämre än att ha spenderat det. Det tvingar fram frågan "uppgradera nu, eller spara till något dyrare senare?" utan att sparandet någonsin känns som ren förlust.

Spejaren och ingenjören dödar nästan ingenting. En rent kill-driven ekonomi skulle göra dem obrukbara och därmed slå sönder informationsspelet i §3.4 — därför får spaning och desarmering betalt i samma valuta.

### 6.2 Kopplingen till vägvalet

Det här är tilläggets viktigaste konsekvens:

**Den farliga rutten betalar.** Fler fiender betyder mer silver, vilket betyder starkare trupper när det verkligen gäller. Den säkra rutten tar dig fram med intakta vagnar men med en outvecklad armé. Ekonomin förstärker alltså den avvägning som redan fanns i vägritningen i stället för att konkurrera med den — och den fasta budgeten före bana får ett rörligt motstycke under bana.

Konsekvens för generatorn: **silverbudgeten per rutt måste valideras**, inte bara fiendebudgeten. En försiktig linje som ger så lite silver att slutstriden blir omöjlig är en trasig bana. Eftersom rutten är spelarens mäts det mot ett urval av rutter en spelare kan tänkas rita, inte mot tre korridorer. Se `content-pipeline.md` §3.

### 6.3 Gränssnittet

Mobilkravet styr allt här: en tumme, inga menyträd, inga tvingande pauser.

En rad med de 6 truppikonerna ligger permanent längst ned. Varje ikon visar nuvarande nivå och lyser upp när nästa steg är prisvärt.

- **Första trycket på en trupp** öppnar ett treval (spelet går till 25 % hastighet). Här låses truppens spår för resten av banan.
- **Varje följande tryck** köper nästa nivå i det valda spåret direkt — inget menyträd, ingen paus.

Djupet ligger i det första valet, friktionsfriheten i alla följande. Det är vad som gör systemet spelbart under pågående strid på en telefon.

### 6.4 Uppgraderingsspår

Tre spår per trupp, max nivå 5 inom en bana. Kostnad per nivå: **20, 32, 51, 82, 131 silver** (×1.6).

| Spår | Effekt per nivå |
|---|---|
| **Vapen** | +18 % skada |
| **Rustning** | +15 % HP, +4 % skadereduktion |
| **Special** | Unik per trupptyp |

Special-spåren är det som gör truppvalet meningsfullt även mitt i en bana:

| Trupp | Special |
|---|---|
| Spjutmän | Anti-kavalleribonus ökar; blockerar frammarsch helt |
| Svärdsmän | Cleave — träffar 2 mål |
| Bågskyttar | +15 % räckvidd; pilar penetrerar första målet |
| Kavalleri | Starkare charge, kortare omladdning mellan anfall |
| Trollkarl | −20 % manakostnad, +1 m AoE-radie |
| Spejare | +6 m sikt, avslöjar fällor längre bort |
| Sköldbärare | Provokationsaura — drar fiender bort från vagnarna |
| Präst | Läker hela formationen i stället för en trupp |
| Ingenjör | Desarmerar dubbelt så snabbt, reparerar även under strid |

En typisk bana i kapitel 1 ger cirka **220 silver** — ungefär 8 nivåer att fördela över 6 trupper. Du kan aldrig maxa allt. Det är avsikten: du måste läsa vad banan faktiskt kastar på dig och specialisera därefter, utan att veta vad som väntar bortom nästa krön.

### 6.5 Shoppen som accelerator

Vill man skynda på finns shoppen (se `economy.md` §5). Den säljer **startsilver**, permanenta grundnivåer i smedjan och förbrukningsvaror — men aldrig en genväg som gör banans egna beslut oviktiga. Gränsen är att shoppen får höja *golvet* och spara *tid*, aldrig ersätta valet av vad man uppgraderar och när.

---

## 7. Fiender och fällor (fas 1 — skogsbiomet)

### 7.1 Fiender

| Fiende | HP | DPS | Hastighet | detectRadius | Gruppstorlek | Beteende |
|---|---|---|---|---|---|---|
| **Varg** | 60 | 14 | 3.5 t/s | 20 m | 5 | Snabb, går mot flankerna och kringgår täten |
| **Skogsbandit** | 100 | 20 | 2.0 t/s | 16 m | 4 | Går rakt på skattevagnen, ignorerar trupper om möjligt |
| **Bågbandit** | 60 | 18 (18 m) | 1.8 t/s | 22 m | 3 | Håller avstånd, retirerar när något kommer inom 8 m |

Vargen straffar tomma flanker. Banditen straffar den som inte skyddar mitten. Bågbanditen straffar den som saknar egen räckvidd. Tre fiender, tre olika lärdomar.

### 7.2 Fällor

| Fälla | Skada | Effekt | Avslöjas inom |
|---|---|---|---|
| **Vargagrop** | 80 | Immobiliserar träffad trupp 2 s | 6 m |
| **Stockfälla** | 120 | Linjeskada, 5 m bred | 8 m |

Sköldbäraren absorberar fällskada; ingenjören desarmerar; spejaren avslöjar på 10 m i stället för 6–8. Tre olika svar på samma problem.

---

## 8. Progression

### 8.1 Kapitelstruktur

100 kapitel × 10 banor. Inom ett kapitel (`1-1` … `1-10`) delas biom och fiendetema.

| Bana | Roll |
|---|---|
| `x-1` | Intro — lär ut kapitlets nya mekanik i en förlåtande miljö |
| `x-2` … `x-4` | Variation, stigande svårighet |
| `x-5` | Twist — mekaniken vänds (t.ex. natt, storm, dubbel ruttlängd) |
| `x-6` … `x-9` | Upptrappning |
| `x-10` | Boss — fästning eller krigsherre, handbyggd |

Endast `x-1`, `x-5` och `x-10` handkureras. Resten genereras från kapitelrecept (se `content-pipeline.md`).

### 8.2 Attrition inom kapitel

Detta är det system som gör progressionen minnesvärd:

- Skador på trupper **följer med** från bana till bana inom kapitlet.
- Läkning mellan banor kostar guld och är begränsad — man kan inte alltid gå in i nästa bana helt återställd.
- Trupper som förlorar alla modeller är **döda** och måste ersättas med en ny, oerfaren grupp.
- Vid kapitelslut återställs allt.

Effekten: bana `1-7` spelas inte isolerat utan med en armé märkt av `1-1` till `1-6`. Ett dyrt vinstläge tidigt får konsekvenser sent. Det förvandlar 10 fristående omgångar till en kampanj.

### 8.3 Veteranstatus

En trupp som överlever en bana får veteranpoäng. Vid 3 / 8 / 15 poäng: +10 % / +20 % / +35 % till HP och skada. Veteranstatus är permanent och är en av de starkaste anledningarna att skydda sina trupper i stället för att offra dem.

### 8.4 Betygsättning

| Stjärnor | Krav |
|---|---|
| ★ | Nå målet med minst en vagn kvar |
| ★★ | Alla tre vagnar överlever |
| ★★★ | Alla tre vagnar över 60 % HP **och** måltid underskriden |

**Karavanen stannar i strid, och partidens klocka stannar med den.** En kolonn under
angrepp håller in och eskorten formerar sig — det är den sannare bilden och den
tydligare: striden blir en händelse i stället för en sträcka långsammare väg.

**Men bara för en strid den faktiskt står i.** Att stanna för *allt* var nära nog
dödligt: en bågskyttetrupp håller in på sina egna 18 meter och skjuter, kolonnen
stannade med den, och eftersom förrådsvagnen bara lappar ihop eskorten *utanför* strid
kunde ingen av parterna bryta kontakten. Bana 1-5 slutade med karavanen förstörd på
sju procent av rutten efter 228 sekunder — varav tre i rörelse — och bandet som gjorde
det stod kvar på full hälsa hela tiden. Gränsen är därför **5 meter mätt från truppen**:
en närstridsangripare stannar på sina 2 meter plus slacken och ligger med god marginal
innanför, en bågskytt på 19,5 är ingenstans i närheten. Man formerar sig när vargarna
är på en; man rullar vidare när pilarna kommer ur ett skogsbryn.

Förrådsvagnens läkning följer samma gräns. "Mellan strider" betyder *medan kolonnen
rullar*, inte *medan absolut ingenting är i kontakt* — det är den regel den skrevs som,
de två var samma sak ända tills stoppet snävades in. Mätt: kapitel 1 gick från 20
överlevbara rutter av 30 till 24.

Men partiden mäts på **restiden**, inte på väggklockan. `parTime` härleds ur *ruttens*
kostnad — hur långt, över vilken mark — så att mäta den mot en klocka som också räknar
stillastående strid jämför olika saker. Det märktes inte medan strid bara saktade ner
kolonnen.

Det som står på spel är att tredje stjärnan ställer **två** frågor. Tid är rutten du
ritade; blod är striderna du tog. Låter man striden kosta båda blir de en enda fråga
ställd två gånger, och valet mellan den snabba och den säkra vägen upphör att vara ett
val. Mätt: sex strider är 27 sekunder stillastående mot en partidsslack på 14–19.

---

## 9. Beslutade öppna frågor

**Trupper är grupper i simuleringen, men ritas som enskilda figurer.** Poolad hälsa och
en position per grupp är det som gör tolv grupper billiga i 20 Hz på en telefon, och
skadefördelningen begriplig. Inget av det kräver att gruppen *syns* som en figur — och
att den gjorde det var ett fel med namn: en vargflock är fem vargar och skärmen visade
en. Antalet figurer följer nu gruppens kvarvarande modeller, och `Formation` i
`Arna.Sim` bestämmer var de står, så vy, karta och test får samma svar.

**Silver nollställs mellan banor.** Om det sparades skulle en spelare kunna gå in i en bana redan maxad, och då försvinner hela uppgraderingskurvan som gör varje bana till en egen berättelse. Nollställningen är det som håller bana `47-3` lika spännande som `1-3`.

**Byte i den skadade skattevagnen är permanent förlorat**, men kan räddas med en rewarded ad direkt vid resultatskärmen. Det ger attritionen tyngd utan att göra den bestraffande.

**Biomlistan** (~15 stycken) fastställs tillsammans med användaren innan fas 3. Skogsbiomet räcker för fas 1 och 2.
