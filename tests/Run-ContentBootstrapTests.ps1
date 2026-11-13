param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$deliveryDir=Split-Path -Parent $PSScriptRoot
$workspaceDir=$deliveryDir
$dotnetExe=(Get-Command dotnet).Source
$dotnetRoot=Split-Path -Parent $dotnetExe
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$outputDir=Join-Path $workspaceDir ('work\content-bootstrap-tests\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$coreDll=Join-Path $outputDir 'StarPawn.Core.dll'
$coreArgs=@('-nologo','-target:library','-nostdlib+','-langversion:8.0','-nullable:enable','-warnaserror+',('-out:'+$coreDll))
Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1') -Filter '*.dll' | ForEach-Object {$coreArgs+='-reference:'+$_.FullName}
$coreArgs+=@(Get-ChildItem -LiteralPath (Join-Path $deliveryDir 'source\Core') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
& $dotnetExe $compiler @coreArgs
if($LASTEXITCODE -ne 0){throw 'Bootstrap core compile failed'}
$runtime=Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object {[Version]$_.Name} -Descending | Select-Object -First 1
$testDll=Join-Path $outputDir 'ContentBootstrapTests.dll'
$testArgs=@('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable','-warnaserror+',('-out:'+$testDll),('-reference:'+$coreDll),('-reference:'+(Join-Path $GameDir 'Probably Stolen_Data\Managed\Newtonsoft.Json.dll')))
Get-ChildItem -LiteralPath (Join-Path $runtime.FullName 'ref\net8.0') -Filter '*.dll' | ForEach-Object {$testArgs+='-reference:'+$_.FullName}
& $dotnetExe $compiler @testArgs (Join-Path $PSScriptRoot 'ContentBootstrapTests.cs') (Join-Path $deliveryDir 'source\Game\ContentBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Bootstrap test compile failed'}
Copy-Item -LiteralPath (Join-Path $GameDir 'Probably Stolen_Data\Managed\Newtonsoft.Json.dll') -Destination (Join-Path $outputDir 'Newtonsoft.Json.dll')
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $outputDir 'ContentBootstrapTests.runtimeconfig.json') -Encoding utf8
& $dotnetExe $testDll $deliveryDir (Join-Path $outputDir 'fixtures')
if($LASTEXITCODE -ne 0){throw 'Bootstrap tests failed'}
