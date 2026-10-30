$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location -LiteralPath $PSScriptRoot
try {
    $testDirectory = Join-Path $PSScriptRoot 'tests'
    New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null
    & $compiler /nologo /target:exe /optimize+ /out:tests\PhotoRenamer.Tests.exe /reference:System.Core.dll /reference:System.Web.Extensions.dll PhotoRenamer.Core.cs FolderRenamer.cs PhotoRenamer.Tests.cs FolderRenamer.Tests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & '.\tests\PhotoRenamer.Tests.exe' $testDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
