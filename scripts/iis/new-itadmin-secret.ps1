#Requires -Version 5.1

# Optional developer utility for generating cryptographic secrets and setup key hashes.
# ITAdmin installation does not require this script; install-itadmin-server.ps1 generates secrets inline.

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(1, 4096)]
    [int]$ByteLength = 64,

    [Parameter()]
    [ValidateSet("Base64Url", "Base64", "Hex")]
    [string]$Format = "Base64Url",

    [Parameter()]
    [switch]$SetupKey,

    [Parameter()]
    [ValidateRange(1, 100)]
    [int]$Count = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


function ConvertTo-ITAdminBase64Url {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )

    $base64 = [Convert]::ToBase64String($Bytes)
    return $base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-ITAdminCryptographicSecret {
    param(
        [Parameter()]
        [ValidateRange(1, 4096)]
        [int]$ByteLength = 64,

        [Parameter()]
        [ValidateSet("Base64Url", "Base64", "Hex")]
        [string]$Format = "Base64Url"
    )

    $bytes = New-Object byte[] $ByteLength
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        if ($null -ne $rng) {
            $rng.Dispose()
        }
    }

    switch ($Format) {
        "Base64Url" { return ConvertTo-ITAdminBase64Url -Bytes $bytes }
        "Base64" { return [Convert]::ToBase64String($bytes) }
        "Hex" { return ([BitConverter]::ToString($bytes)).Replace("-", "").ToLowerInvariant() }
        default { throw "Unsupported secret format: $Format" }
    }
}

function New-ITAdminSetupKeyMaterial {
    param(
        [Parameter()]
        [ValidateRange(16, 128)]
        [int]$PlaintextByteLength = 32
    )

    $plaintext = New-ITAdminCryptographicSecret -ByteLength $PlaintextByteLength -Format "Base64Url"
    $hashBytes = [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($plaintext)
    )
    $hash = "sha256:{0}" -f (ConvertTo-ITAdminBase64Url -Bytes $hashBytes)

    return [PSCustomObject]@{
        PlaintextSetupKey = $plaintext
        SetupKeyHash = $hash
    }
}

function Invoke-ITAdminNewSecretMain {
    param(
        [int]$ByteLength,
        [string]$Format,
        [switch]$SetupKey,
        [int]$Count
    )

    if ($SetupKey) {
        $material = New-ITAdminSetupKeyMaterial
        Write-Host "Setup key (store securely; shown once): $($material.PlaintextSetupKey)"
        Write-Host "Setup key hash (store in machine env ITADMIN_Setup__SetupKeyHash): $($material.SetupKeyHash)"
        return
    }

    for ($index = 1; $index -le $Count; $index++) {
        $secret = New-ITAdminCryptographicSecret -ByteLength $ByteLength -Format $Format
        if ($Count -gt 1) {
            Write-Host ("Secret {0}: {1}" -f $index, $secret)
        }
        else {
            Write-Host $secret
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-ITAdminNewSecretMain -ByteLength $ByteLength -Format $Format -SetupKey:$SetupKey -Count $Count
}
