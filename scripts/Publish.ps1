#requires -Version 7.0
param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.0', [switch]$Sign)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stage = Join-Path $root ('artifacts/build-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'portable'
$out = Join-Path $root 'artifacts/release'
New-Item -ItemType Directory -Force $payload,$out | Out-Null
function RunDotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet falhou ($LASTEXITCODE)" }
}
function Id([string]$rel) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($rel.ToLowerInvariant())
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).Substring(0,32)
}
function Esc([string]$content) { return [Security.SecurityElement]::Escape($content) }
Push-Location $root
try {
    RunDotnet @('publish','src/SoBar.App','-c','Release','-r','win-x64','--self-contained','true','-p:DebugType=None','-p:DebugSymbols=false',"-p:Version=$Version",'-o',$payload)
    Copy-Item 'docs/THIRD_PARTY.md' $payload
    & "$PSScriptRoot/Collect-Notices.ps1" -Payload $payload -Assets 'src/SoBar.App/obj/project.assets.json'
    RunDotnet @('tool','restore')
    $dirs = [Collections.Generic.List[string]]::new()
    $components = [Collections.Generic.List[string]]::new()
    $refs = [Collections.Generic.List[string]]::new()
    function AddDirectory([string]$path,[string]$directoryId) {
        foreach ($entry in (Get-ChildItem -LiteralPath $path | Sort-Object Name)) {
            $part = [IO.Path]::GetRelativePath($payload,$entry.FullName)
            $hash = Id $part
            if ($entry.PSIsContainer) {
                $dirs.Add(('<DirectoryRef Id="{0}"><Directory Id="D{1}" Name="{2}"/></DirectoryRef>' -f $directoryId,$hash,(Esc $entry.Name)))
                AddDirectory $entry.FullName ('D'+$hash)
            } else {
                $guid = ([Guid]::ParseExact($hash,'N')).ToString()
                $components.Add(('<Component Id="C{0}" Directory="{1}" Guid="{2}"><File Id="F{0}" Source="{3}"/><RegistryValue Root="HKCU" Key="Software\SoBar\Installer\Files" Name="{0}" Value="1" Type="integer" KeyPath="yes"/></Component>' -f $hash,$directoryId,$guid,(Esc $entry.FullName)))
                $refs.Add(('<ComponentRef Id="C{0}"/>' -f $hash))
            }
        }
    }
    AddDirectory $payload 'INSTALLFOLDER'
    $xml = '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment>' + ($dirs -join '') + ($components -join '') + '<ComponentGroup Id="Payload">' + ($refs -join '') + '</ComponentGroup></Fragment></Wix>'
    $fragment = Join-Path $stage 'Payload.wxs'
    [IO.File]::WriteAllText($fragment,$xml)
    $msi = Join-Path $out "SoBar-$Version-win-x64.msi"
    $env:DOTNET_ROLL_FORWARD = 'Major'
    RunDotnet @('tool','run','wix','--','build','installer/Product.wxs',$fragment,'-arch','x64','-d',"Version=$Version",'-intermediateFolder',$stage,'-pdbtype','none','-o',$msi)
    if ($Sign) { & "$PSScriptRoot/Signing.ps1" -Package $msi -Version $Version }
    $zip = Join-Path $out "SoBar-$Version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item -LiteralPath $zip }
    [IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
    Write-Output $out
} finally { Pop-Location }
