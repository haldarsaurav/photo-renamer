# Check the exact Git index that will be committed, never local runtime data.
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    $files = @(& git ls-files --cached)
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'Stage the intended source files in Git before running this check.' }
    $forbiddenPaths = '(?i)(^|/)(\.env(?:\..*)?|.*\.(exe|dll|zip|pdb|dat|pem|key|pfx))$|^(dist|\.build)/|^tests/(?!UiCheck\.cs$)'
    $patterns = @(
        @{ Name = 'absolute drive path'; Pattern = '(?i)\b[A-Z]:[\\/]' },
        @{ Name = 'personal home path'; Pattern = '(?i)/(Users|home)/[^\s/]+|\\Users\\[^\s\\]+' },
        @{ Name = 'GitHub token'; Pattern = '(gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})' },
        @{ Name = 'private key'; Pattern = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----' },
        @{ Name = 'cloud access key'; Pattern = '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b' }
    )
    $issues = New-Object Collections.Generic.List[string]
    foreach ($file in $files) {
        if ($file -match $forbiddenPaths) { $issues.Add($file + ': generated or sensitive file type'); continue }
        if ($file -match '\.(png|ico)$') { continue }
        $contents = (& git show (':' + $file)) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw ('Cannot inspect staged file: ' + $file) }
        foreach ($rule in $patterns) { if ($contents -match $rule.Pattern) { $issues.Add($file + ': ' + $rule.Name) } }
    }
    if ($issues.Count -gt 0) { throw ('Public-source check failed:' + [Environment]::NewLine + ($issues -join [Environment]::NewLine)) }
    Write-Output ('Public-source check passed for ' + $files.Count + ' staged files. No absolute personal paths, token patterns or generated runtime files found.')
} finally { Pop-Location }
