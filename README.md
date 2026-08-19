# The Long Road

Det första spelet i **Legacy of Arna** — världen som titlarna utspelar sig i.

| Nivå | Namn | Roll |
|---|---|---|
| Utvecklare | **3J Studiodesign** | Företaget som bygger spelet. Publisher i App Store och Google Play. |
| Värld | **Legacy of Arna** | Universumet. Kan rymma fler titlar än den här. |
| Spel | **The Long Road** | Den här titeln. Första spelet i världen. |

Skrivs ut som *The Long Road — A Legacy of Arna Game*, utgivet av 3J Studiodesign.

**Bundle ID:** `com.legacyofarna.thelongroad`

Världen som grupperingsled, titeln som app. Nästa spel i Legacy of Arna blir `com.legacyofarna.<nästa titel>` — samma prefix, eget ID, konsekvent hela vägen.

Studionamnet ingår inte, och behöver inte göra det: utgivare anges separat som metadata i App Store och Google Play. Att skriva `com.3jstudiodesign...` hade dessutom inte gått att bygga — Androids `applicationId` kräver att varje segment inleds med en bokstav, och Gradle avvisar segment som börjar på siffra.

Låst efter första butiksinlämningen.

Ett 3D tower defense-spel för iOS och Android. Spelaren eskorterar en karavan om tre vagnar genom en fientlig värld — 100 kapitel om 10 banor vardera. Terrängen syns från start; fiender och fällor gör det inte.

**Status:** fas 0 — design. Ingen kod skriven ännu.

## Dokument

| Dokument | Innehåll |
|---|---|
| [GDD.md](docs/GDD.md) | Kärnloop, terrängmatris, trupproster, fiender, progression |
| [technical-design.md](docs/technical-design.md) | Unity-arkitektur, prestandabudget, determinism |
| [content-pipeline.md](docs/content-pipeline.md) | Hur 1000 banor genereras från recept och seed |
| [economy.md](docs/economy.md) | F2P-loop, valutor, annonser, IAP, nyckeltal |

## Grundvalen

| Val | Beslut |
|---|---|
| Motor | Unity 6000.3.22f1 (6.3 LTS), URP, mobilprofil |
| Kontroll under bana | Hybrid — autostrid + formationsbyte + 2–3 förmågor |
| Kartproduktion | Seedad procedurgenerering + handkurerade `x-1`, `x-5`, `x-10` |
| Affärsmodell | F2P — rewarded ads + IAP, ingen energimätare |

## De fyra besluten som bär spelet

1. **Information är en resurs.** Spejartruppen, förhandsspaning och rykten konkurrerar med rå stridskraft om samma budget. Utan det blir vägvalet ett myntkast i stället för strategi.
2. **Den farliga rutten betalar.** Dödade fiender släpper silver som spelaren uppgraderar sina trupper med under pågående bana. Fler fiender ger en starkare armé — så ekonomin förstärker hastighet-mot-säkerhet-avvägningen i stället för att konkurrera med den.
3. **Generatorn garanterar tre distinkta rutter per bana** — snabb, säker och udda — och fördelar fiender omvänt mot restid. Det är kvalitetsgrinden som gör att vägvalet betyder något.
4. **Simuleringen är skild från presentationen** och deterministisk. Det ger hastighetskontroll, omspel, headless balansering och tävlingslägen — allt från samma arkitekturbeslut.

## Referens

Visuell ambitionsnivå: [Arrow Quest: Idle Defense RPG](https://play.google.com/store/apps/details?id=com.Wispwood.ArrowQuest) (Wispwood) — stiliserad fantasy, fast kamera, låg polygonbudget. Deras kill→valuta→uppgradera-loop är vår förlaga, men deras spel saknar vägval, fog of war och formationslager.

## Nästa steg

Fas 1: vertikal skiva — en spelbar skogsbana som bevisar hela loopen på fysisk hårdvara.
