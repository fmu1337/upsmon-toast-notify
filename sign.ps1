# Authenticode-sign EventMsg.exe (self-signed cert for local trust).
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,
    [switch]$ExportCert,
    [switch]$TrustLocalMachine
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $FilePath)) { throw "File not found: $FilePath" }

$certSubject = 'CN=Upsmon Toast Notify, O=UpsmonToastNotify'
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $certSubject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $certSubject `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "Created code signing certificate: $($cert.Thumbprint)"
}

$timestamp = 'http://timestamp.digicert.com'
$signed = $false

$signtool = Get-ChildItem -Path @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\signtool.exe"
) -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($signtool) {
    & $signtool.FullName sign /fd SHA256 /tr $timestamp /td SHA256 /sha1 $cert.Thumbprint $FilePath
    if ($LASTEXITCODE -eq 0) { $signed = $true }
}

if (-not $signed) {
    Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $timestamp | Out-Null
}

Write-Host "Signed -> $FilePath"

$cerPath = Join-Path (Split-Path -Parent $FilePath) 'UpsmonToastNotify.cer'
if ($ExportCert -or $TrustLocalMachine) {
    Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
    Write-Host "Certificate exported -> $cerPath"
}

if ($TrustLocalMachine) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { throw 'TrustLocalMachine requires Administrator.' }
    if (-not (Test-Path -LiteralPath $cerPath)) { throw "Certificate file not found: $cerPath" }
    Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Write-Host 'Certificate trusted on this PC (LocalMachine Root + TrustedPublisher).'
}
