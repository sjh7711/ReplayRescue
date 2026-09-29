param([string]$AppExe = (Join-Path $PSScriptRoot '..\ReplayRescue.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$AppExe = [IO.Path]::GetFullPath($AppExe)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('ReplayRescue-install-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = @'
using System;
using System.IO;
using System.Reflection;
using System.Threading;
#if NEW_VERSION
[assembly: AssemblyVersion("9.9.9.0")]
#else
[assembly: AssemblyVersion("1.0.0.0")]
#endif
class Fixture {
  static void Main(string[] args) {
    string root=AppDomain.CurrentDomain.BaseDirectory;
    if(args.Length>0 && (args[0]=="--hold" || args[0]=="--native-host")){
      while(!File.Exists(Path.Combine(root,args[0]=="--hold"?"exit-parent.flag":"exit-host.flag")))Thread.Sleep(30);
    }else File.WriteAllText(Path.Combine(root,"restarted.txt"),Assembly.GetExecutingAssembly().GetName().Version+" "+string.Join(" ",args));
  }
}
'@
$sourcePath = Join-Path $testRoot 'Fixture.cs'
[IO.File]::WriteAllText($sourcePath, $source)
foreach ($name in @('old','new')) {
    $directory = Join-Path $testRoot $name
    New-Item -ItemType Directory -Path $directory | Out-Null
    $options = @('/nologo','/target:winexe','/platform:x64',('/out:' + (Join-Path $directory 'ReplayRescue.exe')))
    if ($name -eq 'new') { $options += '/define:NEW_VERSION' }
    & $compiler @options $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed' }
}
foreach ($scenario in @('success','rollback')) {
    $root = Join-Path $testRoot $scenario
    $stage = Join-Path $root ('data\updates\' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'extension') | Out-Null
    Copy-Item -LiteralPath (Join-Path $testRoot 'old\ReplayRescue.exe') -Destination (Join-Path $root 'ReplayRescue.exe')
    Copy-Item -LiteralPath $AppExe -Destination (Join-Path $stage 'ReplayRescue.Updater.exe')
    [IO.File]::WriteAllText((Join-Path $root 'data\settings.json'), '{"PollSeconds":73,"Language":"ko","Enabled":false,"CheckUpdates":false}')
    [IO.File]::WriteAllText((Join-Path $root 'extension\manifest.json'), 'old manifest')
    [IO.File]::WriteAllText((Join-Path $root 'user-file.txt'), 'preserved')
    $archive = Join-Path $stage 'package.zip'
    $zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $testRoot 'new\ReplayRescue.exe'), 'ReplayRescue/ReplayRescue.exe') | Out-Null
        foreach ($item in @(@('extension-id.txt','edlganjmnhkmjlnekkbpnhdpaiillfgc'),@('extension/manifest.json','{"version":"9.9.9"}'),@('README.md','new readme'))) {
            $writer = New-Object IO.StreamWriter($zip.CreateEntry('ReplayRescue/' + $item[0]).Open())
            try { $writer.Write($item[1]) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    $parent = Start-Process -FilePath (Join-Path $root 'ReplayRescue.exe') -ArgumentList '--hold' -WindowStyle Hidden -PassThru
    $hostProcess = Start-Process -FilePath (Join-Path $root 'ReplayRescue.exe') -ArgumentList '--native-host' -WindowStyle Hidden -PassThru
    $plan = @{Root=$root;Tag='v9.9.9';Digest=('sha256:' + (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant());Size=(Get-Item -LiteralPath $archive).Length;ParentPid=$parent.Id;Tray=$true;Language='en'}
    $planPath = Join-Path $stage 'plan.json'
    [IO.File]::WriteAllText($planPath, ($plan | ConvertTo-Json))
    $locked = $null
    $helper = $null
    try {
        if ($scenario -eq 'rollback') { $locked = [IO.File]::Open((Join-Path $root 'extension\manifest.json'), [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        $helper = Start-Process -FilePath (Join-Path $stage 'ReplayRescue.Updater.exe') -ArgumentList '--apply-update',('"' + $planPath + '"') -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (!(Test-Path -LiteralPath (Join-Path $stage 'ready.json'))) {
            if ($helper.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Updater did not become ready' }
            Start-Sleep -Milliseconds 50
        }
        [IO.File]::WriteAllText((Join-Path $root 'exit-parent.flag'), 'exit')
        if (!$helper.WaitForExit(15000)) { throw 'Updater did not exit' }
        if ($locked) { $locked.Dispose(); $locked = $null }
        $deadline = [DateTime]::UtcNow.AddSeconds(5)
        while (!(Test-Path -LiteralPath (Join-Path $root 'restarted.txt'))) {
            if ([DateTime]::UtcNow -gt $deadline) { throw 'App did not restart' }
            Start-Sleep -Milliseconds 50
        }
        $result = Get-Content -LiteralPath (Join-Path $root 'data\update-result.json') -Raw | ConvertFrom-Json
        $expected = $scenario -eq 'success'
        if ($result.Success -ne $expected) { throw ('Unexpected update result: ' + ($result | ConvertTo-Json)) }
        $expectedVersion = if ($expected) {'9.9.9.0 --tray'} else {'1.0.0.0 --tray'}
        if ([IO.File]::ReadAllText((Join-Path $root 'restarted.txt')) -ne $expectedVersion) { throw 'Wrong version or restart mode' }
        $settings = Get-Content -LiteralPath (Join-Path $root 'data\settings.json') -Raw | ConvertFrom-Json
        if ($settings.PollSeconds -ne 73 -or $settings.Enabled -ne $false -or $settings.Language -ne 'ko' -or $settings.CheckUpdates -ne $false) { throw 'Settings were changed' }
        if ([IO.File]::ReadAllText((Join-Path $root 'user-file.txt')) -ne 'preserved') { throw 'Unmanaged file changed' }
        if (!$hostProcess.HasExited) { throw 'Old Chrome native host was not stopped' }
        if (!$expected -and [IO.File]::ReadAllText((Join-Path $root 'extension\manifest.json')) -ne 'old manifest') { throw 'Rollback lost the previous extension' }
        Write-Output "$scenario passed: process handoff, restart, settings, and native-host handling."
    } finally {
        if ($locked) { $locked.Dispose() }
        foreach ($process in @($helper,$parent,$hostProcess)) { if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }; if ($process) { $process.Dispose() } }
    }
}
Write-Output "Test artifacts: $testRoot"
