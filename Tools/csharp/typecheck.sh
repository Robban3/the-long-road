#!/usr/bin/env bash
# Type-checks the engine-independent C# without opening Unity.
#
# Arna.Sim is compiled without engine references on purpose and Arna.Gen only depends
# on it, so both build with a plain compiler. The EditMode tests build too, against the
# NUnit stand-in beside this script — it asserts nothing, and exists only so a test that
# will not compile is caught here rather than by somebody opening the editor.
#
# Two mono limitations to know about, because one of them has already hidden a real
# error from me:
#
#   * Mono cannot parse C# local functions, so the four test files that use them are
#     skipped. Unity compiles them fine.
#   * Mono reports parse errors and then stops, before typing anything. A file it
#     cannot parse therefore masks real errors everywhere else — which is exactly how
#     a wrong constructor argument in LevelRunTests reached a push. Hence the skips
#     rather than tolerating the noise.
#
# View, App and Editor still need Unity: they use UnityEngine.
set -euo pipefail
cd "$(dirname "$0")/../.."

UNPARSEABLE='CombatTests|DeterministicRandomTests|RunEconomyTests|TroopUpgradeTests'
SIM=$(find Assets/_Project/Scripts/Sim Assets/_Project/Scripts/Gen -name '*.cs')
TESTS=$(find Assets/Tests/EditMode -name '*.cs' | grep -vE "$UNPARSEABLE")

mcs -langversion:latest -target:library -out:/tmp/arna-typecheck.dll \
    -nowarn:1591,1587,1574,0169,0219,0414 \
    Tools/csharp/NUnitShim.cs $SIM $TESTS 2>&1 | grep -E 'error CS' && exit 1

echo "Sim, Gen and the parseable EditMode tests type-check."
echo "Skipped (mono cannot parse local functions): $UNPARSEABLE"
