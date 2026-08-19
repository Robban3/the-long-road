# Ekonomi, shop och F2P-design

Version 0.2 — fas 0. Siffror är startvärden för tuning mot riktig data i soft launch.

---

## 1. Designprincip

Monetiseringen ska växa ur spelets egna spänningar, inte klistras på. Tre spänningar bär hela ekonomin:

- **Guld till trupp, eller guld till information?** — inför varje bana.
- **Uppgradera nu, eller spara till något dyrare?** — under varje bana, med silvret som fienderna släpper.
- **Attrition inom kapitlet** — skadade trupper skapar ett återkommande resursbehov utan att någon artificiell energimätare behövs.

**Ingen energimätare.** Spelet ska kunna spelas hur länge man vill. Intäkten kommer från progressionens djup, inte från att stänga porten. En spärr på antal sessioner motverkar hela poängen med 1000 banor.

---

## 2. Valutor

| Valuta | Källa | Används till | Varaktighet |
|---|---|---|---|
| **Silver** | Dödade fiender, desarmerade fällor, upptäckta fiendegrupper | Uppgraderingar under pågående bana | **Nollställs vid banans slut** |
| **Guld** | Banresultat, oanvänt silver (4:1), dagliga uppdrag | Läkning, reparation, rekrytering, information, smedjan | Permanent |
| **Ädelstenar** | IAP, sparsamt från stjärnbelöningar | Acceleration, kosmetik, extra truppplats | Permanent |
| **Veteranpoäng** | Överlevda banor | Truppuppgraderingar — **kan inte köpas** | Permanent, per trupp |

Veteranpoäng hålls osäljbara med flit. Det som gör en trupp stark ska vara att du har skyddat den, inte betalat för den. Det bevarar attritionssystemets tyngd och håller spelet borta från pay-to-win.

---

## 3. De tre uppgraderingslagren

Tre system höjer samma siffror. Om de inte hålls isär blir spelet obegripligt för spelaren och obalanserbart för oss.

| Lager | Valuta | Varaktighet | Takeffekt | Roll |
|---|---|---|---|---|
| **Smedjan** | Guld | Permanent, alla trupper av typen | +50 % | Höjer *golvet* |
| **Veteranstatus** | Veteranpoäng | Permanent, den enskilda truppen | +35 % | Belönar *överlevnad* |
| **Stridsuppgraderingar** | Silver | Endast innevarande bana | **+150 %** | Avgör *banan* |

**Balansregeln: stridslagret måste dominera metalagret.** Om de permanenta uppgraderingarna växer sig starkare än banans egen kurva kommer en spelare i kapitel 40 att köra över kapitel 5 utan att fatta ett enda beslut — och då kollapsar 1000 banor till en grind. Metalagret höjer golvet, stridslagret avgör utfallet. Därför är silvertaket dubbelt så högt som de två permanenta lagren tillsammans.

---

## 4. Guldflöden

**Inkomst per bana** (kapitel 1, skalar med kapitelnummer)

| Källa | Guld |
|---|---|
| Bana klarad | 40 |
| Per överlevande vagn | 15 |
| Skattevagnens innehåll | 0–60, skalar med vagnens HP% |
| Oanvänt silver | 4 silver → 1 guld |
| ★★★ första gången | 50 (engångs) |
| Omspel av klarad bana | 40 % av full inkomst |

**Utgifter**

| Post | Guld |
|---|---|
| Läka trupp till full HP | 2 per HP-punkt |
| Reparera vagn | 1.5 per HP-punkt |
| Rekrytera ny trupp | 8 × truppkostnad |
| Förhandsspaning (delsträcka) | 30 |
| Lokal vägvisare (avslöjar 1 fara) | 45 |
| Kartor och rykten | 25 |

Kalibrering: en spelare som klarar en bana rent går ungefär +60 guld. En som klarar den skadad går nära noll. Skicklighet är den primära guldkällan, och attritionen förblir kännbar utan att någonsin låsa in spelaren.

---

## 5. Shoppen (lägret)

Fem diskar, var och en med ett tydligt syfte.

| Disk | Valuta | Säljer |
|---|---|---|
| **Kasernen** | Guld | Rekrytera trupper, låsa upp nya trupptyper |
| **Smedjan** | Guld + veteranpoäng | Permanenta grundnivåer per trupptyp |
| **Vagnmakaren** | Guld | Reparera vagnar; uppgradera max-HP och ballistans DPS |
| **Handelsboden** | Guld | Information (kartor, rykten, spaning) och förbrukningsvaror |
| **Ädelstensbutiken** | Riktiga pengar | Ädelstenar, pass, kosmetik |

**Förbrukningsvaror** köps i förväg och bärs in i banan, max 2 per bana:

| Vara | Guld | Effekt |
|---|---|---|
| Silverpung | 120 | 60 silver vid banans start |
| Vagnreparationssats | 90 | Återställer 150 HP på en vagn mitt i banan |
| Fällkit | 70 | Desarmerar en fälla utan ingenjör, engångs |
| Rökbomb | 80 | Fiender tappar mål i 5 s |

Taket på två varor per bana är avsiktligt. Förbrukningsvaror ska vara ett svar på ett *känt* problem — "kapitlets boss krossar mina vagnar" — inte en generell buffert som gör planeringen oviktig.

---

## 6. Ädelstenar som accelerator

