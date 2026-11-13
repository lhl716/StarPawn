param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$testOutput=Join-Path ([System.IO.Path]::GetTempPath()) 'StarPawn-CommerceTests'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$dotnetRoot=Split-Path -Parent (Get-Command dotnet).Source
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$refs=Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref\8.0.30\ref\net8.0'
$json=Join-Path $GameDir 'Probably Stolen_Data\Managed\Newtonsoft.Json.dll'
$compileArgs=@('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable',('-out:'+(Join-Path $testOutput 'CommerceHarness.dll')),('-reference:'+$json))
$compileArgs+=@(Get-ChildItem -LiteralPath $refs -Filter '*.dll'|ForEach-Object{'-reference:'+$_.FullName})
$compileArgs+=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'source\Core') -Filter '*.cs'|Select-Object -ExpandProperty FullName)
$compileArgs+=(Join-Path $projectRoot 'source\Game\CommerceActions.cs')
$compileArgs+=(Join-Path $projectRoot 'source\Game\NativeSourcing.cs')
$compileArgs+=(Join-Path $PSScriptRoot 'CommerceHarness.cs')
& dotnet $compiler @compileArgs
if($LASTEXITCODE -ne 0){throw 'Commerce test compilation failed'}
# Local runtime dependency only, outside the deliverable; not redistributed with the mod.
Copy-Item -LiteralPath $json -Destination (Join-Path $testOutput 'Newtonsoft.Json.dll') -Force
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testOutput 'CommerceHarness.runtimeconfig.json') -Encoding utf8
& dotnet (Join-Path $testOutput 'CommerceHarness.dll')
if($LASTEXITCODE -ne 0){throw 'Commerce invariant test failed'}
