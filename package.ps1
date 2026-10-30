param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot $OutputDirectory }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $outputPath }
$files = @('Photo Renamer.exe','Photo Renamer.exe.config','README.md','LICENSE','AUDIT.md','app.png')
foreach ($name in @('README.md','LICENSE','AUDIT.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $outputPath -Force
}
$sources = @($files | ForEach-Object { Join-Path $outputPath $_ })
foreach ($file in $sources) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Missing release input: ' + [IO.Path]::GetFileName($file)) } }
$archive = Join-Path $outputPath 'Photo-Renamer.zip'
Compress-Archive -LiteralPath $sources -DestinationPath $archive -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputPath 'SHA256SUMS.txt'), $checksum + '  Photo-Renamer.zip' + [Environment]::NewLine, (New-Object Text.UTF8Encoding $false))
Write-Output 'Created dist/Photo-Renamer.zip and SHA256SUMS.txt (or the supplied output directory).'
