param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$managed=Join-Path $GameDir 'Probably Stolen_Data\Managed'
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'))
$mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $projectRoot 'mod\StarPawn.Game.dll'))
$checked=0
$failures=[System.Collections.Generic.List[string]]::new()
try {
 foreach($type in $mod.MainModule.Types){
  $patches=@($type.CustomAttributes|Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'})
  if($patches.Count -eq 0){continue}
  $targetType=$null;$targetName=$null;$argumentTypes=$null
  foreach($patch in $patches){foreach($argument in $patch.ConstructorArguments){
   if($argument.Type.FullName -eq 'System.Type'){$targetType=$argument.Value.FullName}
   elseif($argument.Type.FullName -eq 'System.String'){$targetName=[string]$argument.Value}
   elseif($argument.Type.FullName -eq 'System.Type[]'){$argumentTypes=@($argument.Value|ForEach-Object {$_.Value.FullName})}
  }}
  if(!$targetType -or !$targetName){$failures.Add($type.FullName+': dynamic target requires an explicit metadata check');continue}
  $originalType=$game.MainModule.Types|Where-Object FullName -eq $targetType
  $originals=@($originalType.Methods|Where-Object Name -eq $targetName)
  if($null -ne $argumentTypes){$originals=@($originals|Where-Object {(@($_.Parameters|ForEach-Object {$_.ParameterType.FullName}) -join '|') -eq ($argumentTypes -join '|')})}
  if($originals.Count -ne 1){$failures.Add($type.FullName+': target not unique ('+$originals.Count+')');continue}
  $original=$originals[0]
  foreach($method in $type.Methods|Where-Object {$_.Name -in @('Prefix','Postfix','Finalizer')}){
   foreach($parameter in $method.Parameters){
    $expected=$null;$name=$parameter.Name
    if($name -eq '__instance'){$expected=$targetType}
    elseif($name -eq '__result'){$expected=$original.ReturnType.FullName}
    elseif($name -eq '__exception'){$expected='System.Exception'}
    elseif($name -in @('__state','__originalMethod','__args','__runOriginal')){continue}
    elseif($name -match '^__(\d+)$'){
     $index=[int]$Matches[1]
     if($index -ge $original.Parameters.Count){$failures.Add($type.Name+'.'+$method.Name+': index '+$index+' missing');continue}
     $expected=$original.Parameters[$index].ParameterType.FullName
    }
    else {
     $matching=@($original.Parameters|Where-Object Name -eq $name)
     if($matching.Count -ne 1){$failures.Add($type.Name+'.'+$method.Name+': named parameter '+$name+' missing');continue}
     $expected=$matching[0].ParameterType.FullName
    }
    $actual=$parameter.ParameterType.FullName.TrimEnd('&')
    if($actual -ne $expected.TrimEnd('&')){$failures.Add($type.Name+'.'+$method.Name+': '+$name+' expected '+$expected+' got '+$actual)}
    $checked++
   }
  }
 }
 if($failures.Count -gt 0){$failures|ForEach-Object {Write-Output ('FAIL '+$_)};throw 'Harmony metadata binding check failed'}
 Write-Output ('HARMONY_METADATA_PARAMETERS_PASSED='+$checked)
 Write-Output 'Checked compiled patch parameter names, positional indices, types, target overload uniqueness against local Assembly-CSharp.dll.'
 Write-Output 'This is metadata validation, not execution of Harmony patch generation or Unity gameplay.'
}
finally {$mod.Dispose();$game.Dispose()}
