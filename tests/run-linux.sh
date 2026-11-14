#!/usr/bin/env bash
# Linux runner for the pure/harness C# suites. Needs a dotnet host, Roslyn csc.dll,
# net8.0 reference assemblies and Newtonsoft.Json; see docs/ENGINEERING_REPORT_05.md.
# These are business/harness tests. They do not load the game or Unity.
set -u
DN=${DN:-/opt/dn}
DOTNET=$DN/sdk/dotnet; CSC=$DN/x/toolset/tasks/netcore/bincore/csc.dll
NET8=$DN/x/microsoft.netcore.app.ref/ref/net8.0; JSON=$DN/x/nj/lib/netstandard2.0/Newtonsoft.Json.dll
export DOTNET_ROOT=$DN/sdk
ROOT=$(cd "$(dirname "$0")/.." && pwd); OUT=${OUT:-/tmp/starpawn-tests}; mkdir -p "$OUT"
REFS=$(ls $NET8/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
RC='{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
fail=0
run() { # name, extra csc args..., then sources
  local name=$1; shift
  if ! $DOTNET $CSC -nologo -target:exe -nostdlib+ -langversion:8.0 -nullable:enable -nowarn:CS1701 $REFS -r:$JSON -out:$OUT/$name.dll "$@" > $OUT/$name.build 2>&1; then
    echo "BUILD FAIL $name"; head -15 $OUT/$name.build; fail=1; return; fi
  cp -f $JSON $OUT/; echo "$RC" > $OUT/$name.runtimeconfig.json
  local a=("$ROOT"); [ "${NOARG:-}" = 1 ] && a=(); [ "$name" = ContentBootstrapTests ] && a=("$ROOT" "$OUT/fixtures")
  if (cd $OUT && $DOTNET $name.dll "${a[@]}") > $OUT/$name.out 2>&1; then echo "OK   $name: $(tail -1 $OUT/$name.out)"; else echo "FAIL $name"; tail -8 $OUT/$name.out; fail=1; fi
}
CORE=$(ls $ROOT/source/Core/*.cs)
for t in "$@"; do case $t in
  core) run CoreTests $ROOT/source/Core/Inspection.cs $ROOT/tests/CoreTests.cs;;
  content) NOARG=1 run ContentTests $CORE $ROOT/tests/ContentTests.cs;;
  economy) run EconomyTests $CORE $ROOT/tests/EconomyTests.cs;;
  appraisal) run AppraisalTests $CORE $ROOT/tests/AppraisalTests.cs;;
  bootstrap) run ContentBootstrapTests $CORE $ROOT/source/Game/ContentBootstrap.cs $ROOT/tests/ContentBootstrapTests.cs;;
  commerce) run CommerceHarness $CORE $ROOT/source/Game/CommerceActions.cs $ROOT/source/Game/NativeSourcing.cs $ROOT/tests/CommerceHarness.cs;;
  cards) run BankCardsHarness $CORE $ROOT/source/Game/BankCardsRoot.cs $ROOT/tests/BankCardsHarness.cs;;
  unified) run UnifiedTests $CORE $ROOT/tests/UnifiedTests.cs;;
  stubs) if $DOTNET $CSC -nologo -target:library -nostdlib+ -langversion:8.0 -nullable:enable -nowarn:CS1701,CS0649,CS0169,CS0414,CS8618,CS0067 $REFS -r:$JSON -r:$DN/x/harmony/lib/net48/0Harmony.dll -out:$OUT/StarPawn.GameStubCheck.dll $CORE $ROOT/source/Game/*.cs $ROOT/tests/stubs/*.cs > $OUT/stubs.build 2>&1; then echo "OK   stub compile of source/Game (type-check against tests/stubs, NOT the real game assemblies)"; else echo "FAIL stub compile"; grep -c "error" $OUT/stubs.build; grep error $OUT/stubs.build | head -25; fail=1; fi;;
esac; done
exit $fail
