param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$dotnetExe=(Get-Command dotnet).Source
$dotnetRoot=Split-Path -Parent $dotnetExe
$compiler=Join-Path $dotnetRoot 'sdk\8.0.424\Roslyn\bincore\csc.dll'
$refs=Join-Path $dotnetRoot 'packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1'
$managed=Join-Path $GameDir 'Probably Stolen_Data\Managed'
$output=Join-Path (Split-Path -Parent $PSScriptRoot) 'mod'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$baseArgs=@('-nologo','-target:library','-nostdlib+','-langversion:8.0','-nullable:enable','-deterministic+','-warnaserror+')
$baseArgs+=@(Get-ChildItem -LiteralPath $refs -Filter '*.dll' | ForEach-Object {'-reference:'+$_.FullName})
$coreOutput=Join-Path $output 'StarPawn.Core.dll'
$coreArgs=$baseArgs+@('-out:'+$coreOutput)+@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Core') -Filter '*.cs' -Recurse | Select-Object -ExpandProperty FullName)
& $dotnetExe $compiler @coreArgs
if($LASTEXITCODE -ne 0){throw 'Core compile failed'}
# The game ships Newtonsoft.Json built for netstandard2.0. The mod targets the
# compatible netstandard2.1 reference pack. Keep CS1701 visible, but do not promote
# this assembly-version-unification warning to an error.
$gameArgs=$baseArgs+@('-warnaserror-:1701',('-out:'+(Join-Path $output 'StarPawn.Game.dll')),('-reference:'+$coreOutput))
foreach($name in @('Assembly-CSharp','UnityEngine','UnityEngine.CoreModule','UnityEngine.InputLegacyModule','UnityEngine.IMGUIModule','UnityEngine.UI','Unity.TextMeshPro','UnityEngine.ImageConversionModule','Newtonsoft.Json','0Harmony')){
 $gameArgs+='-reference:'+(Join-Path $managed ($name+'.dll'))
}
$gameArgs+=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Game') -Filter '*.cs' -Recurse | Select-Object -ExpandProperty FullName)
& $dotnetExe $compiler @gameArgs
if($LASTEXITCODE -ne 0){throw 'Game compile failed'}
Write-Output 'StarPawn.Core and StarPawn.Game compiled against local game assemblies. No deployment performed.'
