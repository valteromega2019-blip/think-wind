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
$restartAttempted = $false
$readyEvent = $null
$progressProcess = $null
$progressState = $null
function Publish-Progress([string]$Title, [string]$Detail, [bool]$Done = $false, [bool]$Failed = $false) {
    if ($null -eq $progressState) { return }
    try {
        $json = @{ Title=$Title; Detail=$Detail; Done=$Done; Error=$Failed } | ConvertTo-Json
        [IO.File]::WriteAllText($progressState + '.tmp', $json, [Text.UTF8Encoding]::new($true))
        Move-Item -LiteralPath ($progressState + '.tmp') -Destination $progressState -Force
    } catch { }
}
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
    # Clear the desktop once, only after authorization and package validation,
    # before showing our independent progress window. Never close user apps.
    $desktopShell = $null
    try {
        $desktopShell = New-Object -ComObject Shell.Application
        $desktopShell.MinimizeAll()
    } catch { Write-Output 'Nao foi possivel minimizar as janelas. A atualizacao continuara normalmente.' }
    finally { if ($null -ne $desktopShell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($desktopShell) } }
    # Copy the UI script away from the installed files before they are replaced.
    # This companion never installs anything and needs no elevation.
    try {
        $progressState = Join-Path (Split-Path -Parent $Installer) ('progress-' + [Guid]::NewGuid().ToString('N') + '.json')
        $progressScript = $progressState + '.ps1'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UpdateProgress.ps1') -Destination $progressScript
        Publish-Progress 'Preparando atualização' 'O pacote foi validado. Aguardando o Orby encerrar com segurança.'
        $command = "& '" + $progressScript.Replace("'", "''") + "' -StateFile '" + $progressState.Replace("'", "''") + "' -OwnerPid " + $PID
        $progressProcess = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @('-NoProfile','-STA','-ExecutionPolicy','Bypass','-EncodedCommand',[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) -WindowStyle Hidden -PassThru
        $uiDeadline = [DateTime]::UtcNow.AddSeconds(8)
        while (!(Test-Path -LiteralPath ($progressState + '.ready')) -and !$progressProcess.HasExited -and [DateTime]::UtcNow -lt $uiDeadline) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path -LiteralPath ($progressState + '.ready')) -and !$progressProcess.HasExited) { $progressProcess.Kill(); $progressProcess = $null }
    } catch { $progressProcess = $null }
    # Publish readiness only after all prerequisites have passed. The UI stays
    # alive until this point; no installer runs while its parent still exists.
    $parent = Get-Process -Id $WaitPid -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText($ReadyFile + '.tmp', 'ready')
    [IO.File]::Move($ReadyFile + '.tmp', $ReadyFile)
    if ($null -ne $parent -and !$parent.WaitForExit(60000)) { throw 'O launcher nao encerrou. Instalacao cancelada.' }
    $existing = $null
    try { $existing = [Threading.Mutex]::OpenExisting('Local\OrbyLauncher.Launcher') }
    catch [Threading.WaitHandleCannotBeOpenedException] { }
    if ($null -ne $existing) {
        $existing.Dispose()
        throw 'Outra copia do Orby esta aberta. Feche-a pelo icone na bandeja e tente atualizar novamente. Nenhum processo sera encerrado a forca.'
    }
    $log = Join-Path (Split-Path -Parent $Installer) 'setup.log'
    # SILENT skips wizard questions but keeps Inno's actual installation
    # progress/status visible after the launcher exits. VERYSILENT + Hidden
    # left the desktop with no indication that installation was still running.
    Publish-Progress 'Instalando atualização' 'Substituindo os arquivos do Orby e preparando os componentes. Suas configurações serão preservadas.'
    $customProgress = $null -ne $progressProcess -and !$progressProcess.HasExited -and (Test-Path -LiteralPath ($progressState + '.ready'))
    $mode = '/SILENT'; $windowStyle = 'Normal'
    if ($customProgress) { $mode = '/VERYSILENT'; $windowStyle = 'Hidden' }
    $setup = Start-Process -FilePath $Installer -ArgumentList @($mode,'/SUPPRESSMSGBOXES','/NORESTART','/ORBYUPDATE=1',('/LOG="' + $log + '"')) -WindowStyle $windowStyle -Wait -PassThru
    if ($setup.ExitCode -notin @(0,3010)) { throw ('Instalador retornou erro ' + $setup.ExitCode + '. Consulte ' + $log) }
    Publish-Progress 'Abrindo o Orby' 'A instalação terminou. Aguardando a confirmação de abertura da versão instalada.'
    # The updater can have been started from a development/portable copy.
    # Reopen the actual registered installation, never that older source path.
    $installedKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7408A21-58E2-4E3A-A842-84EF85965350}_is1'
    $location = (Get-ItemProperty -LiteralPath $installedKey -ErrorAction Stop).InstallLocation
    if ([string]::IsNullOrWhiteSpace($location)) { throw 'Pasta da instalacao nao encontrada no registro.' }
    $installedLauncher = Join-Path $location 'OrbyLauncher.exe'
    if (!(Test-Path -LiteralPath $installedLauncher)) { throw 'Executavel instalado nao encontrado.' }
    $eventName = 'Local\OrbyLauncher.UpdateReady.' + [Guid]::NewGuid().ToString('N')
    $readyEvent = New-Object Threading.EventWaitHandle($false, [Threading.EventResetMode]::ManualReset, $eventName)
    $restartAttempted = $true
    $opened = Start-Process -FilePath $installedLauncher -ArgumentList @('--update-ready-event', $eventName) -WorkingDirectory $location -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $confirmed = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($readyEvent.WaitOne(250)) { $confirmed = $true; break }
        if ($opened.HasExited) { break }
    }
    if (!$confirmed) { throw 'Os arquivos foram atualizados, mas o Orby nao confirmou a abertura. Feche outras copias e abra o Orby pelo menu Iniciar. Suas configuracoes foram preservadas.' }
    @{ ExitCode=$setup.ExitCode; Utc=[DateTime]::UtcNow; Log=$log; LaunchConfirmed=$true; Launcher=$installedLauncher } | ConvertTo-Json | Set-Content -LiteralPath $record -Encoding UTF8
    Publish-Progress 'Atualização concluída' 'A versão instalada foi aberta. Você já pode continuar usando o Orby.' $true
    exit 0
} catch {
    $message = $_.Exception.Message
    Write-Output $message
    Publish-Progress 'Não foi possível concluir' $message $true $true
    if ($WaitPid -ne 0) {
        try { @{ ExitCode=-1; Error=$message; Utc=[DateTime]::UtcNow } | ConvertTo-Json | Set-Content -LiteralPath $record -Encoding UTF8 } catch { }
        try { Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show($message, 'Orby Launcher - Atualizacao', 'OK', 'Warning') | Out-Null } catch { }
        if (!$restartAttempted -and $null -eq (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $Launcher)) {
            try { Start-Process -FilePath $Launcher -WindowStyle Hidden } catch { Write-Output $_.Exception.Message }
        }
    }
    exit 1
} finally { if ($null -ne $lock) { $lock.Dispose() }; if ($null -ne $readyEvent) { $readyEvent.Dispose() } }
