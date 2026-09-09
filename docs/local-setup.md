# Att köra Claude Code lokalt, med Unity

## Varför det är värt besväret

I dag kör sessionen i en molncontainer. Den ser bara det som ligger i git — ingen
Unity, inga modeller (varje `.fbx` är en Git LFS-pekare), ingen konsol. Följden är att
varje fel måste gå genom dig: du kör, du ser något, du klistrar in en loggrad, jag mäter i
koden eller i Python-porten och gissar mig till resten.

En session som kör **på din dator** kan i stället:

- bygga en bana och läsa Unitys konsol själv
- köra `The Veil > Set Up Project` utan att du öppnar menyn
- köra hela EditMode-sviten i Unitys egen testkörare, inte bara i Roslyn-porten
- se att en fil faktiskt kompilerar innan den pushas — vilket `unitycheck.sh` bevisligen
  inte alltid gör

Det som *inte* ändras: att titta på spelet medan det körs är fortfarande ditt jobb.
Batch-läge ritar ingenting.

---

## Steg 1 — Claude Code på datorn

Installera Claude Code på Windows och starta den i projektmappen:

```
cd C:\Users\robvan\projekt\legend of arna
claude
```

Samma repo, samma historik, samma `main`. Ingenting behöver flyttas.

## Steg 2 — säg var Unity ligger

Unity Hub lägger editorn under en versionsmapp. Projektet står på **6000.3.22f1**
(`ProjectSettings/ProjectVersion.txt`), så sökvägen är:

```
C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe
```

Kontrollera att den finns innan något annat. Är versionen en annan öppnar Unity projektet
i uppgraderingsläge, vilket skriver om tillgångar.

## Steg 3 — de tre kommandon som gör jobbet

**Unity kan bara hålla ett projekt öppet i taget.** Editorn måste vara stängd när något av
de här körs, annars låser den projektmappen och kommandot faller på en låsfil.

Nyckeln i alla tre är `-logFile -`: den skickar loggen till stdout i stället för till en
fil, vilket är hela skillnaden mellan att jag kan läsa den och att du får klistra in den.

### Kör uppsättningen

```
"C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" ^
  -batchmode -quit -logFile - ^
  -projectPath "C:\Users\robvan\projekt\legend of arna" ^
  -executeMethod TheVeil.Editor.TheVeilSetup.SetupProject
```

Det är samma sak som menyvalet `The Veil > Set Up Project`. Metoden är publik och statisk,
vilket är vad `-executeMethod` kräver — den fullständiga namnet står i
`Assets/Editor/TheVeilSetup.cs:24`.

Samma mönster når varje annan rapport i den filen, till exempel
`TheVeil.Editor.TheVeilSetup.ReportModelDimensions` — som mäter modellernas verkliga
storlek, alltså precis det jag har fått gissa mig till hela vägen.

### Kör Unitys egen testsvit

```
"C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" ^
  -batchmode -logFile - ^
  -projectPath "C:\Users\robvan\projekt\legend of arna" ^
  -runTests -testPlatform EditMode ^
  -testResults "C:\Users\robvan\projekt\legend of arna\TestResults.xml"
```

Inget `-quit` här — testköraren avslutar själv, och `-quit` skulle döda den på vägen.

Det här kör testerna i **Unity**, mot de riktiga assemblies. `Tools/csharp/typecheck.sh`
kör samma 271 tester mot en Roslyn-byggd kopia, vilket är snabbt och blint för allt som
Unity gör med sina egna referenser.

### Läs konsolen efteråt

Editorns logg ligger på:

```
%LOCALAPPDATA%\Unity\Editor\Editor.log
```

Där hamnar allt som `Debug.Log` skriver, inklusive raderna som är byggda för just det här:

| rad | vad den svarar på |
|---|---|
| `[The Veil] Tallest built (height x width)` | hur stort allt faktiskt blev |
| `[The Veil] Bridge ... roadway ... m above the bank` | om bron mäts eller inte |
| `[The Veil] N bridge(s) found` | om kolonnen lyfts upp på den |
| `[The Veil] wagons at ... m` | vilka modeller som är inkopplade |

## Steg 4 — Unity-MCP, om du vill ha mer

Batch-läget ovan täcker det mesta och behöver **inget plugin**. Vill du utöver det ha
levande kontroll över editorn — gå in i play mode, läsa konsolen medan den kör, inspektera
ett objekt i scenen — är det en MCP-server för Unity som gäller.

Där kan jag inte rekommendera ett specifikt paket. Jag når varken Asset Store eller
paketregistren härifrån, och att peka ut ett namn jag inte kan granska vore samma sorts
gissning som har kostat oss tid redan. Leta själv, och installera det i den **lokala**
sessionen — ett plugin i molnsessionen når ändå ingen Unity.

---

## Vad som fortfarande är ditt

Batch-läge ritar inte. Det bygger banan, kör koden och skriver loggar, men ingen bild
kommer ut. Så det här förblir dina ögon:

- ser tornet för stort ut
- glider djuren
- står husen i varandra
- rider kolonnen på bron

Skillnaden är att jag inte längre behöver *gissa* siffrorna bakom det du ser. Du säger vad
som ser fel ut, jag mäter det själv i samma minut.
