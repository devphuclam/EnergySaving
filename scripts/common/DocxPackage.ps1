Set-StrictMode -Version Latest

function Read-DocxEntryText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.IO.Compression.ZipArchive]$Zip,
        [Parameter(Mandatory)][string]$Name
    )

    $entry = $Zip.GetEntry($Name)
    if ($null -eq $entry) {
        return $null
    }

    $stream = $entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Resolve-DocxRelationshipTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$SourcePart,
        [Parameter(Mandatory)][string]$Target
    )

    if ([string]::IsNullOrWhiteSpace($Target) -or $Target.StartsWith('/', [StringComparison]::Ordinal)) {
        return $null
    }

    $base = @($SourcePart.Replace('\', '/') -split '/')
    if ($base.Count -gt 1) { $base = $base[0..($base.Count - 2)] } else { $base = @() }
    $segments = @($base + @($Target.Replace('\', '/') -split '/'))
    $resolved = [System.Collections.Generic.List[string]]::new()
    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.') { continue }
        if ($segment -eq '..') {
            if ($resolved.Count -eq 0) { return $null }
            $resolved.RemoveAt($resolved.Count - 1)
            continue
        }
        $resolved.Add($segment)
    }
    return ($resolved -join '/')
}

function Get-DocxRelationshipAttributeValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Xml.Linq.XElement]$Relationship,
        [Parameter(Mandatory)][string]$Name
    )

    $attribute = @($Relationship.Attributes() | Where-Object { $_.Name.LocalName -eq $Name } | Select-Object -First 1)
    if ($attribute.Count -ne 1) { return '' }
    $raw = [string]$attribute[0]
    if ($raw -match '^.+="(.*)"$') { return $matches[1] }
    return $raw
}

function Test-DocxPackageIntegrity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.IO.Compression.ZipArchive]$Zip)

    $failures = [System.Collections.Generic.List[string]]::new()
    $checks = 0
    $requiredEntries = @('[Content_Types].xml', '_rels/.rels', 'word/document.xml', 'word/_rels/document.xml.rels')
    foreach ($name in $requiredEntries) {
        $checks++
        if ($null -eq $Zip.GetEntry($name)) { $failures.Add("missing required package entry: $name") }
    }

    $criticalEntries = @('[Content_Types].xml', '_rels/.rels', 'word/document.xml', 'word/_rels/document.xml.rels')
    foreach ($name in $criticalEntries) {
        $checks++
        $count = @($Zip.Entries | Where-Object { $_.FullName -eq $name }).Count
        if ($count -ne 1) { $failures.Add("critical package entry count must be one: $name") }
    }

    $xmlByName = @{}
    foreach ($name in $requiredEntries) {
        $text = Read-DocxEntryText -Zip $Zip -Name $name
        if ($null -eq $text) { continue }
        $checks++
        try { $xmlByName[$name] = [System.Xml.Linq.XDocument]::Parse($text) }
        catch { $failures.Add("package XML is malformed: $name") }
    }

    $relationshipNamespace = [System.Xml.Linq.XNamespace]::Get('http://schemas.openxmlformats.org/package/2006/relationships')
    $rootRels = $xmlByName['_rels/.rels']
    if ($null -ne $rootRels) {
        $checks++
        $officeRelationship = @($rootRels.Descendants($relationshipNamespace + 'Relationship') |
            Where-Object { (Get-DocxRelationshipAttributeValue -Relationship $_ -Name 'Type') -match '/officeDocument$' } | Select-Object -First 1)
        if ($officeRelationship.Count -ne 1) {
            $failures.Add('root relationships must contain one officeDocument relationship')
        }
        else {
            $target = Resolve-DocxRelationshipTarget -SourcePart '' -Target (Get-DocxRelationshipAttributeValue -Relationship $officeRelationship[0] -Name 'Target')
            if ($target -ne 'word/document.xml' -or $null -eq $Zip.GetEntry($target)) {
                $failures.Add('officeDocument relationship target must resolve to word/document.xml')
            }
        }
    }

    $checks++
    $documentRelationships = $xmlByName['word/_rels/document.xml.rels']
    if ($null -eq $documentRelationships) {
        $failures.Add('document relationship XML is unavailable')
    }
    else {
        foreach ($relationship in $documentRelationships.Descendants($relationshipNamespace + 'Relationship')) {
            $targetValue = Get-DocxRelationshipAttributeValue -Relationship $relationship -Name 'Target'
            $targetMode = Get-DocxRelationshipAttributeValue -Relationship $relationship -Name 'TargetMode'
            $checks++
            $target = Resolve-DocxRelationshipTarget -SourcePart 'word/document.xml' -Target $targetValue
            if ($null -eq $target) {
                $failures.Add("relationship target contains traversal or absolute path: $targetValue")
            }
            elseif ($targetMode -ne 'External' -and $null -eq $Zip.GetEntry($target)) {
                $failures.Add("relationship target entry is missing: $target")
            }
        }
    }

    [pscustomobject]@{ Checks = $checks; Failures = $failures }
}
