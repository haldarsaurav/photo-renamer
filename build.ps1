param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location -LiteralPath $PSScriptRoot
try {
    if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler is required.' }
    if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot $OutputDirectory }
    $outputPath = [IO.Path]::GetFullPath($OutputDirectory)
    $buildPath = Join-Path $PSScriptRoot '.build'
    New-Item -ItemType Directory -Force -Path $outputPath,$buildPath | Out-Null
    $iconCompiler = Join-Path $buildPath 'CreateIcon.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $iconCompiler) /reference:System.Drawing.dll CreateIcon.cs
    if ($LASTEXITCODE -ne 0) { throw 'Icon build failed.' }
    & $iconCompiler $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
    & $compiler /nologo /target:winexe /optimize+ /utf8output ('/out:' + (Join-Path $outputPath 'Photo Renamer.exe')) /win32manifest:app.manifest ('/win32icon:' + (Join-Path $outputPath 'app.ico')) ('/resource:' + (Join-Path $outputPath 'app.png') + ',PhotoRenamer.Icon.png') /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll PhotoRenamer.Core.cs FolderRenamer.cs FolderPicker.cs DarkTheme.cs PhotoRenamer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Photo Renamer.exe.config') -Destination $outputPath -Force
    Write-Output 'Built dist/Photo Renamer.exe (or the supplied output directory).'
} finally { Pop-Location }
