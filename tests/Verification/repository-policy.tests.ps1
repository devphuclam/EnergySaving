[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

$prohibitedArtifacts = Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](\.git|\.agents|node_modules|bin|obj|Business Docs)[\\/]' -and (
        $_.Name -like 'Dockerfile*' -or
        $_.Name -like 'Containerfile*' -or
        $_.Name -match '^docker-compose.*\.(yml|yaml)$' -or
        $_.Name -match '^compose\.(yml|yaml)$' -or
        $_.FullName -match '[\\/]\.devcontainer[\\/]' -or
        $_.Name -match '(?i)(podman|buildah|container[-_.]?image|image[-_.]?build)' -or
        $_.FullName -match '[\\/]\.github[\\/]workflows[\\/].+\.(yml|yaml)$'
    )
}
if ($prohibitedArtifacts) {
    throw "Prohibited artifact exists: $($prohibitedArtifacts.FullName -join ', ')"
}

$textFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](\.git|node_modules|bin|obj)[\\/]' -and
    $_.Extension -in @('.cs', '.json', '.sql', '.ps1', '.yml', '.yaml', '.config')
}

$credentialPatterns = @(
    '(?i)Password\s*=\s*iump',
    '(?i)Username\s*=\s*iump;Password',
    '(?i)sk-proj-[A-Za-z0-9_-]+',
    '(?i)(ghp|github_pat)_[A-Za-z0-9_]{20,}',
    '(?i)Password\s*=(?!=)\s*(?!\$\{|<|REDACTED|CHANGE_ME)[^;\s]+'
)

foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($pattern in $credentialPatterns) {
        if ($content -match $pattern) {
            throw "Credential-like value found in $($file.FullName)"
        }
    }
}

Write-Output 'PASS: repository policy contract'