Vill man skynda på finns genvägarna här. Gränsen är att ädelstenar får **spara tid och höja golvet — aldrig ersätta ett beslut.**

| Genväg | Ädelstenar | Tak |
|---|---|---|
| Silverpung i pågående bana | 40 | **Max 1 per bana** |
| Läk hela armén direkt | 25 | — |
| Reparera alla vagnar direkt | 30 | — |
| Sjunde truppplatsen | 300 | Permanent, engångs |
| Återställ förbrukade varor | 20 | — |

Taket på en silverpung per bana är det som skiljer accelerator från pay-to-win: du kan starta banan starkare, men du kan inte köpa dig ur ett dåligt vägval eller ur att ha uppgraderat fel trupp. Beslutet är fortfarande ditt, och det är fortfarande det som avgör.

Veteranpoäng finns medvetet inte i tabellen och ska aldrig hamna där.

---

## 7. IAP

| Produkt | Pris (ca) | Innehåll |
|---|---|---|
| Startpaket | 29 kr | Ädelstenar + en trupp + guld. Engångs, visas efter kapitel 1. |
| Ädelstenspaket | 29 / 79 / 199 / 499 kr | Standardtrappa |
| **Regionspass** | 79 kr | Se nedan |
| Reklamfri | 79 kr | Tar bort alla frivilliga annonser, ger belöningarna ändå |
| Kosmetiska paket | 39–99 kr | Vagnsskinn, truppfärger, karavansflaggor |

"Reklamfri" ger belöningarna ändå. Att sälja bort en förmån och lämna spelaren sämre ställd är kortsiktigt och skadar retentionen.

### Regionspass i stället för säsongspass

Arrow Quest säljer **Chapter Passes à $11.99**, vilket fungerar för dem eftersom de har få kapitel. Med 100 kapitel blir ett pass per kapitel antingen påträngande eller meningslöst billigt.

Lösningen: gruppera de 100 kapitlen i **10 regioner om 10 kapitel** (= 100 banor per region) och sälj ett **Regionspass** per region. Det ger passets belöningstrappa en naturlig längd, kopplar den till faktiskt spelande i stället för till en kalender, och undviker FOMO-mekaniken helt. En spelare som kommer tillbaka efter tre månader har inte förlorat något.

Regionspasset innehåller kosmetik, guld och ädelstenar — **aldrig** veteranpoäng eller exklusiva trupptyper. Ett tidsbundet säsongspass hålls utanför v1.

---

## 8. Annonser

Alla annonser är **rewarded** — aldrig påtvingade interstitials. Ett spel med 90-sekunderssessioner tål inte avbrott mellan banor; det skulle döda själva flödet som får folk att fortsätta.

| Placering | Belöning | Frekvenstak |
|---|---|---|
| **Rädda vagnen** — resultatskärm efter förlorad vagn | Skattevagnens byte återställs | 3/dag |
| **Dubbelt byte** — efter klarad bana | ×2 guld | 5/dag |
| **Silverpung** — under pågående bana | 40 silver direkt | 2/dag |
| **Gratis spaning** — i förberedelsevyn | En förhandsspaning gratis | 2/dag |
| **Fältläkare** — i lägret | Läk en trupp gratis | 3/dag |

"Rädda vagnen" är den starkaste placeringen och den enda som växer direkt ur speldesignen: delnederlaget i `GDD.md` §5 skapar ett äkta ögonblick av förlust som spelaren *vill* ångra. Trevagnssystemet är därför både ett design- och ett ekonomibeslut.

---

## 9. Retention

| System | Syfte |
|---|---|
| Dagliga uppdrag (3 st) | Återkommande skäl att öppna appen |
| Kapitelavslut | Naturlig stoppunkt med belöningspuckel |
| Stjärnjakt | ★★★ på gamla banor ger guld och passframsteg |
| Veckans karavan | Fast seed, global rankning, ett försök per dag |

**Veckans karavan** är billig att bygga eftersom banor redan är seedbaserade och simuleringen deterministisk — samma bana åt alla, och resultat som går att verifiera. Ett tävlingsläge för nästan ingen produktionskostnad.

---

## 10. Nyckeltal att mäta från fas 4

Utan dessa går 1000 banor inte att balansera.

| Mätvärde | Varför |
|---|---|
| **Vald korridor per bana** | Är alla tre rutterna gångbara, eller finns ett dominant val? |
| **Silver intjänat vs spenderat** | Sitter spelarna på outnyttjat silver? Då är kostnadskurvan fel. |
| **Valt uppgraderingsspår per trupp** | Är något spår alltid rätt svar? Då är det inte ett val. |
| Fail-punkt per bana (position längs rutt) | Hittar orättvisa bakhåll |
| Truppval per bana | Är någon trupp obligatorisk? Är någon aldrig vald? |
| Stjärnfördelning | Svårighetskurvan |
| Andel som köper information | Fungerar spaningsmekaniken som avsett? |
| Retention D1 / D7 / D30 | Grundhälsa |
| ARPDAU och annonsvisningar per session | Ekonomins hälsa |

"Vald korridor" och "valt uppgraderingsspår" är de två viktigaste designmätvärdena. De mäter samma sak på två nivåer: **har spelarens val någon betydelse?** Om svaret blir nej på någon av dem har banans centrala beslut kollapsat, och generatorns korridorvalidering (`content-pipeline.md` §3, steg 4) eller kostnadskurvan i `GDD.md` §6.4 måste justeras.
