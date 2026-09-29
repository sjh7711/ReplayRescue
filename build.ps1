$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler is required.' }
$files = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32manifest:$root\app.manifest" "/out:$root\ReplayRescue.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll $files
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $root\ReplayRescue.exe"
