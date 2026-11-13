param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$managed=Join-Path $GameDir 'Probably Stolen_Data\Managed'
$source=Join-Path $projectRoot 'source'
$auditOut=Join-Path ([IO.Path]::GetTempPath()) ('starpawn-native-trade-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $auditOut -Force | Out-Null
$dotnetExe=(Get-Command dotnet).Source
$dotnetRoot=Split-Path -Parent $dotnetExe
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$refPack=Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object Name -Descending | Select-Object -First 1
if($null -eq $refPack){throw '.NET 8 reference pack is required.'}
$refPath=Join-Path $refPack.FullName 'ref\net8.0'
$rootText=Get-Content -LiteralPath (Join-Path $source 'Game\ModEntry.cs') -Raw
$dtoStart=$rootText.IndexOf('public sealed class RunState')
$dtoEnd=$rootText.IndexOf('public sealed partial class ModEntry')
if($dtoStart -lt 0 -or $dtoEnd -le $dtoStart){throw 'Run DTO declarations were not found.'}
$dto='using System; using System.Collections.Generic; using StarPawn.Core; namespace StarPawn.Game {'+$rootText.Substring($dtoStart,$dtoEnd-$dtoStart)+'}'
$dtoPath=Join-Path $auditOut 'RunDtos.cs'
[IO.File]::WriteAllText($dtoPath,$dto)
$executable=Join-Path $auditOut 'NativeTradeAudit.dll'
$argsList=@('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable',('-out:'+$executable))
$argsList+=@(Get-ChildItem -LiteralPath $refPath -Filter '*.dll' | ForEach-Object {'-reference:'+$_.FullName})
$argsList+=('-reference:'+(Join-Path $managed 'Newtonsoft.Json.dll'))
$argsList+=@(Get-ChildItem -LiteralPath (Join-Path $source 'Core') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$argsList+=@((Join-Path $source 'Game\NativeTradeRoot.cs'),(Join-Path $source 'Game\NativeTradeBridge.cs'),$dtoPath,(Join-Path $PSScriptRoot 'NativeTradeAuditHost.cs'))
& $dotnetExe $compiler @argsList
if($LASTEXITCODE -ne 0){throw 'Native trade test compilation failed'}
Copy-Item -LiteralPath (Join-Path $managed 'Newtonsoft.Json.dll') -Destination $auditOut -Force
$runtime='{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
[IO.File]::WriteAllText((Join-Path $auditOut 'NativeTradeAudit.runtimeconfig.json'),$runtime)
$testOutput=& $dotnetExe $executable 2>&1
$testExit=$LASTEXITCODE
$testOutput | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'native-trade-results.txt') -Encoding UTF8
$testOutput
Write-Output ('Temporary test binaries: '+$auditOut)
if($testExit -ne 0){throw 'Native trade tests failed; inspect native-trade-results.txt'}
