$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$testOutput=Join-Path ([System.IO.Path]::GetTempPath()) 'StarPawn-BankCardTests'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$dotnetRoot=Split-Path -Parent (Get-Command dotnet).Source
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$refs=Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref\8.0.30\ref\net8.0'
$compileArgs=@('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable',('-out:'+(Join-Path $testOutput 'BankCardsHarness.dll')))
$compileArgs+=@(Get-ChildItem -LiteralPath $refs -Filter '*.dll'|ForEach-Object{'-reference:'+$_.FullName})
$compileArgs+=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'source\Core') -Filter '*.cs'|Select-Object -ExpandProperty FullName)
$compileArgs+=(Join-Path $projectRoot 'source\Game\BankCardsRoot.cs')
$compileArgs+=(Join-Path $PSScriptRoot 'BankCardsHarness.cs')
& dotnet $compiler @compileArgs
if($LASTEXITCODE -ne 0){throw 'Bank card test compilation failed'}
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $testOutput 'BankCardsHarness.runtimeconfig.json') -Encoding utf8
& dotnet (Join-Path $testOutput 'BankCardsHarness.dll')
if($LASTEXITCODE -ne 0){throw 'Bank card policy test failed'}
