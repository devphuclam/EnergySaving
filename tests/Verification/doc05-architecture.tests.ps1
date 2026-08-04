[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$docxPath = Join-Path $repoRoot 'Business Docs\DOC-05_Software_Architecture_Document_v0.2.docx'
$packageVerifierPath = Join-Path $repoRoot 'scripts\common\DocxPackage.ps1'

if (-not (Test-Path -LiteralPath $packageVerifierPath -PathType Leaf)) {
    throw 'RED: DOCX package-integrity verifier is not implemented'
}
. $packageVerifierPath

if (-not (Test-Path -LiteralPath $docxPath -PathType Leaf)) {
    throw "RED: canonical DOC-05 v0.2 document is missing at $docxPath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Xml.Linq

$failures = [System.Collections.Generic.List[string]]::new()
$checks = 0

function Assert-ContainsText {
    param([string]$Text, [string]$Expected, [string]$Label, [switch]$IgnoreCase)
    $script:checks++
    $comparison = if ($IgnoreCase) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if ($Text.IndexOf($Expected, $comparison) -lt 0) {
        $script:failures.Add("$Label`: DOCX text does not contain '$Expected'")
    }
}

function Assert-NotContainsText {
    param([string]$Text, [string]$Unexpected, [string]$Label)
    $script:checks++
    if ($Text.IndexOf($Unexpected, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $script:failures.Add("$Label`: DOCX text must not contain '$Unexpected'")
    }
}

function Get-InternalServicePhrase {
    $internalService = -join @(
        [char]0x64, [char]0x1ECB, [char]0x63, [char]0x68, [char]0x20,
        [char]0x76, [char]0x1EE5, [char]0x20,
        [char]0x6E, [char]0x1ED9, [char]0x69, [char]0x20,
        [char]0x62, [char]0x1ED9
    )
    return $internalService
}

$tempFile = Join-Path ([IO.Path]::GetTempPath()) ('iump-doc05-' + [Guid]::NewGuid().ToString('N') + '.docx')
$tempCopyCreated = $false
$workPath = $docxPath

try {
    try {
        $probe = [IO.File]::OpenRead($docxPath)
        $probe.Dispose()
    }
    catch {
        Copy-Item -LiteralPath $docxPath -Destination $tempFile -Force
        $tempCopyCreated = $true
        $workPath = $tempFile
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($workPath)
    try {
        $packageResult = Test-DocxPackageIntegrity -Zip $zip
        $script:checks += $packageResult.Checks
        if ($packageResult.Failures.Count -gt 0) {
            $packageResult.Failures | ForEach-Object { $script:failures.Add($_) }
        }
        $entry = $zip.GetEntry('word/document.xml')
        if ($null -eq $entry) {
            throw 'DOCX does not contain word/document.xml'
        }
        $reader = New-Object System.IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
        try {
            $xmlText = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
        $script:checks++
    }
    finally {
        $zip.Dispose()
    }

    $document = [System.Xml.Linq.XDocument]::Parse($xmlText)
    $textNodes = @($document.DescendantNodes() | Where-Object { $_ -is [System.Xml.Linq.XText] } |
        ForEach-Object { $_.Value })
    $docText = ($textNodes -join ' ')
    $script:checks++

    Assert-ContainsText $docText 'non-containerized' 'current deployment wording' -IgnoreCase
    Assert-NotContainsText $docText 'containerized reference deployment' 'stale containerized wording absent'
    Assert-NotContainsText $docText 'On-premise containerized' 'stale on-premise containerized absent'
    Assert-ContainsText $docText 'corrected 03/08/2026' 'version history correction date' -IgnoreCase
    Assert-ContainsText $docText 'static files' 'static files component' -IgnoreCase
    Assert-ContainsText $docText 'Windows Service' 'Windows Service component' -IgnoreCase
    Assert-ContainsText $docText (Get-InternalServicePhrase) 'internal PostgreSQL service component'
    Assert-ContainsText $docText 'AR-11' 'ADR catalogue AR-11'
}
finally {
    if ($tempCopyCreated -and (Test-Path -LiteralPath $tempFile)) {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Doc05Architecture: checks=$checks failures=$($failures.Count)"
    $failures | ForEach-Object { Write-Host "FAIL: $_" }
    exit 1
}

Write-Host "Doc05Architecture: checks=$checks failures=0"
Write-Host 'PASS: DOC-05 v0.2 restricted non-containerized architecture structure (text-level)'
Write-Host 'NOTE: text-level structural PASS is not a visual/render PASS; approved visual render verification remains unavailable.'
exit 0
