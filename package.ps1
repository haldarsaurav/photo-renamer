param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [switch]$SkipBuild,
    [string]$DocumentationRef = 'main'
)

$ErrorActionPreference = 'Stop'
if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot $OutputDirectory }
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $outputPath }
$files = @('Photo Renamer.exe','Photo Renamer.exe.config','README.md','LICENSE','AUDIT.md','THIRD_PARTY_NOTICES.md','Fonts-OFL.txt','app.png')
foreach ($name in @('README.md','LICENSE','AUDIT.md','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $outputPath -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/fonts/OFL.txt') -Destination (Join-Path $outputPath 'Fonts-OFL.txt') -Force
# Keep the portable ZIP small while making its README's media and guide links
# usable outside the source checkout. The application itself stays offline.
$readme = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Raw
$githubRoot = 'https://github.com/haldarsaurav/photo-renamer/blob/' + [Uri]::EscapeDataString($DocumentationRef) + '/'
$rawRoot = 'https://raw.githubusercontent.com/haldarsaurav/photo-renamer/' + [Uri]::EscapeDataString($DocumentationRef) + '/'
$readme = [regex]::Replace($readme, '\]\((?<target>(?!https?://|#)[^\s)]+)\)', [Text.RegularExpressions.MatchEvaluator]{ param($match)
    $target = $match.Groups['target'].Value
    $root = if ($target -match '\.(png|ico|gif)$') { $rawRoot } else { $githubRoot }
    '](' + $root + $target + ')'
})
$readme = $readme.Replace('src="app.png"', ('src="' + $rawRoot + 'app.png"'))
[IO.File]::WriteAllText((Join-Path $outputPath 'README.md'), $readme, (New-Object Text.UTF8Encoding $false))
$sources = @($files | ForEach-Object { Join-Path $outputPath $_ })
foreach ($file in $sources) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Missing release input: ' + [IO.Path]::GetFileName($file)) } }
$archive = Join-Path $outputPath 'Photo-Renamer.zip'
Compress-Archive -LiteralPath $sources -DestinationPath $archive -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputPath 'SHA256SUMS.txt'), $checksum + '  Photo-Renamer.zip' + [Environment]::NewLine, (New-Object Text.UTF8Encoding $false))
Write-Output 'Created dist/Photo-Renamer.zip and SHA256SUMS.txt (or the supplied output directory).'
