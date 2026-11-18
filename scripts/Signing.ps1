param([string]$Package, [string]$Version, [switch]$Initialize, [string]$KeyName = 'SoBar.LocalRelease.v1')
$ErrorActionPreference = 'Stop'
$provider = [Security.Cryptography.CngProvider]::MicrosoftSoftwareKeyStorageProvider
$publicPath = Join-Path $PSScriptRoot '../src/SoBar.Infrastructure/publisher.xml'
if (![Security.Cryptography.CngKey]::Exists($KeyName,$provider)) {
    if (!$Initialize) { throw 'A chave de publicação não está disponível neste usuário do Windows.' }
    if (Test-Path $publicPath) { throw 'O repositório já possui uma identidade pública. Use a chave correspondente para assinar.' }
    $options = [Security.Cryptography.CngKeyCreationParameters]::new()
    $options.Provider = $provider
    $options.KeyUsage = [Security.Cryptography.CngKeyUsages]::Signing
    $options.Parameters.Add([Security.Cryptography.CngProperty]::new('Length',[BitConverter]::GetBytes([int]3072),0))
    $key = [Security.Cryptography.CngKey]::Create([Security.Cryptography.CngAlgorithm]::Rsa,$KeyName,$options)
} else { $key = [Security.Cryptography.CngKey]::Open($KeyName,$provider) }
$rsa = [Security.Cryptography.RSACng]::new($key)
try {
    $p = $rsa.ExportParameters($false)
    $public = '<RSAKeyValue><Modulus>' + [Convert]::ToBase64String($p.Modulus) + '</Modulus><Exponent>' + [Convert]::ToBase64String($p.Exponent) + '</Exponent></RSAKeyValue>'
    if ($Initialize -and !(Test-Path $publicPath)) { [IO.File]::WriteAllText($publicPath,$public) }
    if ([IO.File]::ReadAllText($publicPath).Trim() -ne $public) { throw 'Identidade de publicação divergente.' }
    if ($Package) {
        $hash = (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash
        $message = [Text.Encoding]::UTF8.GetBytes(("SoBar-MSI-v1","$Version",$hash,"" -join [char]10))
        $signature = $rsa.SignData($message,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
        $envelope = [ordered]@{Version=$Version;Sha256=$hash;Signature=[Convert]::ToBase64String($signature)}
        [IO.File]::WriteAllText("$Package.sig",($envelope | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    }
} finally { $rsa.Dispose(); $key.Dispose() }
