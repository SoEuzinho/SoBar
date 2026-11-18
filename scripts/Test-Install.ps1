#requires -Version 7.0
param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
$msi = (Resolve-Path -LiteralPath $Package).Path
$standard = Join-Path $env:LOCALAPPDATA 'Programs/SoBar'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'SoBar.lnk'
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'SoBar AutoStart.lnk'
$installer = New-Object -ComObject WindowsInstaller.Installer
$upgradeCode = '{28F56D0E-874B-48B7-AFBA-F148F7B583AE}'
if ($installer.RelatedProducts($upgradeCode).Count -gt 0 -or (Test-Path $standard) -or (Test-Path $menu) -or (Test-Path $startup)) {
    throw 'O teste requer um perfil sem instalação, atalho ou inicialização anterior do SoBar.'
}
$logDir = Join-Path $PSScriptRoot ('../artifacts/install-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $logDir | Out-Null
$logDir = (Resolve-Path $logDir).Path
function Msi([string]$mode,[string]$name) {
    $log = Join-Path $logDir "$name.log"
    $job = Start-Process -FilePath (Join-Path ([Environment]::SystemDirectory) 'msiexec.exe') -ArgumentList @($mode,('"{0}"' -f $msi),'/qn','/norestart','/L*v',('"{0}"' -f $log)) -PassThru -Wait -WindowStyle Hidden
    if ($job.ExitCode -ne 0) { throw "Windows Installer retornou $($job.ExitCode). Consulte $log" }
}
Msi '/i' 'install'
try {
    $exe = Join-Path $standard 'SoBar.exe'
    if (!(Test-Path $exe) -or !(Test-Path $menu)) { throw 'Executável ou atalho ausente após instalação.' }
    $smoke = Start-Process -FilePath $exe -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
    if (!$smoke.WaitForExit(40000)) { $smoke.Kill(); throw 'Tempo de execução excedido.' }
    if ($smoke.ExitCode -ne 0) { throw "Smoke test retornou $($smoke.ExitCode)." }
} finally { Msi '/x' 'uninstall' }
if ((Test-Path (Join-Path $standard 'SoBar.exe')) -or (Test-Path $menu)) { throw 'Arquivos instalados permaneceram após a remoção.' }
Write-Output 'Instalação por usuário, inicialização WPF e desinstalação passaram.'

