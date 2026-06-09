#requires -Version 7.0
<#
.SYNOPSIS
    Harness di sicurezza per il canale IPC Named Pipe di PassKey (\\.\pipe\PassKey.IPC).
    Riproduce e verifica le 3 criticità SEC-01/02/03 del Cluster 6 + hardening collaterale.

.DESCRIPTION
    Questo è il "cliente d'attacco" della Fase A / Fase E della checklist di sicurezza
    (piano-revisione-correzione-miglioramento.md).

    - FASE A (baseline): eseguito su un Desktop VULNERABILE → gli attacchi RIESCONO.
    - FASE E (verifica): eseguito sul Desktop CORRETTO → gli attacchi DEVONO fallire.

    Parla il protocollo wire reale del server (BrowserIpcService.cs):
      * frame = [4 byte length little-endian][JSON UTF-8]
      * UNA richiesta per connessione (il server chiude il pipe dopo ogni risposta)
      * envelope camelCase: version, action, requestId, clientId, payload
      * handshake ECDH P-256 -> HKDF-SHA256 (info "PassKey-IPC-Session") -> AES-256-GCM

    REQUISITI PER L'ESECUZIONE (parte del gate di test utente):
      * PassKey Desktop in esecuzione.
      * Vault SBLOCCATO per i test SEC-01/02/03 e l'happy-path.
      * Esegui anche una passata con vault BLOCCATO per verificare il fail-safe 'vault-locked'.

    NON stampa MAI password in chiaro: i segreti recuperati sono mascherati (lunghezza + ****).

.PARAMETER PipeName
    Nome del pipe (default: PassKey.IPC).

