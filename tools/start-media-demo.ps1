$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$distribution = Join-Path $repository 'dist'
$application = Join-Path $distribution 'Photo Renamer.exe'
if (!(Test-Path -LiteralPath $application)) { & (Join-Path $repository 'build.ps1') }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$demo = Join-Path $distribution 'PhotoRenamer.MediaDemo.exe'
& $compiler /nologo /target:winexe ('/out:' + $demo) ('/win32icon:' + (Join-Path $repository 'app.ico')) ('/reference:' + $application) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'MediaDemo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Media demo build failed.' }
Copy-Item -LiteralPath (Join-Path $repository 'Photo Renamer.exe.config') -Destination ($demo + '.config') -Force
Write-Output 'Built dist/PhotoRenamer.MediaDemo.exe. Launch it for an isolated screenshot session.'
