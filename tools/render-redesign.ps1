param([switch]$Setup)

# Documentation tooling only. No Node, Python or ffmpeg dependency in the app.
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$vendor = Join-Path $repository '.build/vendor/unslop'
$revision = '4652254daff586ff4739849395a127b56f7f8cc7'
$renderRoot = [IO.Path]::GetFullPath((Join-Path $repository '.build/unslop-story'))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $repository '.build')) + [IO.Path]::DirectorySeparatorChar
if (!$renderRoot.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Story frames must stay inside .build.' }
if (!(Test-Path -LiteralPath (Join-Path $vendor '.git'))) {
    if (!$Setup) { throw 'Run tools/render-redesign.ps1 -Setup to fetch the pinned Unslop skill and rendering dependencies.' }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $vendor) | Out-Null
    & git clone --no-checkout --depth 1 https://github.com/heydamir/unslop.git $vendor
    if ($LASTEXITCODE -ne 0) { throw 'Unslop clone failed.' }
    & git -C $vendor fetch --depth 1 origin $revision
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch the pinned Unslop revision.' }
    & git -C $vendor checkout --detach $revision
    if ($LASTEXITCODE -ne 0) { throw 'Cannot check out the pinned Unslop revision.' }
}
$actualRevision = (& git -C $vendor rev-parse HEAD).Trim()
if ($actualRevision -ne $revision) { throw 'The local Unslop checkout is not the documented revision.' }
if ($Setup) {
    & npm.cmd install --prefix $vendor --no-audit --no-fund --ignore-scripts playwright-core@1.64.1 ffmpeg-static@5.3.0
    if ($LASTEXITCODE -ne 0) { throw 'Documentation dependency installation failed.' }
    & node (Join-Path $vendor 'node_modules/ffmpeg-static/install.js')
    if ($LASTEXITCODE -ne 0) { throw 'ffmpeg download failed.' }
}
$ffmpeg = Join-Path $vendor 'node_modules/ffmpeg-static/ffmpeg.exe'
if (!(Test-Path -LiteralPath $ffmpeg)) { throw 'Run tools/render-redesign.ps1 -Setup to fetch ffmpeg.' }
New-Item -ItemType Directory -Force -Path $renderRoot | Out-Null
$story = Get-Content -LiteralPath (Join-Path $repository 'docs/redesign/story.html') -Raw
foreach ($name in @('IBMPlexSans-Regular','IBMPlexSans-Medium','IBMPlexMono-Regular')) {
    $data = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $repository ('assets/fonts/' + $name + '.ttf'))))
    $story = $story.Replace('../../assets/fonts/' + $name + '.ttf', 'data:font/ttf;base64,' + $data)
}
foreach ($asset in @(@{ Url = 'icon-before.png'; Path = 'docs/redesign/icon-before.png' }, @{ Url = '../../app.png'; Path = 'app.png' })) {
    $data = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $repository $asset.Path)))
    $story = $story.Replace($asset.Url, 'data:image/png;base64,' + $data)
}
$inputFile = Join-Path $renderRoot 'story.html'
[IO.File]::WriteAllText($inputFile, $story, (New-Object Text.UTF8Encoding $false))
$output = Join-Path $repository 'docs/images/redesign.mp4'
$previousPath = $env:PATH
$previousChrome = $env:CHROME_PATH
try {
    $env:PATH = (Split-Path -Parent $ffmpeg) + [IO.Path]::PathSeparator + $previousPath
    if (!$env:CHROME_PATH) {
        $browsers = @((Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'), (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'))
        $env:CHROME_PATH = $browsers | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (!$env:CHROME_PATH) { throw 'Chrome or Edge is required; alternatively set CHROME_PATH to a Chromium executable.' }
    }
    & node (Join-Path $vendor 'skills/unslop/scripts/story.mjs') $inputFile $output
    if ($LASTEXITCODE -ne 0) { throw 'Unslop story rendering failed.' }
    & $ffmpeg -y -loglevel error -i $output -vf 'fps=12,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=160[p];[b][p]paletteuse=dither=none' -loop 0 (Join-Path $repository 'docs/images/redesign.gif')
    if ($LASTEXITCODE -ne 0) { throw 'Story GIF conversion failed.' }
} finally { $env:PATH = $previousPath; $env:CHROME_PATH = $previousChrome }
Write-Output 'Rendered docs/images/redesign.mp4 and redesign.gif with the pinned Unslop story tool.'
