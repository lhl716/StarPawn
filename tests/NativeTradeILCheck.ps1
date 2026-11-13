param([string]$GameDir='D:\Program Files (x86)\steam\steamapps\common\Probably Stolen')
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Probably Stolen_Data\Managed'
# Cecil parses metadata/IL without loading or executing the game assembly.
# The report includes method signatures and counts, not game implementations.
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'))
function Get-AllTypes($types){foreach($type in $types){$type;Get-AllTypes $type.NestedTypes}}
try {
 $types=@(Get-AllTypes $assembly.MainModule.Types)
 $player=$types | Where-Object FullName -eq 'PlayerStore'
 $sites=@()
 foreach($name in @('BuyItem','SellItem','CanBuyItemFromTable')){
  foreach($method in @($player.Methods | Where-Object Name -eq $name)){
   $calls=@($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'GameItem' -and $_.Operand.Name -eq 'GetNegociatedValue'})
   $sites+= [pscustomobject]@{Method=$method.FullName;Count=$calls.Count}
  }
 }
 $callers=@()
 foreach($type in $types){foreach($method in $type.Methods){if(!$method.HasBody){continue};foreach($instruction in $method.Body.Instructions){
  if($instruction.Operand -isnot [Mono.Cecil.MethodReference]){continue}
  $callee=$instruction.Operand
  if($callee.DeclaringType.FullName -eq 'PlayerStore' -and $callee.Name -in @('BuyItem','SellItem')){
   $moneyCalls=@($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('PayMoney','ModCash')})
   $cashWrites=@($method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.DeclaringType.FullName -eq 'PlayerStore' -and $_.Operand.Name -eq 'playerCash'})
   $callers+=[pscustomobject]@{Caller=$method.FullName;Callee=$callee.FullName;DirectWalletCallCount=$moneyCalls.Count;DirectCashFieldWriteCount=$cashWrites.Count}
  }
 }}}
 $report=[pscustomobject]@{
  Mode='Read-only installed-game IL metadata. No game execution or runtime Harmony patch verification.'
  GameAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll') -Algorithm SHA256).Hash
  PriceCallSites=$sites
  DirectNativeBuySellCallers=$callers
 }
 $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'native-trade-il-results.json') -Encoding UTF8
 $sites | Format-Table -AutoSize
 if($sites.Count -ne 3 -or @($sites|Where-Object Count -ne 1).Count -ne 0){throw 'Expected exactly one negotiated-value call in each of the three native sites'}
 if(@($callers|Where-Object {$_.DirectWalletCallCount -ne 0 -or $_.DirectCashFieldWriteCount -ne 0}).Count -ne 0){throw 'A direct native trade caller also writes cash; inspect the report'}
 Write-Output ('PASS: '+$callers.Count+' direct native BuyItem/SellItem call sites have no additional direct cash writes.')
}finally{$assembly.Dispose()}
