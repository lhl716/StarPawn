$ErrorActionPreference = 'Stop'
$deliveryDir = Split-Path -Parent $PSScriptRoot
$dotnetExe = (Get-Command dotnet).Source
$dotnetRoot = Split-Path -Parent $dotnetExe
$compiler = Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$outputDir = Join-Path $PSScriptRoot 'content-bin'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$coreDll = Join-Path $outputDir 'StarPawn.Content.dll'
$coreArgs = @('-nologo','-target:library','-nostdlib+','-langversion:8.0','-nullable:enable','-warnaserror+',('-out:'+$coreDll))
Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1') -Filter '*.dll' | ForEach-Object { $coreArgs += '-reference:' + $_.FullName }
$coreArgs += (Join-Path $deliveryDir 'source\Core\Content.cs')
$coreArgs += (Join-Path $deliveryDir 'source\Core\InspectionV2.cs')
$coreArgs += (Join-Path $deliveryDir 'source\Core\ContentPacks.cs')
$coreArgs += (Join-Path $deliveryDir 'source\Core\Economy.cs')
& $dotnetExe $compiler @coreArgs
if ($LASTEXITCODE -ne 0) { throw 'Content netstandard2.1 compile failed' }
$runtime = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
$testDll = Join-Path $outputDir 'ContentTests.dll'
$testArgs = @('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable','-warnaserror+',('-out:'+$testDll),('-reference:'+$coreDll))
Get-ChildItem -LiteralPath (Join-Path $runtime.FullName 'ref\net8.0') -Filter '*.dll' | ForEach-Object { $testArgs += '-reference:' + $_.FullName }
& $dotnetExe $compiler @testArgs (Join-Path $PSScriptRoot 'ContentTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Content test compile failed' }
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $outputDir 'ContentTests.runtimeconfig.json') -Encoding utf8
& $dotnetExe $testDll @args
if ($LASTEXITCODE -ne 0) { throw 'Content invariant tests failed' }
