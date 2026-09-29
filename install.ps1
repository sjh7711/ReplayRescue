param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = Join-Path $root 'ReplayRescue.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'ReplayRescue.exe is missing. Run build.ps1 first.' }
$hostManifest = Join-Path $root 'native-host.json'
$extensionId = [IO.File]::ReadAllText((Join-Path $root 'extension-id.txt')).Trim()
if ($extensionId -notmatch '^[a-p]{32}$') { throw 'Invalid Chrome extension ID.' }
$hostData = [ordered]@{
    name = 'com.local.replayrescue'
    description = 'Local Replay Rescue domain bridge'
    path = $exe
    type = 'stdio'
    allowed_origins = @('chrome-extension://' + $extensionId + '/')
}
[IO.File]::WriteAllText($hostManifest, ($hostData | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$nativeKey = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.local.replayrescue'
New-Item -Path $nativeKey -Force | Out-Null
Set-Item -LiteralPath $nativeKey -Value $hostManifest
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -LiteralPath $runKey -Name 'ReplayRescue' -Value ('"' + $exe + '" --tray')
Write-Output 'Chrome native host registered for the current user. Login startup enabled.'
if (!$NoLaunch) { Start-Process -FilePath $exe -WindowStyle Hidden }
