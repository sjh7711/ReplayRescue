$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$nativeKey = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.local.replayrescue'
$manifest = Join-Path $root 'native-host.json'
if (Test-Path -LiteralPath $nativeKey) {
  $registered = (Get-Item -LiteralPath $nativeKey).GetValue('')
  if ($registered -eq $manifest) { Remove-Item -LiteralPath $nativeKey }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$value = Get-ItemPropertyValue -LiteralPath $runKey -Name 'ReplayRescue' -ErrorAction SilentlyContinue
if ($value -eq ('"' + (Join-Path $root 'ReplayRescue.exe') + '" --tray')) { Remove-ItemProperty -LiteralPath $runKey -Name 'ReplayRescue' }
Write-Output 'Startup and native host registration removed. Exit the tray app and remove the Chrome extension. All files and logs have been preserved.'
