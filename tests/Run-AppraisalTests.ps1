param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$dotnetExe=(Get-Command dotnet).Source
$dotnetRoot=Split-Path -Parent $dotnetExe
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$output=Join-Path $PSScriptRoot 'appraisal-bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$runtime=Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object {[Version]$_.Name} -Descending | Select-Object -First 1
$json=Join-Path $GameDir 'Probably Stolen_Data\Managed\Newtonsoft.Json.dll'
$testArgs=@('-nologo','-target:exe','-nostdlib+','-langversion:8.0','-nullable:enable','-warnaserror+',('-out:'+(Join-Path $output 'AppraisalTests.dll')),('-reference:'+$json))
$testArgs+=@(Get-ChildItem -LiteralPath (Join-Path $runtime.FullName 'ref\net8.0') -Filter '*.dll' | ForEach-Object {'-reference:'+$_.FullName})
$testArgs+=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'source\Core') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$testArgs+=(Join-Path $PSScriptRoot 'AppraisalTests.cs')
& $dotnetExe $compiler @testArgs
if($LASTEXITCODE -ne 0){throw 'Appraisal test compilation failed'}
Copy-Item -LiteralPath $json -Destination $output -Force
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath (Join-Path $output 'AppraisalTests.runtimeconfig.json') -Encoding utf8
& $dotnetExe (Join-Path $output 'AppraisalTests.dll') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Appraisal test failed'}
