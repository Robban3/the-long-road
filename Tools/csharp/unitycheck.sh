#!/usr/bin/env sh
# Catches the errors in the Unity-only assemblies that `typecheck.sh` cannot see.
#
# `typecheck.sh` builds Sim, Gen and the tests, which is everything that compiles
# without an engine — and that leaves View, App and Editor unchecked. Those are
# three quarters of the code that draws anything, and the only compiler that has
# ever seen them is the one inside Unity, on somebody else's machine, after a
# push. A mistake there costs a round trip and a Safe Mode dialog.
#
# The first version of this script handed those files to Roslyn with no references
# at all and filtered the resulting flood. It caught duplicate methods and unclosed
# braces and nothing else, for a reason worth recording: **csc does not bind method
# bodies at all when the declaration phase has errors.** It cannot emit, so it does
# not try. With no references every signature mentioning a Transform is an error,
# so no body in View was ever read — and a shadowed local that broke the build
# sailed through a clean run of this script.
#
# So give it references. Sim and Gen come from the assembly `typecheck.sh` builds,
# which makes TileGrid and DeterministicRandom real. UnityEngine cannot, so the
# engine types are stood in for: every name still unresolved after a first pass is
# declared as a permissive class, and every namespace anybody imports is declared
# so no `using` can fail — a failed using suppresses every diagnostic in its file,
# which is the same blindness in miniature. The stand-ins take a `params object[]`
# constructor so `[Header("…")]` and `new Vector3(x, y, z)` both bind.
#
# What survives is a large amount of noise about members the stand-ins do not have,
# and — because the bodies are read now — the errors that live inside them:
#
#   CS0136  a local shadowing another        CS0111  a method defined twice
#   CS1503  the wrong argument type          CS0101  a type defined twice
#   CS0128  a local declared twice           CS1513  } expected
#
# It is not a substitute for Unity's own compile: anything reached through an
# engine type is invisible here. It is the half of it that can be had in fifteen
# seconds, before pushing.
set -e
cd "$(dirname "$0")/../.."

CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
if [ -z "$CSC" ]; then echo "no Roslyn under /usr/lib/dotnet/sdk"; exit 2; fi

REFDIR=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net* 2>/dev/null | tail -1)
if [ -z "$REFDIR" ]; then echo "no reference assemblies under /usr/lib/dotnet/packs"; exit 2; fi

# Sim and Gen, real, from the build typecheck.sh uses.
SIM=Tools/csharp/bin/Debug/net8.0/ArnaTests.dll
[ -f "$SIM" ] || dotnet build Tools/csharp/Typecheck.csproj -v quiet --nologo >/dev/null

REFS=$(for f in "$REFDIR"/*.dll; do printf ' -r:%s' "$f"; done)
REFS="$REFS -r:$SIM"

SOURCES=$(find Assets/_Project/Scripts/View Assets/_Project/Scripts/App \
               Assets/Editor Assets/_Project/Scripts/UI Assets/_Project/Scripts/Data \
               -name '*.cs' 2>/dev/null)

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# Every namespace anybody imports, so that no using directive can fail. Declaring a
# type inside System or Arna.Sim alongside the real ones is legal and harmless.
grep -h '^ *using [A-Za-z]' $SOURCES | sed 's/^ *using *//; s/;.*//' \
  | grep -v '=' | grep -v '^static ' | sort -u > "$WORK/ns"

# Pass one: what is still missing once Sim, Gen and the framework are known.
dotnet "$CSC" -nologo -noconfig -nostdlib+ -langversion:9 -target:library \
       -out:"$WORK/pass1.dll" $REFS $SOURCES 2>&1 \
  | grep -oE "type or namespace name '[A-Za-z0-9_]+'" \
  | sed "s/.*'\(.*\)'/\1/" | sed 's/Attribute$//' | sort -u > "$WORK/names"

sed 's/\..*//' "$WORK/ns" | sort -u > "$WORK/roots"
comm -23 "$WORK/names" "$WORK/roots" > "$WORK/missing"

{
  echo "// Stand-ins, generated. See the comment at the top of unitycheck.sh."
  while read -r n; do echo "namespace $n { class __UnitycheckExists { } }"; done < "$WORK/ns"
  while read -r n; do
    case "$n" in
      I[A-Z]*) echo "public interface $n { }" ;;
      *)       echo "public class $n : System.Attribute { public $n(params object[] _) { } }" ;;
    esac
  done < "$WORK/missing"
} > "$WORK/Stubs.cs"

# Everything a missing reference produces, and nothing else. Anything outside this
# list is a real fault in the source.
NOISE='CS0518|CS0246|CS0234|CS0656|CS8179|CS8137|CS0012|CS1069|CS0433|CS0103|CS0117|CS1061|CS0122|CS0029|CS1729|CS0165|CS0161|CS0019|CS0021|CS0023|CS0030|CS0031|CS0034|CS0035|CS0119|CS0120|CS0123|CS0126|CS0138|CS0173|CS0176|CS0182|CS0184|CS0186|CS0193|CS0202|CS0205|CS0266|CS0267|CS0304|CS0305|CS0306|CS0308|CS0310|CS0311|CS0314|CS0403|CS0407|CS0411|CS0413|CS0426|CS0428|CS0446|CS0453|CS0457|CS0462|CS0464|CS0570|CS0571|CS0572|CS0584|CS0611|CS0616|CS0619|CS0648|CS0721|CS0724|CS0742|CS0748|CS0754|CS0759|CS0762|CS0765|CS0834|CS0841|CS0844|CS1001|CS1540|CS1545|CS1546|CS1579|CS1593|CS1620|CS1621|CS1640|CS1656|CS1660|CS1661|CS1662|CS1674|CS1715|CS1739|CS1740|CS1750|CS1928|CS1929|CS1936|CS1955|CS1973|CS1978|CS1979|CS1983|CS1988|CS1994|CS7036|CS8028|CS8058|CS8070|CS8072|CS8107|CS8130|CS8131|CS8156|CS8188|CS8198|CS8305|CS8352|CS8355|CS8377|CS8400|CS8401|CS8403|CS8410|CS8416|CS8640|CS8641|CS8652|CS8773|CS8803|CS8805|CS8813|CS8919|CS9035'

REAL=$(dotnet "$CSC" -nologo -noconfig -nostdlib+ -langversion:9 -target:library \
              -out:"$WORK/pass2.dll" $REFS $SOURCES "$WORK/Stubs.cs" 2>&1 \
       | grep "error CS" | grep -vE "$NOISE" | sort -u || true)

if [ -n "$REAL" ]; then
    echo "$REAL"
    exit 1
fi

echo "no reference-independent errors in View, App or Editor"
