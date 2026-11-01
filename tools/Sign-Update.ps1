param([Parameter(Mandatory=$true)][string]$Installer,
      [Parameter(Mandatory=$true)][string]$Tag)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
if ($Tag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$') { throw 'Tag invalida.' }
$resolved = (Resolve-Path -LiteralPath $Installer).Path
if ([IO.Path]::GetFileName($resolved) -cne ('OrbyLauncher-Setup-' + $Tag.Substring(1) + '.exe')) { throw 'Nome do instalador nao corresponde a tag.' }
$keyPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OrbyPublisher\update-signing-key.dpapi'
if (!(Test-Path -LiteralPath $keyPath)) { throw 'Chave privada protegida ausente. Nao gere outra chave: recupere a chave oficial de publicacao.' }
$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp = $false
$secret = $null
$installerLock = $null
try {
    $secret = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($keyPath), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($secret))
    $public = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\src\ThinkWind\Assets\UpdatePublicKey.xml'))
    if ($rsa.ToXmlString($false) -cne $public) { throw 'Chave privada nao corresponde a chave publica do launcher.' }
    $installerLock = [IO.File]::Open($resolved, 'Open', 'Read', 'Read')
    $hash = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
    $payload = [Text.Encoding]::ASCII.GetBytes('OrbyLauncher.Update.SHA256.v1:' + $Tag + ':' + $hash)
    [IO.File]::WriteAllBytes($resolved + '.sig', $rsa.SignData($payload, 'SHA256'))
    Write-Output ('Assinatura criada: ' + $resolved + '.sig')
} finally {
    if ($null -ne $installerLock) { $installerLock.Dispose() }
    if ($null -ne $secret) { [Array]::Clear($secret, 0, $secret.Length) }
    $rsa.Dispose()
}
