#requires -Version 7.0
param([Parameter(Mandatory)][string]$Previous, [Parameter(Mandatory)][string]$Current)
$ErrorActionPreference = 'Stop'
$Previous = (Resolve-Path $Previous).Path
$Current = (Resolve-Path $Current).Path
$installer = New-Object -ComObject WindowsInstaller.Installer
$upgradeCode = '{28F56D0E-874B-48B7-AFBA-F148F7B583AE}'
$folder = Join-Path $env:LOCALAPPDATA 'Programs/SoBar'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SoBar.lnk'
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'SoBar AutoStart.lnk'
if ($installer.RelatedProducts($upgradeCode).Count -gt 0 -or (Test-Path $folder) -or (Test-Path $menu) -or (Test-Path $startup)) {
    throw 'Use um perfil sem instalação, atalho ou inicialização existente do SoBar.'
}
function InvokeMsi($operation,$package) {
    $p = Start-Process -FilePath (Join-Path ([Environment]::SystemDirectory) 'msiexec.exe') -ArgumentList @($operation,('"{0}"' -f $package),'/qn','/norestart') -PassThru -Wait -WindowStyle Hidden
    return $p.ExitCode
}
$owned = $null
try {
    if ((InvokeMsi '/i' $Previous) -ne 0) { throw 'A instalação anterior falhou.' }
    $owned = $Previous
    $exe = Join-Path $folder 'SoBar.exe'
    $from = [version](Get-Item $exe).VersionInfo.FileVersion
    if ((InvokeMsi '/i' $Current) -ne 0) { throw 'Falha no major upgrade.' }
    $owned = $Current
    $to = [version](Get-Item $exe).VersionInfo.FileVersion
    if ($to -le $from -or $installer.RelatedProducts($upgradeCode).Count -ne 1) { throw 'Estado pós-atualização incorreto.' }
    if ((InvokeMsi '/i' $Previous) -eq 0) { throw 'O MSI aceitou um downgrade.' }
    if ([version](Get-Item $exe).VersionInfo.FileVersion -ne $to) { throw 'O downgrade alterou os binários.' }
    $p = Start-Process -FilePath $exe -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
    if (!$p.WaitForExit(40000)) { $p.Kill(); throw 'O aplicativo atualizado demorou a responder.' }
    if ($p.ExitCode -ne 0) { throw 'A interface atualizada falhou no smoke test.' }
    Write-Output "Atualização $from → $to, bloqueio de downgrade e inicialização verificados."
} finally {
    if ($owned -and (InvokeMsi '/x' $owned) -ne 0) { throw 'Falha na remoção da instalação de teste.' }
}
