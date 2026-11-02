param([Parameter(Mandatory=$true)][string]$Installer,
      [Parameter(Mandatory=$true)][string]$ExpectedHash,
      [Parameter(Mandatory=$true)][string]$Launcher,
      [int]$WaitPid = 0,
      [string]$ReadyFile = '',
      [string]$PublisherKey = '',
      [string]$ReleaseTag = '')
$ErrorActionPreference = 'Stop'
$record = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OrbyLauncher\Updates\installation-result.json'
$lock = $null
try {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $record)) | Out-Null
    # FileShare.Read prevents modifications between validation and execution.
    $lock = [IO.File]::Open($Installer, 'Open', 'Read', 'Read')
    if ((Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'SHA-256 divergente. Instalacao bloqueada.' }
    $current = Get-AuthenticodeSignature -LiteralPath $Launcher
    $candidate = Get-AuthenticodeSignature -LiteralPath $Installer
    if ($current.Status -eq 'Valid' -and $null -ne $current.SignerCertificate) {
        if ($candidate.Status -ne 'Valid' -or $null -eq $candidate.SignerCertificate -or $candidate.SignerCertificate.Thumbprint -ne $current.SignerCertificate.Thumbprint) { throw 'Assinatura invalida ou publicador diferente. Instalacao bloqueada.' }
    } elseif ($current.Status -eq 'NotSigned' -and $candidate.Status -eq 'NotSigned') {
        # Beta artifacts use a detached publisher signature. SHA-256 alone is
        # insufficient. Never accept an invalid/revoked Authenticode signature.
        if ([string]::IsNullOrWhiteSpace($PublisherKey) -or $ReleaseTag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$') { throw 'Identidade da publicacao ausente.' }
        $signatureFile = $Installer + '.sig'
        if (!(Test-Path -LiteralPath $signatureFile) -or (Get-Item -LiteralPath $signatureFile).Length -ne 384) { throw 'Assinatura oficial do pacote ausente. Instale manualmente a versao oficial.' }
        $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
        $rsa.PersistKeyInCsp = $false
        try {
            $rsa.FromXmlString($PublisherKey)
            if ($rsa.KeySize -ne 3072) { throw 'Chave de atualizacao invalida.' }
            $payload = [Text.Encoding]::ASCII.GetBytes('OrbyLauncher.Update.SHA256.v1:' + $ReleaseTag + ':' + $ExpectedHash.ToLowerInvariant())
            if (!$rsa.VerifyData($payload, 'SHA256', [IO.File]::ReadAllBytes($signatureFile))) { throw 'Assinatura do publicador Orby invalida. Instalacao bloqueada.' }
        } finally { $rsa.Dispose() }
    } else { throw 'Assinatura Authenticode invalida ou estado nao permitido. Instalacao bloqueada.' }
    if ($WaitPid -eq 0) { Write-Output 'Assinatura e integridade verificadas.'; exit 0 }
    if ([string]::IsNullOrWhiteSpace($ReadyFile)) { throw 'Confirmacao de prontidao ausente. Instalacao cancelada.' }
    # Publish readiness only after all prerequisites have passed. The UI stays
    # alive until this point; no installer runs while its parent still exists.
    $parent = Get-Process -Id $WaitPid -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText($ReadyFile + '.tmp', 'ready')
    [IO.File]::Move($ReadyFile + '.tmp', $ReadyFile)
    if ($null -ne $parent -and !$parent.WaitForExit(60000)) { throw 'O launcher nao encerrou. Instalacao cancelada.' }
    $log = Join-Path (Split-Path -Parent $Installer) 'setup.log'
    # SILENT skips wizard questions but keeps Inno's actual installation
    # progress/status visible after the launcher exits. VERYSILENT + Hidden
    # left the desktop with no indication that installation was still running.
    $setup = Start-Process -FilePath $Installer -ArgumentList @('/SILENT','/SUPPRESSMSGBOXES','/NORESTART','/ORBYUPDATE=1',('/LOG="' + $log + '"')) -WindowStyle Normal -Wait -PassThru
    @{ ExitCode=$setup.ExitCode; Utc=[DateTime]::UtcNow; Log=$log } | ConvertTo-Json | Set-Content -LiteralPath $record -Encoding UTF8
    if ($setup.ExitCode -notin @(0,3010)) { throw ('Instalador retornou erro ' + $setup.ExitCode + '. Consulte ' + $log) }
    Start-Process -FilePath $Launcher -WindowStyle Hidden
    exit 0
} catch {
    $message = $_.Exception.Message
    Write-Output $message
    if ($WaitPid -ne 0) {
        try { @{ ExitCode=-1; Error=$message; Utc=[DateTime]::UtcNow } | ConvertTo-Json | Set-Content -LiteralPath $record -Encoding UTF8 } catch { }
        if ($null -eq (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $Launcher)) {
            try { Start-Process -FilePath $Launcher -WindowStyle Hidden } catch { Write-Output $_.Exception.Message }
        }
    }
    exit 1
} finally { if ($null -ne $lock) { $lock.Dispose() } }
