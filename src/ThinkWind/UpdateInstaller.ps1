param([Parameter(Mandatory=$true)][string]$Installer,
      [Parameter(Mandatory=$true)][string]$ExpectedHash,
      [Parameter(Mandatory=$true)][string]$Launcher,
      [int]$WaitPid = 0,
      [string]$ReadyFile = '')
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
    if ($current.Status -ne 'Valid' -or $null -eq $current.SignerCertificate) { throw 'Esta versao nao possui assinatura digital confiavel. Atualizacao integrada bloqueada ate a assinatura oficial do aplicativo.' }
    if ($candidate.Status -ne 'Valid' -or $null -eq $candidate.SignerCertificate -or $candidate.SignerCertificate.Thumbprint -ne $current.SignerCertificate.Thumbprint) { throw 'Assinatura invalida ou publicador diferente. Instalacao bloqueada.' }
    if ($WaitPid -eq 0) { Write-Output 'Assinatura e integridade verificadas.'; exit 0 }
    if ([string]::IsNullOrWhiteSpace($ReadyFile)) { throw 'Confirmacao de prontidao ausente. Instalacao cancelada.' }
    # Publish readiness only after all prerequisites have passed. The UI stays
    # alive until this point; no installer runs while its parent still exists.
    $parent = Get-Process -Id $WaitPid -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText($ReadyFile + '.tmp', 'ready')
    [IO.File]::Move($ReadyFile + '.tmp', $ReadyFile)
    if ($null -ne $parent -and !$parent.WaitForExit(60000)) { throw 'O launcher nao encerrou. Instalacao cancelada.' }
    $log = Join-Path (Split-Path -Parent $Installer) 'setup.log'
    $setup = Start-Process -FilePath $Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/ORBYUPDATE=1',('/LOG="' + $log + '"')) -WindowStyle Hidden -Wait -PassThru
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
