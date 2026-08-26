#!/usr/bin/env sh
# Catches the errors in the Unity-only assemblies that `typecheck.sh` cannot see.
#
# `typecheck.sh` builds Sim, Gen and the tests, which is everything that compiles
# without an engine — and that leaves View, App and Editor unchecked. Those are
# three quarters of the code that draws anything, and the only compiler that has
# ever seen them is the one inside Unity, on somebody else's machine, after a
# push. A duplicate method there costs a round trip and a Safe Mode dialog.
#
# So: hand every one of those files to Roslyn with **no references at all**. Most
# of what comes back is noise — a thousand "type not found" for UnityEngine — but
# the errors that do not depend on knowing what a GameObject is come back too, and
# those are the ones worth having:
#
#   CS0111  a method defined twice          CS1002  ; expected
#   CS0101  a type defined twice            CS1513  } expected
#   CS0128  a local declared twice          CS1525  invalid expression
#
# It is not a substitute for Unity's own compile. It is the half of it that can be
# had here, in four seconds, before pushing.
set -e
cd "$(dirname "$0")/../.."

CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
if [ -z "$CSC" ]; then echo "no Roslyn under /usr/lib/dotnet/sdk"; exit 2; fi

SOURCES=$(find Assets/_Project/Scripts/View Assets/_Project/Scripts/App \
               Assets/Editor Assets/_Project/Scripts/UI Assets/_Project/Scripts/Data \
               -name '*.cs' 2>/dev/null)

# Everything a missing reference produces, and nothing else. Anything outside this
# list is a real fault in the source.
NOISE='CS0518|CS0246|CS0234|CS0656|CS8179|CS8137|CS0012|CS1069|CS0433|CS0103|CS0117|CS1061|CS0122|CS0029|CS1503|CS1729|CS0165|CS0161|CS0019|CS0021|CS0023|CS0030|CS0031|CS0034|CS0035|CS0119|CS0120|CS0123|CS0126|CS0173|CS0176|CS0182|CS0184|CS0186|CS0193|CS0202|CS0205|CS0266|CS0267|CS0304|CS0305|CS0306|CS0308|CS0310|CS0311|CS0314|CS0403|CS0407|CS0411|CS0413|CS0428|CS0446|CS0453|CS0457|CS0462|CS0464|CS0518|CS0570|CS0571|CS0572|CS0584|CS0611|CS0616|CS0619|CS0648|CS0656|CS0721|CS0724|CS0742|CS0748|CS0754|CS0759|CS0762|CS0765|CS0834|CS0841|CS0844|CS1001|CS1503|CS1540|CS1545|CS1546|CS1579|CS1593|CS1620|CS1621|CS1640|CS1656|CS1660|CS1661|CS1662|CS1674|CS1715|CS1739|CS1740|CS1750|CS1928|CS1929|CS1936|CS1955|CS1973|CS1978|CS1979|CS1983|CS1988|CS1994|CS7036|CS8028|CS8058|CS8070|CS8072|CS8107|CS8130|CS8131|CS8156|CS8188|CS8198|CS8305|CS8352|CS8355|CS8377|CS8400|CS8401|CS8403|CS8410|CS8416|CS8640|CS8641|CS8652|CS8773|CS8803|CS8805|CS8813|CS8919|CS9035'

REAL=$(dotnet "$CSC" -nologo -noconfig -nostdlib+ -langversion:9 \
         -target:library -out:/tmp/arna-unitycheck.dll $SOURCES 2>&1 \
       | grep "error CS" | grep -vE "$NOISE" || true)

if [ -n "$REAL" ]; then
    echo "$REAL"
    exit 1
fi

echo "no reference-independent errors in View, App or Editor"