.PARAMETER Url
    URL usato per il test get-credentials (default: https://example.com).

.EXAMPLE
    pwsh -NoProfile -File .\scripts\ipc-security-test.ps1
#>

[CmdletBinding()]
param(
    [string]$PipeName = 'PassKey.IPC',
    [string]$Url      = 'https://example.com'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─────────────────────────────────────────────────────────────────────────────
# Risultati
# ─────────────────────────────────────────────────────────────────────────────
$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result {
    param(
        [Parameter(Mandatory)] [string]$Id,
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [ValidateSet('SECURE', 'VULNERABLE', 'INFO', 'SKIP', 'ERROR')] [string]$Verdict,
        [string]$Detail = ''
    )
    $script:Results.Add([pscustomobject]@{ Id = $Id; Name = $Name; Verdict = $Verdict; Detail = $Detail })
    $color = switch ($Verdict) {
        'SECURE'     { 'Green' }
        'VULNERABLE' { 'Red' }
        'ERROR'      { 'Red' }
        'SKIP'       { 'DarkYellow' }
        default      { 'Gray' }
    }
    Write-Host ("  [{0,-10}] {1} — {2} {3}" -f $Verdict, $Id, $Name, ($(if ($Detail) { "→ $Detail" } else { '' }))) -ForegroundColor $color
}

function Mask-Secret {
    param([string]$s)
    if ([string]::IsNullOrEmpty($s)) { return '(vuota)' }
    return ("len={0} «{1}****»" -f $s.Length, ($s.Substring(0, [Math]::Min(2, $s.Length))))
}

# ─────────────────────────────────────────────────────────────────────────────
# Trasporto: una richiesta per connessione (il server chiude dopo ogni risposta)
# ─────────────────────────────────────────────────────────────────────────────
function Read-Exact {
    param([System.IO.Stream]$Stream, [int]$Count)
    $buf = New-Object byte[] $Count
    $off = 0
    while ($off -lt $Count) {
        $n = $Stream.Read($buf, $off, $Count - $off)
        if ($n -le 0) { throw "Stream chiuso dopo $off/$Count byte." }
        $off += $n
    }
    return ,$buf
}

function Invoke-Ipc {
    <# Apre una connessione, invia UN envelope, legge UNA risposta, chiude. #>
    param(
        [Parameter(Mandatory)] [string]$Action,
        $Payload    = $null,
        [string]$ClientId = 'pk-sec-test',
        [int]$TimeoutMs   = 5000
    )
    if (-not [BitConverter]::IsLittleEndian) { throw "Piattaforma big-endian non supportata." }

    $envelope = [ordered]@{
        version   = 1
        action    = $Action
        requestId = [guid]::NewGuid().ToString()
        clientId  = $ClientId
        payload   = $Payload
    }
    $json  = $envelope | ConvertTo-Json -Depth 8 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)

    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect($TimeoutMs)
        $len = [BitConverter]::GetBytes([int]$bytes.Length)   # little-endian su Windows
        $pipe.Write($len, 0, 4)
        $pipe.Write($bytes, 0, $bytes.Length)
        $pipe.Flush()

        $lenBuf = Read-Exact -Stream $pipe -Count 4
        $respLen = [BitConverter]::ToInt32($lenBuf, 0)
        if ($respLen -le 0 -or $respLen -gt 1MB) { throw "Lunghezza risposta non valida: $respLen" }
        $respBuf = Read-Exact -Stream $pipe -Count $respLen
        $respJson = [System.Text.Encoding]::UTF8.GetString($respBuf)
        return $respJson | ConvertFrom-Json
    }
    finally {
        $pipe.Dispose()
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Handshake ECDH legittimo (come l'estensione reale)
# ─────────────────────────────────────────────────────────────────────────────
function New-EcdhSession {
    param([string]$ClientId = 'pk-sec-test')

    $ecdh = [System.Security.Cryptography.ECDiffieHellman]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
    try {
        $spki   = $ecdh.ExportSubjectPublicKeyInfo()
        $pubB64 = [Convert]::ToBase64String($spki)

        $resp = Invoke-Ipc -Action 'exchange-keys' -Payload @{ publicKey = $pubB64 } -ClientId $ClientId
        if (-not $resp.success) { throw "exchange-keys fallito: $($resp.error)" }

        $serverSpki  = [Convert]::FromBase64String($resp.payload.publicKey)
        $serverEcdh  = [System.Security.Cryptography.ECDiffieHellman]::Create()
        $br = 0
        $serverEcdh.ImportSubjectPublicKeyInfo($serverSpki, [ref]$br)

        $shared = $ecdh.DeriveRawSecretAgreement($serverEcdh.PublicKey)
        $info   = [System.Text.Encoding]::UTF8.GetBytes('PassKey-IPC-Session')
        $key    = [System.Security.Cryptography.HKDF]::DeriveKey(
            [System.Security.Cryptography.HashAlgorithmName]::SHA256, $shared, 32, $null, $info)

        return [pscustomobject]@{ SessionId = $resp.payload.sessionId; Key = $key; ClientId = $ClientId }
    }
    finally {
        $ecdh.Dispose()
    }
}

function Unprotect-Password {
    param([byte[]]$Key, [string]$NonceB64, [string]$EncB64)
    $enc   = [Convert]::FromBase64String($EncB64)
    $nonce = [Convert]::FromBase64String($NonceB64)
    $tagLen = 16
    if ($enc.Length -lt $tagLen) { throw "Ciphertext troppo corto." }
    $ct  = $enc[0..($enc.Length - $tagLen - 1)]
    $tag = $enc[($enc.Length - $tagLen)..($enc.Length - 1)]
    $pt  = New-Object byte[] $ct.Length
    $gcm = [System.Security.Cryptography.AesGcm]::new($Key, 16)
    try {
        $gcm.Decrypt($nonce, $ct, $tag, $pt)
        return [System.Text.Encoding]::UTF8.GetString($pt)
    }
    finally { $gcm.Dispose() }
}

# ═════════════════════════════════════════════════════════════════════════════
# ESECUZIONE
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "`n=== PassKey IPC Security Test — pipe \\.\pipe\$PipeName ===`n" -ForegroundColor Cyan

# --- T0: connettività + stato vault -----------------------------------------
$unlocked = $false
try {
    $status = Invoke-Ipc -Action 'get-status'
    if ($status.success) {
        $unlocked = [bool]$status.payload.unlocked
        Add-Result -Id 'T0' -Name 'Connettività + get-status' -Verdict 'INFO' `
            -Detail ("unlocked={0}, entries={1}" -f $unlocked, $status.payload.entryCount)
    } else {
        Add-Result -Id 'T0' -Name 'Connettività' -Verdict 'ERROR' -Detail "get-status error: $($status.error)"
    }
}
catch {
    Add-Result -Id 'T0' -Name 'Connettività' -Verdict 'ERROR' -Detail "Desktop non in esecuzione? $($_.Exception.Message)"
    Write-Host "`nImpossibile connettersi al pipe. Avvia PassKey Desktop e riprova.`n" -ForegroundColor Red
    return
}

# --- Sessione legittima + recupero di un id credenziale ----------------------
$legit = $null
$sampleId = $null
if ($unlocked) {
    try {
        $legit = New-EcdhSession -ClientId 'pk-legit'
        Add-Result -Id 'S0' -Name 'Handshake ECDH legittimo' -Verdict 'INFO' -Detail "sessionId ok"

        # get-all-credentials con sessione legittima: dopo SEC-03 questo richiede CONSENSO (dialog).
        $all = Invoke-Ipc -Action 'get-all-credentials' -ClientId 'pk-legit'
        if ($all.success -and $all.payload.credentials.Count -gt 0) {
            $sampleId = $all.payload.credentials[0].id
        }
    }
    catch {
        Add-Result -Id 'S0' -Name 'Handshake/lista' -Verdict 'ERROR' -Detail $_.Exception.Message
    }
} else {
    Add-Result -Id 'S0' -Name 'Setup test' -Verdict 'SKIP' -Detail 'Vault bloccato: vedi sezione fail-safe in fondo'
}

# --- SEC-01: password senza sessione ECDH ------------------------------------
# Atteso SICURO: error 'ecdh-session-required' e NESSUNA password.
# VULNERABILE: ritorna payload con password (in chiaro o comunque servita).
if ($unlocked -and $sampleId) {
    try {
        $r = Invoke-Ipc -Action 'get-credential-password' -Payload @{ id = $sampleId } -ClientId 'pk-attacker'
        if (-not $r.success -and $r.error -eq 'ecdh-session-required') {
            Add-Result -Id 'SEC-01' -Name 'Plaintext fallback senza handshake' -Verdict 'SECURE' -Detail "error=$($r.error)"
        }
        elseif (-not $r.success -and $r.error -eq 'no-session') {
            Add-Result -Id 'SEC-01' -Name 'Plaintext fallback senza handshake' -Verdict 'SECURE' `
                -Detail "error=no-session (da allineare a 'ecdh-session-required')"
        }
        elseif ($r.success -and $r.payload.encryptedPassword) {
            $nonceEmpty = [string]::IsNullOrEmpty($r.payload.nonce)
            Add-Result -Id 'SEC-01' -Name 'Plaintext fallback senza handshake' -Verdict 'VULNERABLE' `
                -Detail ("password SERVITA senza sessione (nonce vuoto={0})" -f $nonceEmpty)
        }
        else {
            Add-Result -Id 'SEC-01' -Name 'Plaintext fallback senza handshake' -Verdict 'INFO' -Detail "risposta inattesa: success=$($r.success) error=$($r.error)"
        }
    }
    catch { Add-Result -Id 'SEC-01' -Name 'Plaintext fallback' -Verdict 'ERROR' -Detail $_.Exception.Message }
} else {
    Add-Result -Id 'SEC-01' -Name 'Plaintext fallback' -Verdict 'SKIP' -Detail 'serve vault sbloccato + almeno 1 credenziale'
}

# --- SEC-02: dirottamento sessione (clientId non corrispondente) -------------
# Usa il sessionId della sessione legittima ma con un clientId diverso.
# Atteso SICURO: rifiutata (la sessione è legata al clientId dell'handshake).
# VULNERABILE: la richiesta viene servita (binding assente o solo via sessionId).
if ($unlocked -and $sampleId -and $legit) {
    try {
        $r = Invoke-Ipc -Action 'get-credential-password' `
            -Payload @{ id = $sampleId; sessionId = $legit.SessionId } -ClientId 'pk-hijacker'
        if (-not $r.success) {
            Add-Result -Id 'SEC-02' -Name 'Hijack sessione via clientId errato' -Verdict 'SECURE' -Detail "error=$($r.error)"
        }
        elseif ($r.success -and $r.payload.encryptedPassword) {
            Add-Result -Id 'SEC-02' -Name 'Hijack sessione via clientId errato' -Verdict 'VULNERABLE' `
                -Detail "il server ha servito la password a un clientId diverso da quello dell'handshake"
        }
        else {
            Add-Result -Id 'SEC-02' -Name 'Hijack sessione' -Verdict 'INFO' -Detail "risposta inattesa"
        }
    }
    catch { Add-Result -Id 'SEC-02' -Name 'Hijack sessione' -Verdict 'ERROR' -Detail $_.Exception.Message }
} else {
    Add-Result -Id 'SEC-02' -Name 'Hijack sessione' -Verdict 'SKIP' -Detail 'serve setup completo'
}

# --- SEC-03: consenso — ora SOLO su get-credential-password ------------------
# Design corrente: get-all-credentials NON chiede consenso (espone solo metadati:
# titoli/username, nessuna password) → restituisce subito = comportamento ATTESO.
# Il consenso vero è verificato dal test FUNC (get-credential-password → dialog sul Desktop).
if ($unlocked -and $legit) {
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $r  = Invoke-Ipc -Action 'get-all-credentials' -ClientId 'pk-legit'
        $sw.Stop()
        if ($r.success) {
            Add-Result -Id 'SEC-03' -Name 'get-all-credentials senza prompt (atteso)' -Verdict 'SECURE' `
                -Detail ("metadati restituiti in {0}ms, nessun dialog — consenso spostato su copia/compila password" -f $sw.ElapsedMilliseconds)
        }
        else {
            Add-Result -Id 'SEC-03' -Name 'get-all-credentials' -Verdict 'INFO' -Detail "success=$($r.success) error=$($r.error)"
        }
    }
    catch { Add-Result -Id 'SEC-03' -Name 'get-all-credentials' -Verdict 'ERROR' -Detail $_.Exception.Message }
} else {
    Add-Result -Id 'SEC-03' -Name 'get-all-credentials' -Verdict 'SKIP' -Detail 'serve vault sbloccato'
}

# --- FUNC + SEC-03 consenso: happy-path cifrato CON dialog di consenso --------
# Sessione valida + clientId corrispondente → supera i controlli SEC-01/02 e arriva
# al consenso SEC-03 → sul Desktop compare il dialog: clicca CONSENTI per il decrypt,
# oppure NEGA per verificare che non esca nulla (error=consent-denied).
if ($unlocked -and $sampleId -and $legit) {
    Write-Host "  [FUNC/SEC-03] osserva il Desktop: comparira' il dialog di consenso → clicca CONSENTI" -ForegroundColor Yellow
    try {
        $r = Invoke-Ipc -Action 'get-credential-password' `
            -Payload @{ id = $sampleId; sessionId = $legit.SessionId } -ClientId $legit.ClientId -TimeoutMs 120000
        if ($r.success -and $r.payload.nonce) {
            $pw = Unprotect-Password -Key $legit.Key -NonceB64 $r.payload.nonce -EncB64 $r.payload.encryptedPassword
            Add-Result -Id 'FUNC' -Name 'Happy-path cifrato (consenso → decrypt OK)' -Verdict 'INFO' -Detail (Mask-Secret $pw)
        }
        elseif (-not $r.success) {
            Add-Result -Id 'FUNC' -Name 'Happy-path cifrato' -Verdict 'INFO' -Detail "error=$($r.error) (se hai cliccato NEGA, 'consent-denied' è atteso)"
        }
        else {
            Add-Result -Id 'FUNC' -Name 'Happy-path cifrato' -Verdict 'INFO' -Detail "nonce assente: verificare cifratura"
        }
    }
    catch { Add-Result -Id 'FUNC' -Name 'Happy-path cifrato' -Verdict 'ERROR' -Detail $_.Exception.Message }
}

# --- Fail-safe: comportamento a vault BLOCCATO -------------------------------
if (-not $unlocked) {
    try {
        $r = Invoke-Ipc -Action 'get-all-credentials' -ClientId 'pk-locked'
        if (-not $r.success -and $r.error -eq 'vault-locked') {
            Add-Result -Id 'LOCK' -Name 'Fail-safe vault bloccato' -Verdict 'SECURE' -Detail "error=vault-locked"
        } else {
            Add-Result -Id 'LOCK' -Name 'Fail-safe vault bloccato' -Verdict 'VULNERABLE' -Detail "success=$($r.success) error=$($r.error)"
        }
    }
    catch { Add-Result -Id 'LOCK' -Name 'Fail-safe vault bloccato' -Verdict 'ERROR' -Detail $_.Exception.Message }
}

# ─────────────────────────────────────────────────────────────────────────────
# Riepilogo
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "`n=== Riepilogo ===" -ForegroundColor Cyan
$script:Results | Format-Table -AutoSize Id, Verdict, Name, Detail | Out-Host

$vuln = @($script:Results | Where-Object Verdict -eq 'VULNERABLE')
$err  = @($script:Results | Where-Object Verdict -eq 'ERROR')
if ($vuln.Count -gt 0) {
    Write-Host ("ATTENZIONE: {0} test VULNERABLE. Le falle NON sono chiuse." -f $vuln.Count) -ForegroundColor Red
    exit 2
} elseif ($err.Count -gt 0) {
    Write-Host ("{0} errori di esecuzione: rivedere setup (app avviata? vault sbloccato?)." -f $err.Count) -ForegroundColor DarkYellow
    exit 1
} else {
    Write-Host "Nessun test VULNERABLE. (Conferma manualmente il dialog SEC-03 e l'happy-path estensione.)" -ForegroundColor Green
    exit 0
}
