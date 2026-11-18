#requires -Version 7.0
param([Parameter(Mandatory)][string]$Payload, [Parameter(Mandatory)][string]$Assets)
$ErrorActionPreference = 'Stop'
$folders = (Get-Content -LiteralPath $Assets -Raw | ConvertFrom-Json -AsHashtable).packageFolders.Keys
$deps = Get-Content -LiteralPath (Join-Path $Payload 'SoBar.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$output = [Text.StringBuilder]::new()
foreach ($name in $deps.libraries.Keys) {
    if ($deps.libraries[$name].type -eq 'project') { continue }
    $relative = ($name -replace '^runtimepack\.','').ToLowerInvariant()
    $package = $folders | ForEach-Object {Join-Path $_ $relative} | Where-Object {Test-Path $_} | Select-Object -First 1
    if (!$package) { throw "Metadados ausentes para $name" }
    $manifest = Get-ChildItem -LiteralPath $package -Filter *.nuspec | Select-Object -First 1
    [xml]$xml = Get-Content -Raw -LiteralPath $manifest.FullName
    $meta = $xml.package.metadata
    [void]$output.AppendLine("=== $name ===")
    foreach($field in @('authors','copyright','license','licenseUrl','projectUrl')) {
        $node = $meta.SelectSingleNode("*[local-name()='$field']")
        if ($node) { [void]$output.AppendLine("$field = $($node.InnerText)") }
    }
    if ($meta.repository.url) {
        [void]$output.AppendLine("Source = $($meta.repository.url) @ $($meta.repository.commit)")
    }
    foreach ($file in (Get-ChildItem -LiteralPath $package -File | Where-Object Name -match '^(license|copying|third.party.notices|notice)(\.|$)')) {
        [void]$output.AppendLine([IO.File]::ReadAllText($file.FullName))
    }
}
foreach($file in (Get-ChildItem (Join-Path $PSScriptRoot '../installer/licenses') -Filter '*.txt')) {
    [void]$output.AppendLine("=== $($file.BaseName) ===")
    [void]$output.AppendLine([IO.File]::ReadAllText($file.FullName))
}
[IO.File]::WriteAllText((Join-Path $Payload 'THIRD-PARTY-NOTICES.txt'),$output.ToString())
