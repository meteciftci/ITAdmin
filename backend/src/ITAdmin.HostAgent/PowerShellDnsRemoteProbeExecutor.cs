using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Remoting;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

public interface IDnsRemoteProbeExecutor
{
    Task<HostAgentDnsProbeResult> ProbeAsync(HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsInventoryPage> ReadInventoryPageAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsRecordMutationResult> MutateRecordAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsZoneMutationResult> MutateZoneAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsServerSettingsResult> ManageServerSettingsAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsPolicyConfigurationResult> ManagePolicyConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnssecConfigurationResult> ManageDnssecConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsScavengingConfigurationResult> ManageDnsScavengingAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
    Task<HostAgentDnsNetworkConfigurationResult> ManageDnsNetworkConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
}

internal static class DnsRemoteCapabilityProbe
{
    // Fixed and owned by the agent. No request value is ever concatenated into this script.
    internal const string Script = """
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop
        $dnsServer = Get-DnsServer -ErrorAction Stop
        $zones = @(Get-DnsServerZone -ErrorAction Stop)
        $module = Get-Module DnsServer
        $versionParts = @(
            $dnsServer.ServerSetting.MajorVersion,
            $dnsServer.ServerSetting.MinorVersion,
            $dnsServer.ServerSetting.BuildNumber
        ) | Where-Object { $null -ne $_ -and "$_" -ne '' }
        [pscustomobject]@{
            OperatingSystemVersion = [Environment]::OSVersion.VersionString
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            DnsModuleVersion = if ($module) { $module.Version.ToString() } else { $null }
            DnsServerVersion = if ($versionParts.Count -gt 0) { $versionParts -join '.' } else { $null }
            ZoneCount = $zones.Count
            Zones = [bool](Get-Command Get-DnsServerZone -ErrorAction SilentlyContinue)
            Records = [bool](Get-Command Get-DnsServerResourceRecord -ErrorAction SilentlyContinue)
            ServerSettings = [bool](Get-Command Get-DnsServerForwarder -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Set-DnsServerForwarder -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Remove-DnsServerForwarder -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Get-DnsServerRecursion -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Set-DnsServerRecursion -ErrorAction SilentlyContinue)
            Dnssec = [bool](Get-Command Get-DnsServerDnsSecZoneSetting -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Get-DnsServerSigningKey -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Invoke-DnsServerZoneSign -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Invoke-DnsServerZoneUnsign -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Invoke-DnsServerSigningKeyRollover -ErrorAction SilentlyContinue)
            Policies = [bool](Get-Command Get-DnsServerQueryResolutionPolicy -ErrorAction SilentlyContinue)
            Scopes = [bool](Get-Command Get-DnsServerZoneScope -ErrorAction SilentlyContinue)
            Cache = [bool](Get-Command Clear-DnsServerCache -ErrorAction SilentlyContinue)
            NetworkConfiguration = [bool](Get-Command Get-DnsServerSetting -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Set-DnsServerSetting -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Get-DnsServerRootHint -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Add-DnsServerRootHint -ErrorAction SilentlyContinue) -and
                [bool](Get-Command Remove-DnsServerRootHint -ErrorAction SilentlyContinue)
        }
        """;
}

internal static class DnsRemoteInventoryProbe
{
    // Values enter only through AddParameter. Neither the API nor request data can alter this code.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Zones','Records')][string]$InventoryKind,
            [Parameter(Mandatory=$true)][int]$Offset,
            [Parameter(Mandatory=$true)][ValidateRange(1,500)][int]$PageSize,
            [string]$ZoneName,
            [string]$ZoneScope,
            [string]$VirtualizationInstance
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        if ($InventoryKind -eq 'Zones') {
            $items = [System.Collections.Generic.List[object]]::new()
            $instances = [System.Collections.Generic.List[string]]::new()
            $instances.Add('')
            if (Get-Command Get-DnsServerVirtualizationInstance -ErrorAction SilentlyContinue) {
                foreach ($instance in @(Get-DnsServerVirtualizationInstance -ErrorAction Stop)) {
                    $instanceName = if ($instance.VirtualizationInstance) {
                        "$($instance.VirtualizationInstance)"
                    } elseif ($instance.Name) { "$($instance.Name)" } else { '' }
                    if ($instanceName -and -not $instances.Contains($instanceName)) {
                        $instances.Add($instanceName)
                    }
                }
            }

            $zoneScopeCommand = Get-Command Get-DnsServerZoneScope -ErrorAction SilentlyContinue
            foreach ($instanceName in $instances) {
                $zoneParameters = @{}
                if ($instanceName) { $zoneParameters.VirtualizationInstance = $instanceName }
                foreach ($zone in @(Get-DnsServerZone @zoneParameters -ErrorAction Stop)) {
                    $scopes = @()
                    if ($zoneScopeCommand -and
                        (-not $instanceName -or $zoneScopeCommand.Parameters.ContainsKey('VirtualizationInstance'))) {
                        $scopeParameters = @{ ZoneName = "$($zone.ZoneName)" }
                        if ($instanceName) { $scopeParameters.VirtualizationInstance = $instanceName }
                        $scopes = @(Get-DnsServerZoneScope @scopeParameters -ErrorAction Stop | ForEach-Object {
                            $scopeName = if ($_.ZoneScope) { "$($_.ZoneScope)" } elseif ($_.Name) { "$($_.Name)" } else { '' }
                            if ($scopeName -and $scopeName -ne "$($zone.ZoneName)") { $scopeName }
                        } | Sort-Object -Unique)
                    }
                    $items.Add([pscustomobject]@{
                        Name = "$($zone.ZoneName)"
                        ZoneType = "$($zone.ZoneType)"
                        IsReverseLookupZone = [bool]$zone.IsReverseLookupZone
                        IsDsIntegrated = [bool]$zone.IsDsIntegrated
                        IsSigned = [bool]$zone.IsSigned
                        IsPaused = [bool]$zone.IsPaused
                        DynamicUpdate = if ($null -ne $zone.DynamicUpdate) { "$($zone.DynamicUpdate)" } else { $null }
                        ReplicationScope = if ($null -ne $zone.ReplicationScope) { "$($zone.ReplicationScope)" } else { $null }
                        DirectoryPartitionName = if ($zone.DirectoryPartitionName) { "$($zone.DirectoryPartitionName)" } else { $null }
                        ZoneFile = if ($zone.ZoneFile) { "$($zone.ZoneFile)" } else { $null }
                        VirtualizationInstance = if ($instanceName) { $instanceName } else { $null }
                        ZoneScopes = $scopes
                        IsAutoCreated = [bool]$zone.IsAutoCreated
                        MasterServers = @($zone.MasterServers | ForEach-Object {
                            if ($_ -is [System.Net.IPAddress]) { $_.IPAddressToString } else { "$_" }
                        })
                        ForwarderTimeoutSeconds = if ($null -ne $zone.ForwarderTimeout) { [int]$zone.ForwarderTimeout } else { $null }
                        UseRecursion = if ($null -ne $zone.UseRecursion) { [bool]$zone.UseRecursion } else { $null }
                    })
                }
            }
            $items | Sort-Object VirtualizationInstance, Name | Select-Object -Skip $Offset -First ($PageSize + 1)
            return
        }

        $recordParameters = @{ ZoneName = $ZoneName }
        if ($ZoneScope) { $recordParameters.ZoneScope = $ZoneScope }
        if ($VirtualizationInstance) { $recordParameters.VirtualizationInstance = $VirtualizationInstance }
        $recordItems = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction Stop | ForEach-Object {
                $record = $_
                $data = [ordered]@{}
                foreach ($property in @($record.RecordData.CimInstanceProperties) | Sort-Object Name) {
                    $value = $property.Value
                    if ($null -eq $value) {
                        $data[$property.Name] = $null
                    } elseif ($value -is [System.Array]) {
                        $data[$property.Name] = @($value | ForEach-Object { "$_" })
                    } elseif ($value -is [System.Net.IPAddress]) {
                        $data[$property.Name] = $value.IPAddressToString
                    } elseif ($value -is [TimeSpan]) {
                        $data[$property.Name] = [long]$value.TotalSeconds
                    } elseif ($value -is [DateTime]) {
                        $data[$property.Name] = $value.ToUniversalTime().ToString('O')
                    } else {
                        $data[$property.Name] = "$value"
                    }
                }
                [pscustomobject]@{
                    RelativeName = if ($record.HostName) { "$($record.HostName)" } else { '@' }
                    RecordType = "$($record.RecordType)".ToUpperInvariant()
                    RecordDataJson = $data | ConvertTo-Json -Compress -Depth 8
                    TimeToLiveSeconds = [int][Math]::Max(0, [Math]::Min([int]::MaxValue, [Math]::Round($record.TimeToLive.TotalSeconds)))
                    Timestamp = if ($record.Timestamp -is [DateTime]) { $record.Timestamp.ToUniversalTime().ToString('O') } else { $null }
                    ZoneScope = if ($ZoneScope) { $ZoneScope } else { $null }
                    VirtualizationInstance = if ($VirtualizationInstance) { $VirtualizationInstance } else { $null }
                }
            })
        $recordItems | Sort-Object RelativeName, RecordType, RecordDataJson, TimeToLiveSeconds, Timestamp |
            Select-Object -Skip $Offset -First ($PageSize + 1)
        """;
}

internal static class DnsRemoteRecordMutation
{
    // This is an allowlisted, typed command. Request values are bound with AddParameter and never
    // interpolated into executable text.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Create','Update','Delete')][string]$MutationKind,
            [Parameter(Mandatory=$true)][string]$ZoneName,
            [string]$ZoneScope,
            [string]$VirtualizationInstance,
            [Parameter(Mandatory=$true)][string]$RelativeName,
            [Parameter(Mandatory=$true)][ValidateSet('A','AAAA','CNAME','MX','NS','PTR','SRV','TXT')][string]$RecordType,
            [string[]]$RecordValues,
            [Parameter(Mandatory=$true)][int]$TimeToLiveSeconds,
            [string]$ExpectedRecordDataJson,
            [int]$ExpectedRecordTimeToLiveSeconds
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Convert-Record([object]$record) {
            if ($null -eq $record) { return $null }
            $data = [ordered]@{}
            foreach ($property in @($record.RecordData.CimInstanceProperties) | Sort-Object Name) {
                $value = $property.Value
                if ($null -eq $value) { $data[$property.Name] = $null }
                elseif ($value -is [System.Array]) { $data[$property.Name] = @($value | ForEach-Object { "$_" }) }
                elseif ($value -is [System.Net.IPAddress]) { $data[$property.Name] = $value.IPAddressToString }
                elseif ($value -is [TimeSpan]) { $data[$property.Name] = [long]$value.TotalSeconds }
                elseif ($value -is [DateTime]) { $data[$property.Name] = $value.ToUniversalTime().ToString('O') }
                else { $data[$property.Name] = "$value" }
            }
            [pscustomobject]@{
                RelativeName = if ($record.HostName) { "$($record.HostName)" } else { '@' }
                RecordType = "$($record.RecordType)".ToUpperInvariant()
                RecordDataJson = $data | ConvertTo-Json -Compress -Depth 8
                TimeToLiveSeconds = [int][Math]::Max(0, [Math]::Min([int]::MaxValue, [Math]::Round($record.TimeToLive.TotalSeconds)))
                Timestamp = if ($record.Timestamp -is [DateTime]) { $record.Timestamp.ToUniversalTime().ToString('O') } else { $null }
                ZoneScope = if ($ZoneScope) { $ZoneScope } else { $null }
                VirtualizationInstance = if ($VirtualizationInstance) { $VirtualizationInstance } else { $null }
            }
        }

        function Get-RecordHash([object]$record) {
            $item = Convert-Record $record
            $hashInput = @(
                $ZoneName.ToLowerInvariant(), $item.RelativeName.ToLowerInvariant(), $item.RecordType,
                $item.RecordDataJson, "$($item.TimeToLiveSeconds)",
                $(if ($ZoneScope) { $ZoneScope.ToLowerInvariant() } else { '' }),
                $(if ($VirtualizationInstance) { $VirtualizationInstance.ToLowerInvariant() } else { '' })
            ) -join "`n"
            $bytes = [Text.Encoding]::UTF8.GetBytes($hashInput)
            $sha = [Security.Cryptography.SHA256]::Create()
            try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose() }
        }

        function Test-ExpectedRecord([object]$record) {
            $item = Convert-Record $record
            $expectedData = ($ExpectedRecordDataJson | ConvertFrom-Json -ErrorAction Stop) |
                ConvertTo-Json -Compress -Depth 8
            return $item.RelativeName -ieq $RelativeName -and $item.RecordType -eq $RecordType -and
                $item.RecordDataJson -ceq $expectedData -and
                $item.TimeToLiveSeconds -eq $ExpectedRecordTimeToLiveSeconds
        }

        function Get-RecordParameters {
            $parameters = @{ ZoneName = $ZoneName; Name = $RelativeName; RRType = $RecordType }
            if ($ZoneScope) { $parameters.ZoneScope = $ZoneScope }
            if ($VirtualizationInstance) { $parameters.VirtualizationInstance = $VirtualizationInstance }
            return $parameters
        }

        function Get-WriteParameters {
            $parameters = @{ ZoneName = $ZoneName }
            if ($ZoneScope) { $parameters.ZoneScope = $ZoneScope }
            if ($VirtualizationInstance) { $parameters.VirtualizationInstance = $VirtualizationInstance }
            return $parameters
        }

        function Set-RecordData([object]$record, [string]$type, [string[]]$values) {
            switch ($type) {
                'A' { $record.RecordData.IPv4Address = [Net.IPAddress]::Parse($values[0]) }
                'AAAA' { $record.RecordData.IPv6Address = [Net.IPAddress]::Parse($values[0]) }
                'CNAME' { $record.RecordData.HostNameAlias = $values[0] }
                'MX' { $record.RecordData.Preference = [uint16]$values[0]; $record.RecordData.MailExchange = $values[1] }
                'NS' { $record.RecordData.NameServer = $values[0] }
                'PTR' { $record.RecordData.PtrDomainName = $values[0] }
                'SRV' {
                    $record.RecordData.Priority = [uint16]$values[0]
                    $record.RecordData.Weight = [uint16]$values[1]
                    $record.RecordData.Port = [uint16]$values[2]
                    $record.RecordData.DomainName = $values[3]
                }
                'TXT' { $record.RecordData.DescriptiveText = $values[0] }
            }
        }

        function Add-Record {
            $parameters = Get-WriteParameters
            $parameters.Name = $RelativeName
            $parameters.TimeToLive = [TimeSpan]::FromSeconds($TimeToLiveSeconds)
            $parameters.PassThru = $true
            switch ($RecordType) {
                'A' { $parameters.A = $true; $parameters.IPv4Address = [Net.IPAddress]::Parse($RecordValues[0]) }
                'AAAA' { $parameters.AAAA = $true; $parameters.IPv6Address = [Net.IPAddress]::Parse($RecordValues[0]) }
                'CNAME' { $parameters.CName = $true; $parameters.HostNameAlias = $RecordValues[0] }
                'MX' { $parameters.MX = $true; $parameters.Preference = [uint16]$RecordValues[0]; $parameters.MailExchange = $RecordValues[1] }
                'NS' { $parameters.NS = $true; $parameters.NameServer = $RecordValues[0] }
                'PTR' { $parameters.Ptr = $true; $parameters.PtrDomainName = $RecordValues[0] }
                'SRV' {
                    $parameters.Srv = $true; $parameters.Priority = [uint16]$RecordValues[0]
                    $parameters.Weight = [uint16]$RecordValues[1]; $parameters.Port = [uint16]$RecordValues[2]
                    $parameters.DomainName = $RecordValues[3]
                }
                'TXT' { $parameters.Txt = $true; $parameters.DescriptiveText = $RecordValues[0] }
            }
            return Add-DnsServerResourceRecord @parameters -ErrorAction Stop
        }

        try {
            if ($MutationKind -eq 'Create') {
                $written = Add-Record
                $writtenHash = Get-RecordHash $written
                $recordParameters = Get-RecordParameters
                $readBack = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction Stop |
                    Where-Object { (Get-RecordHash $_) -eq $writtenHash }) | Select-Object -First 1
                return [pscustomobject]@{
                    Success = ($null -ne $readBack); FailureKind = if ($readBack) { $null } else { 'ReadBackFailed' }
                    Message = if ($readBack) { 'DNS record created.' } else { 'The record was written but could not be verified.' }
                    BeforeRecordJson = $null
                    AfterRecordJson = if ($readBack) { (Convert-Record $readBack) | ConvertTo-Json -Compress -Depth 10 } else { $null }
                }
            }

            $recordParameters = Get-RecordParameters
            $matches = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction Stop |
                Where-Object { Test-ExpectedRecord $_ })
            if ($matches.Count -ne 1) {
                return [pscustomobject]@{
                    Success = $false; FailureKind = 'RecordChanged'
                    Message = 'The DNS record no longer matches the inventory snapshot. Synchronize and retry.'
                    BeforeRecordJson = $null; AfterRecordJson = $null
                }
            }
            $old = $matches[0]
            $beforeJson = (Convert-Record $old) | ConvertTo-Json -Compress -Depth 10
            $writeParameters = Get-WriteParameters
            if ($MutationKind -eq 'Delete') {
                Remove-DnsServerResourceRecord @writeParameters -InputObject $old -Force -ErrorAction Stop
                $stillPresent = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction SilentlyContinue |
                    Where-Object { Test-ExpectedRecord $_ }).Count -gt 0
                return [pscustomobject]@{
                    Success = (-not $stillPresent); FailureKind = if ($stillPresent) { 'ReadBackFailed' } else { $null }
                    Message = if ($stillPresent) { 'The record delete could not be verified.' } else { 'DNS record deleted.' }
                    BeforeRecordJson = $beforeJson; AfterRecordJson = $null
                }
            }

            $new = [ciminstance]::new($old)
            $new.TimeToLive = [TimeSpan]::FromSeconds($TimeToLiveSeconds)
            Set-RecordData $new $RecordType $RecordValues
            $written = Set-DnsServerResourceRecord @writeParameters -OldInputObject $old -NewInputObject $new -PassThru -ErrorAction Stop
            $writtenHash = Get-RecordHash $written
            $readBack = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction Stop |
                Where-Object { (Get-RecordHash $_) -eq $writtenHash }) | Select-Object -First 1
            return [pscustomobject]@{
                Success = ($null -ne $readBack); FailureKind = if ($readBack) { $null } else { 'ReadBackFailed' }
                Message = if ($readBack) { 'DNS record updated.' } else { 'The record was written but could not be verified.' }
                BeforeRecordJson = $beforeJson
                AfterRecordJson = if ($readBack) { (Convert-Record $readBack) | ConvertTo-Json -Compress -Depth 10 } else { $null }
            }
        } catch {
            return [pscustomobject]@{
                Success = $false; FailureKind = 'DnsRecordMutationFailed'
                Message = 'The DNS server rejected the record operation.'
                BeforeRecordJson = $null; AfterRecordJson = $null
            }
        }
        """;
}

internal static class DnsRemoteZoneMutation
{
    // Fixed allowlisted implementation. Every request value is supplied as a bound parameter.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Create','Update','Delete')][string]$MutationKind,
            [Parameter(Mandatory=$true)][ValidateSet('Primary','Secondary','Stub','Forwarder')][string]$ZoneKind,
            [Parameter(Mandatory=$true)][string]$ZoneName,
            [Parameter(Mandatory=$true)][bool]$IsDsIntegrated,
            [string]$DynamicUpdate,
            [string]$ReplicationScope,
            [string]$DirectoryPartitionName,
            [string]$ZoneFile,
            [string[]]$MasterServers,
            [int]$ForwarderTimeoutSeconds,
            [bool]$UseRecursion,
            [string]$ExpectedZoneStateJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Convert-Zone([object]$zone) {
            if ($null -eq $zone) { return $null }
            [ordered]@{
                Name = "$($zone.ZoneName)"
                ZoneType = "$($zone.ZoneType)"
                IsReverseLookupZone = [bool]$zone.IsReverseLookupZone
                IsDsIntegrated = [bool]$zone.IsDsIntegrated
                IsSigned = [bool]$zone.IsSigned
                IsPaused = [bool]$zone.IsPaused
                DynamicUpdate = if ($null -ne $zone.DynamicUpdate) { "$($zone.DynamicUpdate)" } else { $null }
                ReplicationScope = if ($null -ne $zone.ReplicationScope) { "$($zone.ReplicationScope)" } else { $null }
                DirectoryPartitionName = if ($zone.DirectoryPartitionName) { "$($zone.DirectoryPartitionName)" } else { $null }
                ZoneFile = if ($zone.ZoneFile) { "$($zone.ZoneFile)" } else { $null }
                VirtualizationInstance = $null
                ZoneScopes = @()
                IsAutoCreated = [bool]$zone.IsAutoCreated
                MasterServers = @($zone.MasterServers | ForEach-Object {
                    if ($_ -is [System.Net.IPAddress]) { $_.IPAddressToString } else { "$_" }
                } | Sort-Object)
                ForwarderTimeoutSeconds = if ($null -ne $zone.ForwarderTimeout) { [int]$zone.ForwarderTimeout } else { $null }
                UseRecursion = if ($null -ne $zone.UseRecursion) { [bool]$zone.UseRecursion } else { $null }
            }
        }

        function Get-LiveZone {
            return Get-DnsServerZone -Name $ZoneName -ErrorAction SilentlyContinue
        }

        function Test-ExpectedZone([object]$zone) {
            $actual = Convert-Zone $zone
            $expected = $ExpectedZoneStateJson | ConvertFrom-Json -ErrorAction Stop
            $actualMasters = @($actual.MasterServers | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object) -join ','
            $expectedMasters = @($expected.MasterServers | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object) -join ','
            return $actual.Name -ieq "$($expected.Name)" -and
                $actual.ZoneType -ieq "$($expected.ZoneType)" -and
                $actual.IsDsIntegrated -eq [bool]$expected.IsDsIntegrated -and
                $actual.IsSigned -eq [bool]$expected.IsSigned -and
                $actual.IsPaused -eq [bool]$expected.IsPaused -and
                $actual.IsAutoCreated -eq [bool]$expected.IsAutoCreated -and
                "$($actual.DynamicUpdate)" -ieq "$($expected.DynamicUpdate)" -and
                "$($actual.ReplicationScope)" -ieq "$($expected.ReplicationScope)" -and
                "$($actual.DirectoryPartitionName)" -ieq "$($expected.DirectoryPartitionName)" -and
                "$($actual.ZoneFile)" -ieq "$($expected.ZoneFile)" -and
                $actualMasters -eq $expectedMasters -and
                "$($actual.ForwarderTimeoutSeconds)" -eq "$($expected.ForwarderTimeoutSeconds)" -and
                "$($actual.UseRecursion)" -eq "$($expected.UseRecursion)"
        }

        function Test-RequestedZone([object]$zone) {
            $actual = Convert-Zone $zone
            if ($actual.ZoneType -ine $ZoneKind -or $actual.IsDsIntegrated -ne $IsDsIntegrated) { return $false }
            if ($ZoneKind -eq 'Primary' -and $actual.DynamicUpdate -ine $DynamicUpdate) { return $false }
            if ($IsDsIntegrated) {
                if ($ReplicationScope -eq 'Custom') {
                    if ($actual.DirectoryPartitionName -ine $DirectoryPartitionName) { return $false }
                } elseif ($actual.ReplicationScope -ine $ReplicationScope) { return $false }
            } elseif ($ZoneKind -ne 'Forwarder' -and $actual.ZoneFile -ine $ZoneFile) { return $false }
            if ($ZoneKind -in @('Secondary','Stub','Forwarder')) {
                $actualMasters = @($actual.MasterServers | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object) -join ','
                $requestedMasters = @($MasterServers | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object) -join ','
                if ($actualMasters -ne $requestedMasters) { return $false }
            }
            if ($ZoneKind -eq 'Forwarder' -and
                ($actual.ForwarderTimeoutSeconds -ne $ForwarderTimeoutSeconds -or
                 $actual.UseRecursion -ne $UseRecursion)) { return $false }
            return $true
        }

        function Add-StorageParameters([hashtable]$parameters) {
            if ($IsDsIntegrated) {
                if ($ReplicationScope -eq 'Custom') { $parameters.DirectoryPartitionName = $DirectoryPartitionName }
                else { $parameters.ReplicationScope = $ReplicationScope }
            } elseif ($ZoneKind -ne 'Forwarder') {
                $parameters.ZoneFile = $ZoneFile
            }
        }

        try {
            $existing = Get-LiveZone
            if ($MutationKind -eq 'Create') {
                if ($null -ne $existing) {
                    return [pscustomobject]@{ Success=$false; FailureKind='ZoneAlreadyExists'; Message='The DNS zone already exists.'; BeforeZoneJson=$null; AfterZoneJson=$null }
                }
                $parameters = @{ Name=$ZoneName; PassThru=$true; ErrorAction='Stop' }
                Add-StorageParameters $parameters
                switch ($ZoneKind) {
                    'Primary' {
                        $parameters.DynamicUpdate = $DynamicUpdate
                        Add-DnsServerPrimaryZone @parameters | Out-Null
                    }
                    'Secondary' {
                        $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                        Add-DnsServerSecondaryZone @parameters | Out-Null
                    }
                    'Stub' {
                        $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                        Add-DnsServerStubZone @parameters | Out-Null
                    }
                    'Forwarder' {
                        $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                        $parameters.ForwarderTimeout = $ForwarderTimeoutSeconds
                        $parameters.UseRecursion = $UseRecursion
                        Add-DnsServerConditionalForwarderZone @parameters | Out-Null
                    }
                }
                $after = Get-LiveZone
                if ($null -eq $after -or -not (Test-RequestedZone $after)) { throw 'Zone read-back verification failed.' }
                return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='The DNS zone was created.'; BeforeZoneJson=$null; AfterZoneJson=((Convert-Zone $after) | ConvertTo-Json -Compress -Depth 8) }
            }

            if ($null -eq $existing) {
                return [pscustomobject]@{ Success=$false; FailureKind='ZoneChanged'; Message='The DNS zone no longer exists.'; BeforeZoneJson=$null; AfterZoneJson=$null }
            }
            if (-not (Test-ExpectedZone $existing)) {
                return [pscustomobject]@{ Success=$false; FailureKind='ZoneChanged'; Message='The live DNS zone differs from the inventory snapshot.'; BeforeZoneJson=$null; AfterZoneJson=$null }
            }
            $beforeJson = (Convert-Zone $existing) | ConvertTo-Json -Compress -Depth 8
            if ($existing.IsAutoCreated -or $ZoneName -ieq 'TrustAnchors' -or $ZoneName -eq '.') {
                return [pscustomobject]@{ Success=$false; FailureKind='ProtectedZone'; Message='This system-managed DNS zone cannot be changed.'; BeforeZoneJson=$beforeJson; AfterZoneJson=$null }
            }
            if ($MutationKind -eq 'Delete') {
                if ($existing.IsSigned) {
                    return [pscustomobject]@{ Success=$false; FailureKind='SignedZone'; Message='Remove DNSSEC signing before deleting this zone.'; BeforeZoneJson=$beforeJson; AfterZoneJson=$null }
                }
                Remove-DnsServerZone -Name $ZoneName -Force -ErrorAction Stop
                if ($null -ne (Get-LiveZone)) { throw 'Zone deletion could not be verified.' }
                return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='The DNS zone was deleted.'; BeforeZoneJson=$beforeJson; AfterZoneJson=$null }
            }

            $parameters = @{ Name=$ZoneName; PassThru=$true; ErrorAction='Stop' }
            switch ($ZoneKind) {
                'Primary' {
                    $parameters.DynamicUpdate = $DynamicUpdate
                    Set-DnsServerPrimaryZone @parameters | Out-Null
                }
                'Secondary' {
                    $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                    Set-DnsServerSecondaryZone @parameters | Out-Null
                }
                'Stub' {
                    $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                    Set-DnsServerStubZone @parameters | Out-Null
                }
                'Forwarder' {
                    $parameters.MasterServers = [System.Net.IPAddress[]]@($MasterServers)
                    $parameters.ForwarderTimeout = $ForwarderTimeoutSeconds
                    $parameters.UseRecursion = $UseRecursion
                    Set-DnsServerConditionalForwarderZone @parameters | Out-Null
                }
            }
            $after = Get-LiveZone
            if ($null -eq $after -or -not (Test-RequestedZone $after)) { throw 'Zone read-back verification failed.' }
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='The DNS zone was updated.'; BeforeZoneJson=$beforeJson; AfterZoneJson=((Convert-Zone $after) | ConvertTo-Json -Compress -Depth 8) }
        } catch {
            return [pscustomobject]@{ Success=$false; FailureKind='DnsZoneMutationFailed'; Message='The DNS server rejected the zone operation.'; BeforeZoneJson=$null; AfterZoneJson=$null }
        }
        """;
}

internal static class DnsRemoteServerSettings
{
    // Fixed allowlisted implementation. Every request value is supplied as a bound parameter.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Read','Update','ClearCache')][string]$Action,
            [string[]]$ForwarderAddresses,
            [bool]$ForwarderUseRootHint,
            [ValidateRange(0,15)][int]$ForwarderTimeoutSeconds,
            [bool]$ForwarderEnableReordering,
            [bool]$RecursionEnabled,
            [ValidateRange(0,15)][int]$RecursionAdditionalTimeoutSeconds,
            [ValidateRange(1,15)][int]$RecursionRetryIntervalSeconds,
            [ValidateRange(1,15)][int]$RecursionTimeoutSeconds,
            [bool]$RecursionSecureResponse,
            [string]$ExpectedServerSettingsJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Get-Settings {
            $forwarder = Get-DnsServerForwarder -ErrorAction Stop
            $recursion = Get-DnsServerRecursion -ErrorAction Stop
            [ordered]@{
                ForwarderAddresses = @($forwarder.IPAddress | ForEach-Object {
                    if ($_ -is [System.Net.IPAddress]) { $_.IPAddressToString } else { "$_" }
                } | Sort-Object)
                ForwarderUseRootHint = [bool]$forwarder.UseRootHint
                ForwarderTimeoutSeconds = [int]$forwarder.Timeout
                ForwarderEnableReordering = [bool]$forwarder.EnableReordering
                RecursionEnabled = [bool]$recursion.Enable
                RecursionAdditionalTimeoutSeconds = [int]$recursion.AdditionalTimeout
                RecursionRetryIntervalSeconds = [int]$recursion.RetryInterval
                RecursionTimeoutSeconds = [int]$recursion.Timeout
                RecursionSecureResponse = [bool]$recursion.SecureResponse
            }
        }

        function Test-Settings([object]$actual, [object]$expected) {
            $actualForwarders = @($actual.ForwarderAddresses | ForEach-Object { "$($_)".ToLowerInvariant() } | Sort-Object) -join ','
            $expectedForwarders = @($expected.ForwarderAddresses | ForEach-Object { "$($_)".ToLowerInvariant() } | Sort-Object) -join ','
            return $actualForwarders -eq $expectedForwarders -and
                $actual.ForwarderUseRootHint -eq [bool]$expected.ForwarderUseRootHint -and
                $actual.ForwarderTimeoutSeconds -eq [int]$expected.ForwarderTimeoutSeconds -and
                $actual.ForwarderEnableReordering -eq [bool]$expected.ForwarderEnableReordering -and
                $actual.RecursionEnabled -eq [bool]$expected.RecursionEnabled -and
                $actual.RecursionAdditionalTimeoutSeconds -eq [int]$expected.RecursionAdditionalTimeoutSeconds -and
                $actual.RecursionRetryIntervalSeconds -eq [int]$expected.RecursionRetryIntervalSeconds -and
                $actual.RecursionTimeoutSeconds -eq [int]$expected.RecursionTimeoutSeconds -and
                $actual.RecursionSecureResponse -eq [bool]$expected.RecursionSecureResponse
        }

        function Set-Forwarders([object]$settings) {
            $addresses = @($settings.ForwarderAddresses | Sort-Object -Unique)
            $current = Get-Settings
            if ($addresses.Count -eq 0) {
                if ($current.ForwarderAddresses.Count -gt 0) {
                    Remove-DnsServerForwarder -IPAddress ([System.Net.IPAddress[]]@($current.ForwarderAddresses)) -Force -ErrorAction Stop | Out-Null
                }
                Set-DnsServerForwarder -UseRootHint ([bool]$settings.ForwarderUseRootHint) -Timeout ([int]$settings.ForwarderTimeoutSeconds) -EnableReordering ([bool]$settings.ForwarderEnableReordering) -ErrorAction Stop | Out-Null
            } else {
                Set-DnsServerForwarder -IPAddress ([System.Net.IPAddress[]]@($addresses)) -UseRootHint ([bool]$settings.ForwarderUseRootHint) -Timeout ([int]$settings.ForwarderTimeoutSeconds) -EnableReordering ([bool]$settings.ForwarderEnableReordering) -ErrorAction Stop | Out-Null
            }
        }

        try {
            if ($Action -eq 'ClearCache') {
                Clear-DnsServerCache -Force -ErrorAction Stop
                return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='The DNS server cache was cleared.'; BeforeJson=$null; AfterJson=$null }
            }

            $before = Get-Settings
            $beforeJson = $before | ConvertTo-Json -Compress -Depth 5
            if ($Action -eq 'Read') {
                return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS server settings read.'; BeforeJson=$null; AfterJson=$beforeJson }
            }

            $expected = $ExpectedServerSettingsJson | ConvertFrom-Json -ErrorAction Stop
            if (-not (Test-Settings $before $expected)) {
                return [pscustomobject]@{ Success=$false; FailureKind='ServerSettingsChanged'; Message='The live DNS server settings changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
            }

            $requestedForwarders = @($ForwarderAddresses | Sort-Object -Unique)
            $requested = [pscustomobject]@{
                ForwarderAddresses = $requestedForwarders
                ForwarderUseRootHint = $ForwarderUseRootHint
                ForwarderTimeoutSeconds = $ForwarderTimeoutSeconds
                ForwarderEnableReordering = $ForwarderEnableReordering
                RecursionEnabled = $RecursionEnabled
                RecursionAdditionalTimeoutSeconds = $RecursionAdditionalTimeoutSeconds
                RecursionRetryIntervalSeconds = $RecursionRetryIntervalSeconds
                RecursionTimeoutSeconds = $RecursionTimeoutSeconds
                RecursionSecureResponse = $RecursionSecureResponse
            }
            Set-Forwarders $requested
            Set-DnsServerRecursion -Enable $RecursionEnabled -AdditionalTimeout $RecursionAdditionalTimeoutSeconds -RetryInterval $RecursionRetryIntervalSeconds -Timeout $RecursionTimeoutSeconds -SecureResponse $RecursionSecureResponse -ErrorAction Stop | Out-Null

            $after = Get-Settings
            if (-not (Test-Settings $after $requested)) { throw 'Server settings read-back verification failed.' }
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS server settings updated.'; BeforeJson=$beforeJson; AfterJson=($after | ConvertTo-Json -Compress -Depth 5) }
        } catch {
            $afterJson = $null
            if ($Action -eq 'Update' -and $null -ne $before) {
                try {
                    Set-Forwarders $before
                    Set-DnsServerRecursion -Enable $before.RecursionEnabled -AdditionalTimeout $before.RecursionAdditionalTimeoutSeconds -RetryInterval $before.RecursionRetryIntervalSeconds -Timeout $before.RecursionTimeoutSeconds -SecureResponse $before.RecursionSecureResponse -ErrorAction Stop | Out-Null
                    $afterJson = (Get-Settings) | ConvertTo-Json -Compress -Depth 5
                } catch { $afterJson = $null }
            }
            return [pscustomobject]@{ Success=$false; FailureKind='DnsServerSettingsOperationFailed'; Message='The DNS server rejected the server settings operation. Any partial change was rolled back where possible.'; BeforeJson=$beforeJson; AfterJson=$afterJson }
        }
        """;
}

internal static class DnsRemotePolicyConfiguration
{
    // Fixed cmdlet allowlist. The browser can supply only the validated JSON DTO bound below.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Read','SaveClientSubnet','DeleteClientSubnet','CreateZoneScope','DeleteZoneScope','SaveQueryPolicy','DeleteQueryPolicy','SetQueryPolicyEnabled')][string]$Action,
            [string]$MutationJson,
            [string]$ExpectedConfigurationJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Get-Configuration {
            $subnets = @(Get-DnsServerClientSubnet -ErrorAction Stop | ForEach-Object {
                [ordered]@{ Name="$($_.Name)"; Ipv4Subnets=@($_.IPv4Subnet | ForEach-Object { "$_" } | Sort-Object); Ipv6Subnets=@($_.IPv6Subnet | ForEach-Object { "$_" } | Sort-Object) }
            } | Sort-Object Name)
            $scopes = [System.Collections.Generic.List[object]]::new()
            $policies = [System.Collections.Generic.List[object]]::new()
            foreach ($policy in @(Get-DnsServerQueryResolutionPolicy -ErrorAction Stop)) {
                $policies.Add((Convert-Policy $policy 'Server' $null))
            }
            foreach ($zone in @(Get-DnsServerZone -ErrorAction Stop | Sort-Object ZoneName)) {
                $zoneName = "$($zone.ZoneName)"
                foreach ($scope in @(Get-DnsServerZoneScope -ZoneName $zoneName -ErrorAction Stop)) {
                    $scopeName = if ($scope.ZoneScope) { "$($scope.ZoneScope)" } else { "$($scope.Name)" }
                    if ($scopeName -and $scopeName -ine $zoneName) { $scopes.Add([ordered]@{ ZoneName=$zoneName; Name=$scopeName }) }
                }
                foreach ($policy in @(Get-DnsServerQueryResolutionPolicy -ZoneName $zoneName -ErrorAction Stop)) {
                    $policies.Add((Convert-Policy $policy 'Zone' $zoneName))
                }
            }
            [ordered]@{ ClientSubnets=$subnets; ZoneScopes=@($scopes | Sort-Object ZoneName,Name); QueryPolicies=@($policies | Sort-Object Level,ZoneName,ProcessingOrder,Name) }
        }
        function Convert-Policy([object]$policy, [string]$level, [string]$zoneName) {
            $criteria = @{}
            foreach ($entry in @($policy.Criteria)) {
                if ($entry.CriteriaType) { $criteria["$($entry.CriteriaType)"] = "$($entry.Criteria)" }
            }
            $zoneScope = @($policy.Content | ForEach-Object { "$($_.ScopeName),$($_.Weight)" }) -join ';'
            [ordered]@{
                Name="$($policy.Name)"; Level=$level; ZoneName=$zoneName; Action="$($policy.Action)"; Condition="$($policy.Condition)"
                ProcessingOrder=[int]$policy.ProcessingOrder; Enabled=[bool]$policy.IsEnabled
                ClientSubnet=if ($criteria.ClientSubnet) { "$($criteria.ClientSubnet)" } else { $null }
                Fqdn=if ($criteria.Fqdn) { "$($criteria.Fqdn)" } else { $null }
                QueryType=if ($criteria.Qtype) { "$($criteria.Qtype)" } else { $null }
                TransportProtocol=if ($criteria.TransportProtocol) { "$($criteria.TransportProtocol)" } else { $null }
                InternetProtocol=if ($criteria.NetworkProtocol) { "$($criteria.NetworkProtocol)" } else { $null }
                ServerInterfaceIp=if ($criteria.Interface) { "$($criteria.Interface)" } else { $null }
                ZoneScope=if ($zoneScope) { $zoneScope } else { $null }
            }
        }
        function Normalize-Configuration([object]$configuration) {
            $subnets = @($configuration.ClientSubnets | ForEach-Object {
                [ordered]@{ Name="$($_.Name)"; Ipv4Subnets=@($_.Ipv4Subnets | ForEach-Object { "$_" } | Sort-Object); Ipv6Subnets=@($_.Ipv6Subnets | ForEach-Object { "$_" } | Sort-Object) }
            } | Sort-Object Name)
            $scopes = @($configuration.ZoneScopes | ForEach-Object { [ordered]@{ ZoneName="$($_.ZoneName)"; Name="$($_.Name)" } } | Sort-Object ZoneName,Name)
            $policies = @($configuration.QueryPolicies | ForEach-Object {
                [ordered]@{
                    Name="$($_.Name)"; Level="$($_.Level)"; ZoneName=if ($_.ZoneName) { "$($_.ZoneName)" } else { $null }
                    Action="$($_.Action)"; Condition="$($_.Condition)"; ProcessingOrder=[int]$_.ProcessingOrder; Enabled=[bool]$_.Enabled
                    ClientSubnet=if ($_.ClientSubnet) { "$($_.ClientSubnet)" } else { $null }; Fqdn=if ($_.Fqdn) { "$($_.Fqdn)" } else { $null }
                    QueryType=if ($_.QueryType) { "$($_.QueryType)" } else { $null }; TransportProtocol=if ($_.TransportProtocol) { "$($_.TransportProtocol)" } else { $null }
                    InternetProtocol=if ($_.InternetProtocol) { "$($_.InternetProtocol)" } else { $null }; ServerInterfaceIp=if ($_.ServerInterfaceIp) { "$($_.ServerInterfaceIp)" } else { $null }
                    ZoneScope=if ($_.ZoneScope) { "$($_.ZoneScope)" } else { $null }
                }
            } | Sort-Object Level,ZoneName,ProcessingOrder,Name)
            [ordered]@{ ClientSubnets=$subnets; ZoneScopes=$scopes; QueryPolicies=$policies }
        }
        function Criterion([object]$criterion) {
            if ($null -eq $criterion -or @($criterion.Values).Count -eq 0) { return $null }
            $operator = if ("$($criterion.Operator)" -eq 'Ne') { 'NE' } else { 'EQ' }
            return ($operator + ',' + (@($criterion.Values) -join ','))
        }
        function Policy-Parameters([object]$m, [bool]$isCreate) {
            $p = @{ Name="$($m.Name)"; Condition=("$($m.Condition)".ToUpperInvariant()); ProcessingOrder=[int]$m.ProcessingOrder; ErrorAction='Stop' }
            if ($isCreate) { $p.Action=("$($m.Decision)".ToUpperInvariant()) }
            if ("$($m.Level)" -eq 'Zone') { $p.ZoneName="$($m.ZoneName)" }
            $criteria = @{ ClientSubnet=(Criterion $m.ClientSubnet); FQDN=(Criterion $m.Fqdn); QType=(Criterion $m.QueryType); TransportProtocol=(Criterion $m.TransportProtocol); InternetProtocol=(Criterion $m.InternetProtocol); ServerInterfaceIP=(Criterion $m.ServerInterfaceIp) }
            foreach ($entry in $criteria.GetEnumerator()) { if ($entry.Value) { $p[$entry.Key]=$entry.Value } }
            if (@($m.ZoneScopes).Count -gt 0) { $p.ZoneScope=(@($m.ZoneScopes | ForEach-Object { "$($_.Name),$($_.Weight)" }) -join ';') }
            return $p
        }

        try {
            $before = Get-Configuration
            if (@($before.ClientSubnets).Count -gt 500 -or @($before.ZoneScopes).Count -gt 1000 -or @($before.QueryPolicies).Count -gt 1000) {
                return [pscustomobject]@{ Success=$false; FailureKind='PolicyConfigurationTooLarge'; Message='The DNS policy configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            $beforeJson = $before | ConvertTo-Json -Compress -Depth 10
            if ($Action -eq 'Read') { return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS policy configuration read.'; BeforeJson=$null; AfterJson=$beforeJson } }
            $expected = $ExpectedConfigurationJson | ConvertFrom-Json -ErrorAction Stop
            $liveCanonical = (Normalize-Configuration $before) | ConvertTo-Json -Compress -Depth 10
            $expectedCanonical = (Normalize-Configuration $expected) | ConvertTo-Json -Compress -Depth 10
            if ($liveCanonical -cne $expectedCanonical) { return [pscustomobject]@{ Success=$false; FailureKind='PolicyConfigurationChanged'; Message='The live DNS policy configuration changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null } }
            $m = $MutationJson | ConvertFrom-Json -ErrorAction Stop
            switch ($Action) {
                'SaveClientSubnet' {
                    $existing = Get-DnsServerClientSubnet -Name $m.Name -ErrorAction SilentlyContinue
                    if ($existing) {
                        if (@($m.Ipv4Subnets).Count -gt 0) { Set-DnsServerClientSubnet -Name $m.Name -Action REPLACE -IPv4Subnet ([string[]]@($m.Ipv4Subnets)) -ErrorAction Stop | Out-Null }
                        elseif (@($existing.IPv4Subnet).Count -gt 0) { Set-DnsServerClientSubnet -Name $m.Name -Action REMOVE -IPv4Subnet ([string[]]@($existing.IPv4Subnet)) -ErrorAction Stop | Out-Null }
                        if (@($m.Ipv6Subnets).Count -gt 0) { Set-DnsServerClientSubnet -Name $m.Name -Action REPLACE -IPv6Subnet ([string[]]@($m.Ipv6Subnets)) -ErrorAction Stop | Out-Null }
                        elseif (@($existing.IPv6Subnet).Count -gt 0) { Set-DnsServerClientSubnet -Name $m.Name -Action REMOVE -IPv6Subnet ([string[]]@($existing.IPv6Subnet)) -ErrorAction Stop | Out-Null }
                    } else {
                        $p = @{ Name="$($m.Name)"; ErrorAction='Stop' }
                        if (@($m.Ipv4Subnets).Count -gt 0) { $p.IPv4Subnet=[string[]]@($m.Ipv4Subnets) }
                        if (@($m.Ipv6Subnets).Count -gt 0) { $p.IPv6Subnet=[string[]]@($m.Ipv6Subnets) }
                        Add-DnsServerClientSubnet @p | Out-Null
                    }
                }
                'DeleteClientSubnet' { Remove-DnsServerClientSubnet -Name $m.Name -Force -ErrorAction Stop | Out-Null }
                'CreateZoneScope' { Add-DnsServerZoneScope -ZoneName $m.ZoneName -Name $m.Name -ErrorAction Stop | Out-Null }
                'DeleteZoneScope' {
                    $records = @(Get-DnsServerResourceRecord -ZoneName $m.ZoneName -ZoneScope $m.Name -ErrorAction Stop)
                    if ($records.Count -gt 0) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneScopeNotEmpty'; Message='Delete the records in this zone scope first.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    Remove-DnsServerZoneScope -ZoneName $m.ZoneName -Name $m.Name -Force -ErrorAction Stop | Out-Null
                }
                'SaveQueryPolicy' {
                    $zoneArgs = @{}; if ("$($m.Level)" -eq 'Zone') { $zoneArgs.ZoneName="$($m.ZoneName)" }
                    $existing = Get-DnsServerQueryResolutionPolicy -Name $m.Name @zoneArgs -ErrorAction SilentlyContinue
                    if ($existing -and "$($existing.Action)" -ine "$($m.Decision)") { return [pscustomobject]@{ Success=$false; FailureKind='PolicyActionImmutable'; Message='Delete and recreate the policy to change its action.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    if ($existing) {
                        $existingView = Convert-Policy $existing "$($m.Level)" "$($m.ZoneName)"
                        if (($existingView.ClientSubnet -and $null -eq $m.ClientSubnet) -or ($existingView.Fqdn -and $null -eq $m.Fqdn) -or
                            ($existingView.QueryType -and $null -eq $m.QueryType) -or ($existingView.TransportProtocol -and $null -eq $m.TransportProtocol) -or
                            ($existingView.InternetProtocol -and $null -eq $m.InternetProtocol) -or ($existingView.ServerInterfaceIp -and $null -eq $m.ServerInterfaceIp) -or
                            ($existingView.ZoneScope -and @($m.ZoneScopes).Count -eq 0)) {
                            return [pscustomobject]@{ Success=$false; FailureKind='PolicyCriteriaRemovalRequiresRecreate'; Message='Delete and recreate the policy to remove an existing criterion.'; BeforeJson=$beforeJson; AfterJson=$null }
                        }
                    }
                    $p = Policy-Parameters $m ([bool]($null -eq $existing))
                    if ($existing) { Set-DnsServerQueryResolutionPolicy @p | Out-Null } else { Add-DnsServerQueryResolutionPolicy @p | Out-Null }
                    if (-not [bool]$m.Enabled) { Disable-DnsServerPolicy -Name $m.Name -Level $m.Level @zoneArgs -Force -ErrorAction Stop | Out-Null }
                }
                'DeleteQueryPolicy' {
                    $p=@{ Name="$($m.Name)"; ErrorAction='Stop' }; if ("$($m.Level)" -eq 'Zone') { $p.ZoneName="$($m.ZoneName)" }
                    Remove-DnsServerQueryResolutionPolicy @p -Force | Out-Null
                }
                'SetQueryPolicyEnabled' {
                    $p=@{ Name="$($m.Name)"; Level="$($m.Level)"; Force=$true; ErrorAction='Stop' }; if ("$($m.Level)" -eq 'Zone') { $p.ZoneName="$($m.ZoneName)" }
                    if ([bool]$m.Enabled) { Enable-DnsServerPolicy @p | Out-Null } else { Disable-DnsServerPolicy @p | Out-Null }
                }
            }
            $after = Get-Configuration
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS policy configuration updated.'; BeforeJson=$beforeJson; AfterJson=($after | ConvertTo-Json -Compress -Depth 10) }
        } catch {
            return [pscustomobject]@{ Success=$false; FailureKind='DnsPolicyOperationFailed'; Message='The DNS server rejected the policy operation.'; BeforeJson=$beforeJson; AfterJson=$null }
        }
        """;
}

internal static class DnsRemoteScavengingConfiguration
{
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Read','UpdateServer','UpdateZone','StartScavenging')][string]$Action,
            [Nullable[bool]]$ScavengingState,
            [Nullable[int]]$ScavengingIntervalHours,
            [string]$ZoneName,
            [Nullable[bool]]$ZoneAgingEnabled,
            [Nullable[int]]$ZoneNoRefreshIntervalHours,
            [Nullable[int]]$ZoneRefreshIntervalHours,
            [string[]]$ZoneScavengeServers,
            [string]$ExpectedConfigurationJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Seconds([object]$value) {
            if ($null -eq $value) { return 0L }
            if ($value -is [TimeSpan]) { return [long][Math]::Round($value.TotalSeconds) }
            try { return [long]$value } catch { return 0L }
        }
        function Utc-OrNull([object]$value) {
            if ($value -is [DateTime]) { return $value.ToUniversalTime().ToString('O') }
            return $null
        }
        function Convert-ZoneAging([object]$zone) {
            $name = "$($zone.ZoneName)"
            $eligible = "$($zone.ZoneType)" -ieq 'Primary' -and -not [bool]$zone.IsAutoCreated -and $name -ne '.' -and $name -ine 'TrustAnchors'
            $reason = if ("$($zone.ZoneType)" -ine 'Primary') { 'OnlyPrimaryZonesSupported' } elseif (-not $eligible) { 'BuiltInZoneNotSupported' } else { $null }
            $aging = $null
            if ($eligible) { $aging = Get-DnsServerZoneAging -Name $name -ErrorAction Stop }
            [ordered]@{
                Name=$name; ZoneType="$($zone.ZoneType)"
                AgingEnabled=if ($null -ne $aging) { [bool]$aging.AgingEnabled } else { $false }
                IsEligible=$eligible; IneligibilityReason=$reason
                NoRefreshIntervalSeconds=if ($null -ne $aging) { Seconds $aging.NoRefreshInterval } else { 0L }
                RefreshIntervalSeconds=if ($null -ne $aging) { Seconds $aging.RefreshInterval } else { 0L }
                AvailableForScavengeTime=if ($null -ne $aging) { Utc-OrNull $aging.AvailForScavengeTime } else { $null }
                ScavengeServers=if ($null -ne $aging) { @($aging.ScavengeServers | ForEach-Object { "$_" } | Sort-Object) } else { @() }
            }
        }
        function Get-Configuration {
            $server = Get-DnsServerScavenging -ErrorAction Stop
            $zones = @(Get-DnsServerZone -ErrorAction Stop | ForEach-Object { Convert-ZoneAging $_ } | Sort-Object Name)
            [ordered]@{
                ScavengingEnabled=[bool]$server.ScavengingState
                ScavengingIntervalSeconds=(Seconds $server.ScavengingInterval)
                DefaultNoRefreshIntervalSeconds=(Seconds $server.DefaultNoRefreshInterval)
                DefaultRefreshIntervalSeconds=(Seconds $server.DefaultRefreshInterval)
                LastScavengeTime=(Utc-OrNull $server.LastScavengeTime)
                Zones=$zones
            }
        }
        function Normalize-Server([object]$value) {
            [ordered]@{
                ScavengingEnabled=[bool]$value.ScavengingEnabled
                ScavengingIntervalSeconds=[long]$value.ScavengingIntervalSeconds
                DefaultNoRefreshIntervalSeconds=[long]$value.DefaultNoRefreshIntervalSeconds
                DefaultRefreshIntervalSeconds=[long]$value.DefaultRefreshIntervalSeconds
            }
        }
        function Normalize-Zone([object]$value) {
            [ordered]@{
                Name="$($value.Name)".ToLowerInvariant(); ZoneType="$($value.ZoneType)".ToLowerInvariant()
                AgingEnabled=[bool]$value.AgingEnabled; IsEligible=[bool]$value.IsEligible
                IneligibilityReason=if ($value.IneligibilityReason) { "$($value.IneligibilityReason)" } else { $null }
                NoRefreshIntervalSeconds=[long]$value.NoRefreshIntervalSeconds
                RefreshIntervalSeconds=[long]$value.RefreshIntervalSeconds
                ScavengeServers=@($value.ScavengeServers | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object)
            }
        }

        $beforeJson = $null
        try {
            $before = Get-Configuration
            if (@($before.Zones).Count -gt 500) {
                return [pscustomobject]@{ Success=$false; FailureKind='ScavengingConfigurationTooLarge'; Message='The scavenging configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            $beforeJson = $before | ConvertTo-Json -Compress -Depth 8
            if ($beforeJson.Length -gt 262144) {
                return [pscustomobject]@{ Success=$false; FailureKind='ScavengingConfigurationTooLarge'; Message='The scavenging configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            if ($Action -eq 'Read') { return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS aging and scavenging configuration read.'; BeforeJson=$null; AfterJson=$beforeJson } }

            $expected = $ExpectedConfigurationJson | ConvertFrom-Json -ErrorAction Stop
            if ($Action -in @('UpdateServer','StartScavenging')) {
                if (((Normalize-Server $before) | ConvertTo-Json -Compress) -cne ((Normalize-Server $expected) | ConvertTo-Json -Compress)) {
                    return [pscustomobject]@{ Success=$false; FailureKind='ScavengingConfigurationChanged'; Message='The live server scavenging state changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
                }
            }
            if ($Action -eq 'UpdateZone') {
                $target = @($before.Zones | Where-Object { $_.Name -ieq $ZoneName })
                $expectedTarget = @($expected.Zones | Where-Object { $_.Name -ieq $ZoneName })
                if ($target.Count -ne 1) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotFound'; Message='The DNS zone was not found.'; BeforeJson=$beforeJson; AfterJson=$null } }
                if (-not [bool]$target[0].IsEligible) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotEligible'; Message='Only non-built-in primary zones support aging.'; BeforeJson=$beforeJson; AfterJson=$null } }
                if ($expectedTarget.Count -ne 1 -or ((Normalize-Zone $target[0]) | ConvertTo-Json -Compress -Depth 5) -cne ((Normalize-Zone $expectedTarget[0]) | ConvertTo-Json -Compress -Depth 5)) {
                    return [pscustomobject]@{ Success=$false; FailureKind='ScavengingConfigurationChanged'; Message='The live zone aging state changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
                }
            }

            switch ($Action) {
                'UpdateServer' {
                    Set-DnsServerScavenging -ScavengingState ([bool]$ScavengingState) -ScavengingInterval ([TimeSpan]::FromHours([int]$ScavengingIntervalHours)) -Confirm:$false -ErrorAction Stop | Out-Null
                }
                'UpdateZone' {
                    Set-DnsServerZoneAging -Name $ZoneName -Aging ([bool]$ZoneAgingEnabled) -NoRefreshInterval ([TimeSpan]::FromHours([int]$ZoneNoRefreshIntervalHours)) -RefreshInterval ([TimeSpan]::FromHours([int]$ZoneRefreshIntervalHours)) -ScavengeServers ([System.Net.IPAddress[]]@($ZoneScavengeServers)) -Confirm:$false -ErrorAction Stop | Out-Null
                }
                'StartScavenging' {
                    if (-not [bool]$before.ScavengingEnabled -or @($before.Zones | Where-Object { $_.IsEligible -and $_.AgingEnabled }).Count -eq 0) {
                        return [pscustomobject]@{ Success=$false; FailureKind='ScavengingNotEnabled'; Message='Enable server scavenging and aging on at least one primary zone first.'; BeforeJson=$beforeJson; AfterJson=$null }
                    }
                    Start-DnsServerScavenging -Force -ErrorAction Stop | Out-Null
                }
            }
            $after = Get-Configuration
            if ($Action -eq 'UpdateServer' -and ([bool]$after.ScavengingEnabled -ne [bool]$ScavengingState -or [long]$after.ScavengingIntervalSeconds -ne ([long]$ScavengingIntervalHours * 3600L))) { throw 'Server scavenging read-back verification failed.' }
            if ($Action -eq 'UpdateZone') {
                $afterTarget = @($after.Zones | Where-Object { $_.Name -ieq $ZoneName })[0]
                if ([bool]$afterTarget.AgingEnabled -ne [bool]$ZoneAgingEnabled -or [long]$afterTarget.NoRefreshIntervalSeconds -ne ([long]$ZoneNoRefreshIntervalHours * 3600L) -or [long]$afterTarget.RefreshIntervalSeconds -ne ([long]$ZoneRefreshIntervalHours * 3600L)) { throw 'Zone aging read-back verification failed.' }
            }
            $message = if ($Action -eq 'StartScavenging') { 'DNS scavenging was started.' } else { 'DNS aging and scavenging configuration updated.' }
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message=$message; BeforeJson=$beforeJson; AfterJson=($after | ConvertTo-Json -Compress -Depth 8) }
        } catch {
            return [pscustomobject]@{ Success=$false; FailureKind='DnsScavengingOperationFailed'; Message='The DNS server rejected the aging or scavenging operation.'; BeforeJson=$beforeJson; AfterJson=$null }
        }
        """;
}

internal static class DnsRemoteNetworkConfiguration
{
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Read','UpdateListeningAddresses','AddRootHint','UpdateRootHint','RemoveRootHint')][string]$Action,
            [string[]]$ListeningIpAddresses,
            [string]$RootHintNameServer,
            [string[]]$RootHintIpAddresses,
            [string]$OriginalRootHintNameServer,
            [string]$ExpectedConfigurationJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Canonical-Ip([object]$value) {
            try { return ([System.Net.IPAddress]::Parse("$value")).ToString().ToLowerInvariant() } catch { return $null }
        }
        function Canonical-Name([object]$value) {
            $name = "$value".Trim().TrimEnd('.').ToLowerInvariant()
            if ($name) { return "$name." }
            return ''
        }
        function Convert-RootHint([object]$hint) {
            $addresses = @($hint.IPAddress | ForEach-Object {
                $candidate = if ($_.RecordData.IPv4Address) { $_.RecordData.IPv4Address } elseif ($_.RecordData.IPv6Address) { $_.RecordData.IPv6Address } else { $_.RecordData }
                Canonical-Ip $candidate
            } | Where-Object { $_ } | Sort-Object -Unique)
            [ordered]@{ NameServer=(Canonical-Name $hint.NameServer.RecordData.NameServer); IpAddresses=$addresses }
        }
        function Get-Configuration {
            $settings = Get-DnsServerSetting -All -ErrorAction Stop
            $listening = @($settings.ListeningIPAddress | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique)
            $available = @($settings.AllIPAddress | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique)
            if ($available.Count -eq 0) { $available = $listening }
            $hints = @(Get-DnsServerRootHint -ErrorAction Stop | ForEach-Object { Convert-RootHint $_ } | Sort-Object NameServer)
            [ordered]@{ ListeningIpAddresses=$listening; AvailableIpAddresses=$available; RootHints=$hints }
        }
        function Normalize([object]$value) {
            [ordered]@{
                ListeningIpAddresses=@($value.ListeningIpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique)
                AvailableIpAddresses=@($value.AvailableIpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique)
                RootHints=@($value.RootHints | ForEach-Object {
                    [ordered]@{ NameServer=(Canonical-Name $_.NameServer); IpAddresses=@($_.IpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique) }
                } | Sort-Object NameServer)
            }
        }
        function Same-Strings([object[]]$left, [object[]]$right) {
            return ((@($left | Sort-Object -Unique) -join '|') -ceq (@($right | Sort-Object -Unique) -join '|'))
        }

        $beforeJson = $null
        try {
            $before = Get-Configuration
            if (@($before.RootHints).Count -gt 64) {
                return [pscustomobject]@{ Success=$false; FailureKind='NetworkConfigurationTooLarge'; Message='The DNS network configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            $beforeJson = $before | ConvertTo-Json -Compress -Depth 7
            if ($beforeJson.Length -gt 131072) {
                return [pscustomobject]@{ Success=$false; FailureKind='NetworkConfigurationTooLarge'; Message='The DNS network configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            if ($Action -eq 'Read') { return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS network configuration read.'; BeforeJson=$null; AfterJson=$beforeJson } }

            $expected = $ExpectedConfigurationJson | ConvertFrom-Json -ErrorAction Stop
            if (((Normalize $before) | ConvertTo-Json -Compress -Depth 7) -cne ((Normalize $expected) | ConvertTo-Json -Compress -Depth 7)) {
                return [pscustomobject]@{ Success=$false; FailureKind='NetworkConfigurationChanged'; Message='The live DNS network configuration changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
            }

            switch ($Action) {
                'UpdateListeningAddresses' {
                    $requested = @($ListeningIpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ } | Sort-Object -Unique)
                    if ($requested.Count -eq 0 -or @($requested | Where-Object { $_ -notin $before.AvailableIpAddresses }).Count -gt 0) {
                        return [pscustomobject]@{ Success=$false; FailureKind='ListeningAddressUnavailable'; Message='A selected listening address is not available on the DNS server.'; BeforeJson=$beforeJson; AfterJson=$null }
                    }
                    $settings = Get-DnsServerSetting -All -ErrorAction Stop
                    $settings.ListeningIPAddress = [string[]]$requested
                    Set-DnsServerSetting -InputObject $settings -Force -ErrorAction Stop | Out-Null
                }
                'AddRootHint' {
                    $name = Canonical-Name $RootHintNameServer
                    if (@($before.RootHints | Where-Object { $_.NameServer -ceq $name }).Count -gt 0) {
                        return [pscustomobject]@{ Success=$false; FailureKind='RootHintAlreadyExists'; Message='The root hint already exists.'; BeforeJson=$beforeJson; AfterJson=$null }
                    }
                    Add-DnsServerRootHint -NameServer $name -IPAddress ([System.Net.IPAddress[]]@($RootHintIpAddresses)) -ErrorAction Stop | Out-Null
                }
                'UpdateRootHint' {
                    $oldName = Canonical-Name $OriginalRootHintNameServer
                    $newName = Canonical-Name $RootHintNameServer
                    $old = @($before.RootHints | Where-Object { $_.NameServer -ceq $oldName })
                    if ($old.Count -ne 1) { return [pscustomobject]@{ Success=$false; FailureKind='RootHintNotFound'; Message='The root hint was not found.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    if ($newName -cne $oldName -and @($before.RootHints | Where-Object { $_.NameServer -ceq $newName }).Count -gt 0) {
                        return [pscustomobject]@{ Success=$false; FailureKind='RootHintAlreadyExists'; Message='The replacement root hint already exists.'; BeforeJson=$beforeJson; AfterJson=$null }
                    }
                    Remove-DnsServerRootHint -NameServer $oldName -Force -ErrorAction Stop | Out-Null
                    try { Add-DnsServerRootHint -NameServer $newName -IPAddress ([System.Net.IPAddress[]]@($RootHintIpAddresses)) -ErrorAction Stop | Out-Null }
                    catch {
                        try { Add-DnsServerRootHint -NameServer $oldName -IPAddress ([System.Net.IPAddress[]]@($old[0].IpAddresses)) -ErrorAction Stop | Out-Null } catch {}
                        throw
                    }
                }
                'RemoveRootHint' {
                    $name = Canonical-Name $RootHintNameServer
                    if (@($before.RootHints).Count -le 1) { return [pscustomobject]@{ Success=$false; FailureKind='LastRootHintCannotBeRemoved'; Message='The final root hint cannot be removed.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    if (@($before.RootHints | Where-Object { $_.NameServer -ceq $name }).Count -ne 1) { return [pscustomobject]@{ Success=$false; FailureKind='RootHintNotFound'; Message='The root hint was not found.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    Remove-DnsServerRootHint -NameServer $name -Force -ErrorAction Stop | Out-Null
                }
            }

            $after = Get-Configuration
            if ($Action -eq 'UpdateListeningAddresses') {
                $wanted = @($ListeningIpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ })
                if (-not (Same-Strings $after.ListeningIpAddresses $wanted)) { throw 'DNS listening address read-back verification failed.' }
            }
            if ($Action -in @('AddRootHint','UpdateRootHint')) {
                $name = Canonical-Name $RootHintNameServer
                $actual = @($after.RootHints | Where-Object { $_.NameServer -ceq $name })
                $wanted = @($RootHintIpAddresses | ForEach-Object { Canonical-Ip $_ } | Where-Object { $_ })
                if ($actual.Count -ne 1 -or -not (Same-Strings $actual[0].IpAddresses $wanted)) { throw 'DNS root hint read-back verification failed.' }
            }
            if ($Action -eq 'RemoveRootHint' -and @($after.RootHints | Where-Object { $_.NameServer -ceq (Canonical-Name $RootHintNameServer) }).Count -gt 0) { throw 'DNS root hint removal verification failed.' }
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNS network configuration updated.'; BeforeJson=$beforeJson; AfterJson=($after | ConvertTo-Json -Compress -Depth 7) }
        } catch {
            return [pscustomobject]@{ Success=$false; FailureKind='DnsNetworkOperationFailed'; Message='The DNS server rejected the network configuration operation.'; BeforeJson=$beforeJson; AfterJson=$null }
        }
        """;
}

internal static class DnsRemoteDnssecConfiguration
{
    // Authoritative signing and recursive validation are separate concerns, but share one live,
    // concurrency-protected snapshot. Every value is parameter-bound and every cmdlet is fixed.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Read','SignWithDefaults','Resign','Unsign','RolloverKeys','SetValidationEnabled','RetrieveRootTrustAnchor','AddDsTrustAnchor','AddDnsKeyTrustAnchor','RemoveTrustAnchorType')][string]$Action,
            [string]$ZoneName,
            [Guid[]]$KeyIds,
            [Nullable[bool]]$ValidationEnabled,
            [string]$TrustPointName,
            [ValidateSet('','DnsKey','Ds')][string]$TrustAnchorType,
            [ValidateSet('','RsaSha1','RsaSha256','RsaSha512','RsaSha1NSec3','ECDsaP256Sha256','ECDsaP384Sha384')][string]$CryptoAlgorithm,
            [Nullable[int]]$KeyTag,
            [ValidateSet('','Sha1','Sha256','Sha384')][string]$DigestType,
            [string]$Digest,
            [string]$Base64Data,
            [string]$ExpectedConfigurationJson
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        function Seconds([object]$value) {
            if ($null -eq $value) { return $null }
            if ($value -is [TimeSpan]) { return [long][Math]::Round($value.TotalSeconds) }
            try { return [long]$value } catch { return $null }
        }
        function Text-OrNull([object]$value) {
            if ($null -eq $value -or "$value" -eq '') { return $null }
            return "$value"
        }
        function Get-KeyType([object]$key) {
            if ($null -ne $key.IsKeySigningKey) { return $(if ([bool]$key.IsKeySigningKey) { 'KeySigningKey' } else { 'ZoneSigningKey' }) }
            if ($key.KeyType) { return "$($key.KeyType)" }
            return 'Unknown'
        }
        function Convert-Key([object]$key) {
            $nextTime = $null
            if ($key.NextRolloverTime -is [DateTime]) { $nextTime = $key.NextRolloverTime.ToUniversalTime().ToString('O') }
            [ordered]@{
                KeyId=[Guid]$key.KeyId; KeyType=(Get-KeyType $key); CryptoAlgorithm=(Text-OrNull $key.CryptoAlgorithm)
                KeyLength=if ($null -ne $key.KeyLength) { [int]$key.KeyLength } else { $null }
                KeyStatus=(Text-OrNull $key.KeyStatus); KeyStorageProvider=(Text-OrNull $key.KeyStorageProvider)
                IsRolloverEnabled=if ($null -ne $key.IsRolloverEnabled) { [bool]$key.IsRolloverEnabled } else { $null }
                RolloverPeriodSeconds=(Seconds $key.RolloverPeriod); NextRolloverAction=(Text-OrNull $key.NextRolloverAction)
                NextRolloverTime=$nextTime
            }
        }
        function Convert-Zone([object]$zone) {
            $name = "$($zone.ZoneName)"
            $isPrimary = "$($zone.ZoneType)" -ieq 'Primary'
            $isBuiltIn = [bool]$zone.IsAutoCreated -or $name -eq '.' -or $name -ieq 'TrustAnchors'
            $eligible = $isPrimary -and -not $isBuiltIn
            $reason = if (-not $isPrimary) { 'OnlyPrimaryZonesSupported' } elseif ($isBuiltIn) { 'BuiltInZoneNotSupported' } else { $null }
            $settings = $null
            $keys = @()
            if ([bool]$zone.IsSigned) {
                $settings = Get-DnsServerDnsSecZoneSetting -ZoneName $name -ErrorAction Stop
                $keys = @(Get-DnsServerSigningKey -ZoneName $name -ErrorAction Stop | ForEach-Object { Convert-Key $_ } | Sort-Object KeyType,KeyId)
            }
            [ordered]@{
                Name=$name; ZoneType="$($zone.ZoneType)"; IsDsIntegrated=[bool]$zone.IsDsIntegrated
                IsAutoCreated=[bool]$zone.IsAutoCreated; IsSigned=[bool]$zone.IsSigned
                IsEligibleForSigning=$eligible; IneligibilityReason=$reason
                IsKeyMasterServer=if ($null -ne $settings.IsKeyMasterServer) { [bool]$settings.IsKeyMasterServer } else { $null }
                KeyMasterServer=(Text-OrNull $settings.KeyMasterServer); KeyMasterStatus=(Text-OrNull $settings.KeyMasterStatus)
                DenialOfExistence=(Text-OrNull $settings.DenialOfExistence)
                Nsec3Iterations=if ($null -ne $settings.NSec3Iterations) { [int]$settings.NSec3Iterations } else { $null }
                Nsec3OptOut=if ($null -ne $settings.NSec3OptOut) { [bool]$settings.NSec3OptOut } else { $null }
                DnsKeyRecordSetTtlSeconds=(Seconds $settings.DnsKeyRecordSetTTL)
                DsRecordSetTtlSeconds=(Seconds $settings.DSRecordSetTTL)
                DsRecordGenerationAlgorithms=@($settings.DSRecordGenerationAlgorithm | ForEach-Object { "$_" } | Sort-Object)
                ParentHasSecureDelegation=if ($null -ne $settings.ParentHasSecureDelegation) { [bool]$settings.ParentHasSecureDelegation } else { $null }
                SigningKeys=$keys
            }
        }
        function Utc-OrNull([object]$value) {
            if ($value -is [DateTime]) { return $value.ToUniversalTime().ToString('O') }
            return $null
        }
        function Convert-Anchor([object]$anchor) {
            [ordered]@{
                Type=if ($anchor.TrustAnchorType) { "$($anchor.TrustAnchorType)" } else { 'Unknown' }
                State=(Text-OrNull $anchor.TrustAnchorState)
                Data=(Text-OrNull $anchor.TrustAnchorData)
            }
        }
        function Get-ResolverConfiguration {
            $settings = Get-DnsServerSetting -All -ErrorAction Stop
            $points = @(Get-DnsServerTrustPoint -ErrorAction Stop | ForEach-Object {
                $point = $_
                $pointName = "$($point.TrustPointName)"
                $anchors = @(Get-DnsServerTrustAnchor -Name $pointName -ErrorAction Stop | ForEach-Object { Convert-Anchor $_ } | Sort-Object Type,Data)
                [ordered]@{
                    Name=$pointName; State=(Text-OrNull $point.TrustPointState)
                    LastActiveRefreshTime=(Utc-OrNull $point.LastActiveRefreshTime)
                    NextActiveRefreshTime=(Utc-OrNull $point.NextActiveRefreshTime)
                    Anchors=$anchors
                }
            } | Sort-Object Name)
            [ordered]@{
                ValidationEnabled=[bool]$settings.EnableDnsSec
                IsReadOnlyDomainController=[bool]$settings.IsReadOnlyDC
                DirectoryServicesAvailable=[bool]$settings.DsAvailable
                RootTrustAnchorsUrl=(Text-OrNull $settings.RootTrustAnchorsURL)
                TrustPoints=$points
            }
        }
        function Get-Configuration {
            $zones = @(Get-DnsServerZone -ErrorAction Stop | ForEach-Object { Convert-Zone $_ } | Sort-Object Name)
            [ordered]@{ Zones=$zones; Resolver=(Get-ResolverConfiguration) }
        }
        function Normalize-Zone([object]$zone) {
            [ordered]@{
                Name="$($zone.Name)".ToLowerInvariant(); ZoneType="$($zone.ZoneType)".ToLowerInvariant()
                IsDsIntegrated=[bool]$zone.IsDsIntegrated; IsAutoCreated=[bool]$zone.IsAutoCreated
                IsSigned=[bool]$zone.IsSigned; IsEligibleForSigning=[bool]$zone.IsEligibleForSigning
                IneligibilityReason=(Text-OrNull $zone.IneligibilityReason)
                IsKeyMasterServer=if ($null -ne $zone.IsKeyMasterServer) { [bool]$zone.IsKeyMasterServer } else { $null }
                KeyMasterServer=(Text-OrNull $zone.KeyMasterServer); KeyMasterStatus=(Text-OrNull $zone.KeyMasterStatus)
                DenialOfExistence=(Text-OrNull $zone.DenialOfExistence)
                Nsec3Iterations=if ($null -ne $zone.Nsec3Iterations) { [int]$zone.Nsec3Iterations } else { $null }
                Nsec3OptOut=if ($null -ne $zone.Nsec3OptOut) { [bool]$zone.Nsec3OptOut } else { $null }
                DnsKeyRecordSetTtlSeconds=if ($null -ne $zone.DnsKeyRecordSetTtlSeconds) { [long]$zone.DnsKeyRecordSetTtlSeconds } else { $null }
                DsRecordSetTtlSeconds=if ($null -ne $zone.DsRecordSetTtlSeconds) { [long]$zone.DsRecordSetTtlSeconds } else { $null }
                DsRecordGenerationAlgorithms=@($zone.DsRecordGenerationAlgorithms | ForEach-Object { "$_".ToLowerInvariant() } | Sort-Object)
                ParentHasSecureDelegation=if ($null -ne $zone.ParentHasSecureDelegation) { [bool]$zone.ParentHasSecureDelegation } else { $null }
                SigningKeys=@($zone.SigningKeys | ForEach-Object {
                    [ordered]@{ KeyId="$($_.KeyId)".ToLowerInvariant(); KeyType="$($_.KeyType)".ToLowerInvariant(); CryptoAlgorithm=(Text-OrNull $_.CryptoAlgorithm)
                        KeyLength=if ($null -ne $_.KeyLength) { [int]$_.KeyLength } else { $null }; KeyStatus=(Text-OrNull $_.KeyStatus)
                        KeyStorageProvider=(Text-OrNull $_.KeyStorageProvider); IsRolloverEnabled=if ($null -ne $_.IsRolloverEnabled) { [bool]$_.IsRolloverEnabled } else { $null }
                        RolloverPeriodSeconds=if ($null -ne $_.RolloverPeriodSeconds) { [long]$_.RolloverPeriodSeconds } else { $null }
                        NextRolloverAction=(Text-OrNull $_.NextRolloverAction); NextRolloverTime=(Text-OrNull $_.NextRolloverTime) }
                } | Sort-Object KeyType,KeyId)
            }
        }
        function Normalize-Resolver([object]$resolver) {
            [ordered]@{
                ValidationEnabled=[bool]$resolver.ValidationEnabled
                IsReadOnlyDomainController=[bool]$resolver.IsReadOnlyDomainController
                DirectoryServicesAvailable=[bool]$resolver.DirectoryServicesAvailable
                RootTrustAnchorsUrl=(Text-OrNull $resolver.RootTrustAnchorsUrl)
                TrustPoints=@($resolver.TrustPoints | ForEach-Object {
                    [ordered]@{
                        Name="$($_.Name)".ToLowerInvariant(); State=(Text-OrNull $_.State)
                        LastActiveRefreshTime=(Text-OrNull $_.LastActiveRefreshTime)
                        NextActiveRefreshTime=(Text-OrNull $_.NextActiveRefreshTime)
                        Anchors=@($_.Anchors | ForEach-Object { [ordered]@{ Type="$($_.Type)".ToLowerInvariant(); State=(Text-OrNull $_.State); Data=(Text-OrNull $_.Data) } } | Sort-Object Type,Data)
                    }
                } | Sort-Object Name)
            }
        }

        $before = $null
        $beforeJson = $null
        try {
            $before = Get-Configuration
            if (@($before.Zones).Count -gt 500 -or @($before.Zones.SigningKeys).Count -gt 1000 -or @($before.Resolver.TrustPoints).Count -gt 500 -or @($before.Resolver.TrustPoints.Anchors).Count -gt 2000) {
                return [pscustomobject]@{ Success=$false; FailureKind='DnssecConfigurationTooLarge'; Message='The DNSSEC configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            $beforeJson = $before | ConvertTo-Json -Compress -Depth 10
            if ($beforeJson.Length -gt 600000) {
                return [pscustomobject]@{ Success=$false; FailureKind='DnssecConfigurationTooLarge'; Message='The DNSSEC configuration exceeds the supported management limit.'; BeforeJson=$null; AfterJson=$null }
            }
            if ($Action -eq 'Read') { return [pscustomobject]@{ Success=$true; FailureKind=$null; Message='DNSSEC configuration read.'; BeforeJson=$null; AfterJson=$beforeJson } }

            $expected = $ExpectedConfigurationJson | ConvertFrom-Json -ErrorAction Stop
            $zoneAction = $Action -in @('SignWithDefaults','Resign','Unsign','RolloverKeys')
            if ($zoneAction) {
                $target = @($before.Zones | Where-Object { $_.Name -ieq $ZoneName })
                if ($target.Count -ne 1) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotFound'; Message='The DNS zone was not found.'; BeforeJson=$beforeJson; AfterJson=$null } }
                if (-not [bool]$target[0].IsEligibleForSigning) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotEligible'; Message='Only non-built-in primary zones support this DNSSEC operation.'; BeforeJson=$beforeJson; AfterJson=$null } }
                $expectedTarget = @($expected.Zones | Where-Object { $_.Name -ieq $ZoneName })
                if ($expectedTarget.Count -ne 1 -or ((Normalize-Zone $target[0]) | ConvertTo-Json -Compress -Depth 10) -cne ((Normalize-Zone $expectedTarget[0]) | ConvertTo-Json -Compress -Depth 10)) {
                    return [pscustomobject]@{ Success=$false; FailureKind='DnssecConfigurationChanged'; Message='The live DNSSEC zone state changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
                }
            } elseif (((Normalize-Resolver $before.Resolver) | ConvertTo-Json -Compress -Depth 10) -cne ((Normalize-Resolver $expected.Resolver) | ConvertTo-Json -Compress -Depth 10)) {
                return [pscustomobject]@{ Success=$false; FailureKind='DnssecConfigurationChanged'; Message='The live DNSSEC resolver state changed. Refresh and retry.'; BeforeJson=$beforeJson; AfterJson=$null }
            }

            switch ($Action) {
                'SignWithDefaults' {
                    if ([bool]$target[0].IsSigned) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneAlreadySigned'; Message='The DNS zone is already signed.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    Invoke-DnsServerZoneSign -ZoneName $ZoneName -SignWithDefault -Force -ErrorAction Stop | Out-Null
                }
                'Resign' {
                    if (-not [bool]$target[0].IsSigned) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotSigned'; Message='The DNS zone is not signed.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    Invoke-DnsServerZoneSign -ZoneName $ZoneName -DoResign -Force -ErrorAction Stop | Out-Null
                }
                'Unsign' {
                    if (-not [bool]$target[0].IsSigned) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotSigned'; Message='The DNS zone is not signed.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    Invoke-DnsServerZoneUnsign -ZoneName $ZoneName -Force -ErrorAction Stop | Out-Null
                }
                'RolloverKeys' {
                    if (-not [bool]$target[0].IsSigned) { return [pscustomobject]@{ Success=$false; FailureKind='ZoneNotSigned'; Message='The DNS zone is not signed.'; BeforeJson=$beforeJson; AfterJson=$null } }
                    $liveIds = @($target[0].SigningKeys | ForEach-Object { "$($_.KeyId)".ToLowerInvariant() })
                    foreach ($id in @($KeyIds)) { if (-not $liveIds.Contains("$id".ToLowerInvariant())) { return [pscustomobject]@{ Success=$false; FailureKind='SigningKeyNotFound'; Message='One or more selected signing keys no longer exist.'; BeforeJson=$beforeJson; AfterJson=$null } } }
                    Invoke-DnsServerSigningKeyRollover -ZoneName $ZoneName -KeyId ([Guid[]]$KeyIds) -Force -ErrorAction Stop | Out-Null
                }
                'SetValidationEnabled' {
                    $settings = Get-DnsServerSetting -All -ErrorAction Stop
                    $settings.EnableDnsSec = [bool]$ValidationEnabled
                    $settings | Set-DnsServerSetting -Confirm:$false -ErrorAction Stop | Out-Null
                }
                'RetrieveRootTrustAnchor' { Add-DnsServerTrustAnchor -Root -ErrorAction Stop | Out-Null }
                'AddDsTrustAnchor' { Add-DnsServerTrustAnchor -Name $TrustPointName -CryptoAlgorithm $CryptoAlgorithm -KeyTag ([UInt16]$KeyTag) -DigestType $DigestType -Digest $Digest -ErrorAction Stop | Out-Null }
                'AddDnsKeyTrustAnchor' { Add-DnsServerTrustAnchor -Name $TrustPointName -CryptoAlgorithm $CryptoAlgorithm -KeyProtocol DnsSec -Base64Data $Base64Data -ErrorAction Stop | Out-Null }
                'RemoveTrustAnchorType' { Remove-DnsServerTrustAnchor -Name $TrustPointName -Type $TrustAnchorType -Force -ErrorAction Stop | Out-Null }
            }
            $after = Get-Configuration
            if ($zoneAction) {
                $afterTarget = @($after.Zones | Where-Object { $_.Name -ieq $ZoneName })[0]
                if ($Action -in @('SignWithDefaults','Resign') -and -not [bool]$afterTarget.IsSigned) { throw 'DNSSEC signing read-back verification failed.' }
                if ($Action -eq 'Unsign' -and [bool]$afterTarget.IsSigned) { throw 'DNSSEC unsigning read-back verification failed.' }
            }
            if ($Action -eq 'SetValidationEnabled' -and [bool]$after.Resolver.ValidationEnabled -ne [bool]$ValidationEnabled) { throw 'DNSSEC validation setting read-back verification failed.' }
            $message = if ($Action -eq 'RolloverKeys') { 'DNSSEC signing key rollover initiated.' } elseif ($zoneAction) { 'DNSSEC zone operation completed.' } else { 'DNSSEC resolver trust configuration updated.' }
            return [pscustomobject]@{ Success=$true; FailureKind=$null; Message=$message; BeforeJson=$beforeJson; AfterJson=($after | ConvertTo-Json -Compress -Depth 10) }
        } catch {
            return [pscustomobject]@{ Success=$false; FailureKind='DnssecOperationFailed'; Message='The DNS server rejected the DNSSEC operation.'; BeforeJson=$beforeJson; AfterJson=$null }
        }
        """;
}

[SupportedOSPlatform("windows")]
public sealed class PowerShellDnsRemoteProbeExecutor(ILogger<PowerShellDnsRemoteProbeExecutor> logger)
    : IDnsRemoteProbeExecutor
{
    private const string MicrosoftPowerShellShellUri = "http://schemas.microsoft.com/powershell/Microsoft.PowerShell";
    private const int InventoryPayloadBudgetBytes = 700_000;

    public async Task<HostAgentDnsProbeResult> ProbeAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await ProbeCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Timeout", "The DNS connection test timed out.", network: false, tls: false);
        }
    }

    public async Task<HostAgentDnsInventoryPage> ReadInventoryPageAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await ReadInventoryPageCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return InventoryFailure("Timeout", "The DNS inventory page timed out.");
        }
    }

    public async Task<HostAgentDnsRecordMutationResult> MutateRecordAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await MutateRecordCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MutationFailure("Timeout", "The DNS record operation timed out.");
        }
    }

    public async Task<HostAgentDnsZoneMutationResult> MutateZoneAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await MutateZoneCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ZoneMutationFailure("Timeout", "The DNS zone operation timed out.");
        }
    }

    public async Task<HostAgentDnsServerSettingsResult> ManageServerSettingsAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await ManageServerSettingsCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServerSettingsFailure("Timeout", "The DNS server settings operation timed out.");
        }
    }

    public async Task<HostAgentDnsPolicyConfigurationResult> ManagePolicyConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try { return await ManagePolicyConfigurationCoreAsync(request, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return PolicyConfigurationFailure("Timeout", "The DNS policy operation timed out."); }
    }

    public async Task<HostAgentDnssecConfigurationResult> ManageDnssecConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try { return await ManageDnssecConfigurationCoreAsync(request, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return DnssecConfigurationFailure("Timeout", "The DNSSEC operation timed out."); }
    }

    public async Task<HostAgentDnsScavengingConfigurationResult> ManageDnsScavengingAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try { return await ManageDnsScavengingCoreAsync(request, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return ScavengingConfigurationFailure("Timeout", "The DNS scavenging operation timed out."); }
    }

    public async Task<HostAgentDnsNetworkConfigurationResult> ManageDnsNetworkConfigurationAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try { return await ManageDnsNetworkConfigurationCoreAsync(request, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return NetworkConfigurationFailure("Timeout", "The DNS network configuration operation timed out."); }
    }

    private async Task<HostAgentDnsNetworkConfigurationResult> ManageDnsNetworkConfigurationCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value, request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
            return NetworkConfigurationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable ? "The WinRM HTTPS certificate could not be validated." : "The WinRM HTTPS endpoint could not be reached.");
        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout, OperationTimeout = timeout, CancelTimeout = Math.Min(timeout, 10_000), NoMachineProfile = true,
        };
        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteNetworkConfiguration.Script, useLocalScope: true)
                .AddParameter("Action", request.DnsNetworkAction!.Value.ToString())
                .AddParameter("ListeningIpAddresses", request.DnsListeningIpAddresses?.ToArray() ?? [])
                .AddParameter("RootHintNameServer", request.DnsRootHintNameServer)
                .AddParameter("RootHintIpAddresses", request.DnsRootHintIpAddresses?.ToArray() ?? [])
                .AddParameter("OriginalRootHintNameServer", request.DnsOriginalRootHintNameServer)
                .AddParameter("ExpectedConfigurationJson", request.DnsExpectedNetworkConfigurationJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            return powerShell.HadErrors || output.Count != 1
                ? NetworkConfigurationFailure("DnsNetworkOperationFailed", "The DNS server rejected the network configuration operation.")
                : MapNetworkConfigurationResult(output[0]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is PSRemotingTransportException or RemoteException or RuntimeException or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS network configuration operation failed for {Host}:{Port} ({ExceptionType}).", host, request.DnsPort, exception.GetType().Name);
            return NetworkConfigurationFailure("DnsNetworkOperationFailed", "The DNS server rejected the network configuration operation.");
        }
    }

    private async Task<HostAgentDnsScavengingConfigurationResult> ManageDnsScavengingCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value, request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
            return ScavengingConfigurationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable ? "The WinRM HTTPS certificate could not be validated." : "The WinRM HTTPS endpoint could not be reached.");
        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout, OperationTimeout = timeout, CancelTimeout = Math.Min(timeout, 10_000), NoMachineProfile = true,
        };
        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteScavengingConfiguration.Script, useLocalScope: true)
                .AddParameter("Action", request.DnsScavengingAction!.Value.ToString())
                .AddParameter("ScavengingState", request.DnsScavengingState)
                .AddParameter("ScavengingIntervalHours", request.DnsScavengingIntervalHours)
                .AddParameter("ZoneName", request.DnsAgingZoneName)
                .AddParameter("ZoneAgingEnabled", request.DnsZoneAgingEnabled)
                .AddParameter("ZoneNoRefreshIntervalHours", request.DnsZoneNoRefreshIntervalHours)
                .AddParameter("ZoneRefreshIntervalHours", request.DnsZoneRefreshIntervalHours)
                .AddParameter("ZoneScavengeServers", request.DnsZoneScavengeServers?.ToArray() ?? [])
                .AddParameter("ExpectedConfigurationJson", request.DnsExpectedScavengingConfigurationJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            return powerShell.HadErrors || output.Count != 1
                ? ScavengingConfigurationFailure("DnsScavengingOperationFailed", "The DNS server rejected the aging or scavenging operation.")
                : MapScavengingConfigurationResult(output[0]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is PSRemotingTransportException or RemoteException or RuntimeException or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS scavenging operation failed for {Host}:{Port} ({ExceptionType}).", host, request.DnsPort, exception.GetType().Name);
            return ScavengingConfigurationFailure("DnsScavengingOperationFailed", "The DNS server rejected the aging or scavenging operation.");
        }
    }

    private async Task<HostAgentDnssecConfigurationResult> ManageDnssecConfigurationCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value, request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
            return DnssecConfigurationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable ? "The WinRM HTTPS certificate could not be validated." : "The WinRM HTTPS endpoint could not be reached.");
        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout, OperationTimeout = timeout, CancelTimeout = Math.Min(timeout, 10_000), NoMachineProfile = true,
        };
        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteDnssecConfiguration.Script, useLocalScope: true)
                .AddParameter("Action", request.DnssecAction!.Value.ToString())
                .AddParameter("ZoneName", request.DnssecZoneName)
                .AddParameter("KeyIds", request.DnssecKeyIds?.ToArray() ?? [])
                .AddParameter("ValidationEnabled", request.DnssecValidationEnabled)
                .AddParameter("TrustPointName", request.DnssecTrustPointName)
                .AddParameter("TrustAnchorType", request.DnssecTrustAnchorType ?? string.Empty)
                .AddParameter("CryptoAlgorithm", request.DnssecCryptoAlgorithm ?? string.Empty)
                .AddParameter("KeyTag", request.DnssecKeyTag)
                .AddParameter("DigestType", request.DnssecDigestType ?? string.Empty)
                .AddParameter("Digest", request.DnssecDigest)
                .AddParameter("Base64Data", request.DnssecBase64Data)
                .AddParameter("ExpectedConfigurationJson", request.DnsExpectedDnssecConfigurationJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            return powerShell.HadErrors || output.Count != 1
                ? DnssecConfigurationFailure("DnssecOperationFailed", "The DNS server rejected the DNSSEC operation.")
                : MapDnssecConfigurationResult(output[0]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is PSRemotingTransportException or RemoteException or RuntimeException or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNSSEC operation failed for {Host}:{Port} ({ExceptionType}).", host, request.DnsPort, exception.GetType().Name);
            return DnssecConfigurationFailure("DnssecOperationFailed", "The DNS server rejected the DNSSEC operation.");
        }
    }

    private async Task<HostAgentDnsPolicyConfigurationResult> ManagePolicyConfigurationCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value, request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
            return PolicyConfigurationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable ? "The WinRM HTTPS certificate could not be validated." : "The WinRM HTTPS endpoint could not be reached.");
        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout, OperationTimeout = timeout, CancelTimeout = Math.Min(timeout, 10_000), NoMachineProfile = true,
        };
        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemotePolicyConfiguration.Script, useLocalScope: true)
                .AddParameter("Action", request.DnsPolicyAction!.Value.ToString())
                .AddParameter("MutationJson", request.DnsPolicyMutation is null ? null : JsonSerializer.Serialize(request.DnsPolicyMutation, HostAgentProtocol.Json))
                .AddParameter("ExpectedConfigurationJson", request.DnsExpectedPolicyConfigurationJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            return powerShell.HadErrors || output.Count != 1
                ? PolicyConfigurationFailure("DnsPolicyOperationFailed", "The DNS server rejected the policy operation.")
                : MapPolicyConfigurationResult(output[0]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is PSRemotingTransportException or RemoteException or RuntimeException or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS policy operation failed for {Host}:{Port} ({ExceptionType}).", host, request.DnsPort, exception.GetType().Name);
            return PolicyConfigurationFailure("DnsPolicyOperationFailed", "The DNS server rejected the policy operation.");
        }
    }

    private async Task<HostAgentDnsServerSettingsResult> ManageServerSettingsCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return ServerSettingsFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.");
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteServerSettings.Script, useLocalScope: true)
                .AddParameter("Action", request.DnsServerSettingsAction!.Value.ToString())
                .AddParameter("ForwarderAddresses", request.DnsForwarderAddresses?.ToArray() ?? [])
                .AddParameter("ForwarderUseRootHint", request.DnsForwarderUseRootHint ?? false)
                .AddParameter("ForwarderTimeoutSeconds", request.DnsForwarderTimeoutSeconds ?? 5)
                .AddParameter("ForwarderEnableReordering", request.DnsForwarderEnableReordering ?? true)
                .AddParameter("RecursionEnabled", request.DnsRecursionEnabled ?? true)
                .AddParameter("RecursionAdditionalTimeoutSeconds", request.DnsRecursionAdditionalTimeoutSeconds ?? 4)
                .AddParameter("RecursionRetryIntervalSeconds", request.DnsRecursionRetryIntervalSeconds ?? 3)
                .AddParameter("RecursionTimeoutSeconds", request.DnsRecursionTimeoutSeconds ?? 8)
                .AddParameter("RecursionSecureResponse", request.DnsRecursionSecureResponse ?? true)
                .AddParameter("ExpectedServerSettingsJson", request.DnsExpectedServerSettingsJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors || output.Count != 1)
                return ServerSettingsFailure("DnsServerSettingsOperationFailed", "The DNS server rejected the server settings operation.");
            return MapServerSettingsResult(output[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS server settings operation failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return ServerSettingsFailure("DnsServerSettingsOperationFailed", "The DNS server rejected the server settings operation.");
        }
    }

    private async Task<HostAgentDnsZoneMutationResult> MutateZoneCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return ZoneMutationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.");
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteZoneMutation.Script, useLocalScope: true)
                .AddParameter("MutationKind", request.DnsZoneMutationKind!.Value.ToString())
                .AddParameter("ZoneKind", request.DnsZoneKind!.Value.ToString())
                .AddParameter("ZoneName", request.DnsZoneName)
                .AddParameter("IsDsIntegrated", request.DnsZoneIsDsIntegrated)
                .AddParameter("DynamicUpdate", request.DnsZoneDynamicUpdate)
                .AddParameter("ReplicationScope", request.DnsZoneReplicationScope)
                .AddParameter("DirectoryPartitionName", request.DnsZonePartitionName)
                .AddParameter("ZoneFile", request.DnsZoneFile)
                .AddParameter("MasterServers", request.DnsZoneMasterServers?.ToArray() ?? [])
                .AddParameter("ForwarderTimeoutSeconds", request.DnsZoneForwarderTimeoutSeconds ?? 5)
                .AddParameter("UseRecursion", request.DnsZoneUseRecursion ?? false)
                .AddParameter("ExpectedZoneStateJson", request.DnsExpectedZoneStateJson);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors || output.Count != 1)
                return ZoneMutationFailure("DnsZoneMutationFailed", "The DNS server rejected the zone operation.");
            return MapZoneMutation(output[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS zone mutation failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return ZoneMutationFailure("DnsZoneMutationFailed", "The DNS server rejected the zone operation.");
        }
    }

    private async Task<HostAgentDnsRecordMutationResult> MutateRecordCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return MutationFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.");
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteRecordMutation.Script, useLocalScope: true)
                .AddParameter("MutationKind", request.DnsRecordMutationKind!.Value.ToString())
                .AddParameter("ZoneName", request.DnsZoneName)
                .AddParameter("ZoneScope", request.DnsZoneScope)
                .AddParameter("VirtualizationInstance", request.DnsVirtualizationInstance)
                .AddParameter("RelativeName", request.DnsRecordRelativeName)
                .AddParameter("RecordType", request.DnsRecordType)
                .AddParameter("RecordValues", request.DnsRecordValues?.ToArray() ?? [])
                .AddParameter("TimeToLiveSeconds", request.DnsRecordTimeToLiveSeconds)
                .AddParameter("ExpectedRecordDataJson", request.DnsExpectedRecordDataJson)
                .AddParameter("ExpectedRecordTimeToLiveSeconds", request.DnsExpectedRecordTimeToLiveSeconds);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors || output.Count != 1)
                return MutationFailure("DnsRecordMutationFailed", "The DNS server rejected the record operation.");
            return MapMutation(output[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS record mutation failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return MutationFailure("DnsRecordMutationFailed", "The DNS server rejected the record operation.");
        }
    }

    private async Task<HostAgentDnsInventoryPage> ReadInventoryPageCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return InventoryFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.");
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic
                : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteInventoryProbe.Script, useLocalScope: true)
                .AddParameter("InventoryKind", request.DnsInventoryKind!.Value.ToString())
                .AddParameter("Offset", request.DnsInventoryOffset!.Value)
                .AddParameter("PageSize", request.DnsInventoryPageSize!.Value)
                .AddParameter("ZoneName", request.DnsZoneName)
                .AddParameter("ZoneScope", request.DnsZoneScope)
                .AddParameter("VirtualizationInstance", request.DnsVirtualizationInstance);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors)
            {
                return InventoryFailure("DnsInventoryReadFailed", "The DNS inventory page could not be read.");
            }

            if (request.DnsInventoryKind == HostAgentDnsInventoryKind.Zones)
            {
                var values = FitPayload(output.Take(request.DnsInventoryPageSize.Value).Select(MapZone));
                return values.Count == 0 && output.Count > 0
                    ? InventoryFailure("InventoryItemTooLarge", "A DNS zone inventory item exceeded the response limit.")
                    : new HostAgentDnsInventoryPage
                    {
                        Success = true,
                        Message = "DNS zone inventory page read.",
                        HasMore = output.Count > values.Count,
                        Zones = values,
                    };
            }
            else
            {
                var values = FitPayload(output.Take(request.DnsInventoryPageSize.Value).Select(MapRecord));
                return values.Count == 0 && output.Count > 0
                    ? InventoryFailure("InventoryItemTooLarge", "A DNS record inventory item exceeded the response limit.")
                    : new HostAgentDnsInventoryPage
                    {
                        Success = true,
                        Message = "DNS record inventory page read.",
                        HasMore = output.Count > values.Count,
                        Records = values,
                    };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS inventory read failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return InventoryFailure("DnsInventoryReadFailed", "The DNS inventory page could not be read.");
        }
    }

    private async Task<HostAgentDnsProbeResult> ProbeCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return Failure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.",
                tls.NetworkReachable, tls.Valid);
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic
                : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS WinRM connection failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return Failure("AuthenticationOrRemotingFailed",
                "WinRM rejected the credentials or the remote PowerShell endpoint is unavailable.",
                network: true, tls: true);
        }

        try
        {
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteCapabilityProbe.Script, useLocalScope: true);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors || output.Count != 1)
            {
                return Failure("DnsCapabilityProbeFailed",
                    "The connection succeeded, but DNS Server capabilities could not be read.",
                    network: true, tls: true, authentication: true);
            }

            return MapSuccess(output[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS Server capability probe failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return Failure("DnsCapabilityProbeFailed",
                "The connection succeeded, but DNS Server capabilities could not be read.",
                network: true, tls: true, authentication: true);
        }
    }

    private static HostAgentDnsProbeResult MapSuccess(PSObject value) => new()
    {
        Success = true,
        Message = "The DNS server connection and capability probe succeeded.",
        NetworkReachable = true,
        TlsValidated = true,
        AuthenticationSucceeded = true,
        DnsModuleAvailable = true,
        DnsServiceReachable = true,
        OperatingSystemVersion = ReadString(value, "OperatingSystemVersion", 128),
        PowerShellVersion = ReadString(value, "PowerShellVersion", 64),
        DnsModuleVersion = ReadString(value, "DnsModuleVersion", 64),
        DnsServerVersion = ReadString(value, "DnsServerVersion", 128),
        ZoneCount = ReadInt(value, "ZoneCount"),
        Capabilities = new HostAgentDnsCapabilities
        {
            Zones = ReadBool(value, "Zones"),
            Records = ReadBool(value, "Records"),
            ServerSettings = ReadBool(value, "ServerSettings"),
            Dnssec = ReadBool(value, "Dnssec"),
            Policies = ReadBool(value, "Policies"),
            Scopes = ReadBool(value, "Scopes"),
            Cache = ReadBool(value, "Cache"),
        },
    };

    private static async Task<TlsProbeResult> ValidateTlsAsync(
        string host, int port, string? expectedThumbprint, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new(false, false);
        }

        var policyValid = false;
        var pinValid = string.IsNullOrWhiteSpace(expectedThumbprint);
        using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
            (_, certificate, _, errors) =>
            {
                policyValid = errors == SslPolicyErrors.None;
                if (certificate is not null && !string.IsNullOrWhiteSpace(expectedThumbprint))
                {
                    using var parsed = new X509Certificate2(certificate);
                    var expected = NormalizeThumbprint(expectedThumbprint);
                    var actual = expected.Length == 64
                        ? parsed.GetCertHashString(HashAlgorithmName.SHA256)
                        : parsed.GetCertHashString(HashAlgorithmName.SHA1);
                    pinValid = CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(expected), Convert.FromHexString(actual));
                }
                return policyValid && pinValid;
            });
        try
        {
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online,
            }, cancellationToken);
            return new(true, policyValid && pinValid);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            return new(true, false);
        }
    }

    private static SecureString ToSecureString(string value)
    {
        var result = new SecureString();
        foreach (var character in value) result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }

    private static string NormalizeThumbprint(string value) =>
        string.Concat(value.Where(Uri.IsHexDigit)).ToUpperInvariant();
    private static string? ReadString(PSObject value, string name, int maxLength)
    {
        var text = value.Properties[name]?.Value?.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, maxLength)];
    }
    private static bool ReadBool(PSObject value, string name) =>
        LanguagePrimitives.TryConvertTo(value.Properties[name]?.Value, out bool result) && result;
    private static int? ReadInt(PSObject value, string name) =>
        LanguagePrimitives.TryConvertTo(value.Properties[name]?.Value, out int result) && result >= 0 ? result : null;
    private static DateTime? ReadDateTime(PSObject value, string name)
    {
        var raw = value.Properties[name]?.Value;
        if (raw is DateTime timestamp) return timestamp.ToUniversalTime();
        return DateTime.TryParse(raw?.ToString(), null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed) ? parsed : null;
    }
    private static IReadOnlyList<string> ReadStrings(PSObject value, string name, int maxLength)
    {
        var raw = value.Properties[name]?.Value;
        if (raw is null) return [];
        var values = raw is string text ? [text] : raw is System.Collections.IEnumerable sequence
            ? sequence.Cast<object?>().Select(x => x?.ToString())
            : [raw.ToString()];
        return values.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()[..Math.Min(x.Trim().Length, maxLength)])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static HostAgentDnsZoneInventoryItem MapZone(PSObject value) => new()
    {
        Name = ReadString(value, "Name", 253) ?? string.Empty,
        ZoneType = ReadString(value, "ZoneType", 64) ?? "Unknown",
        IsReverseLookupZone = ReadBool(value, "IsReverseLookupZone"),
        IsDsIntegrated = ReadBool(value, "IsDsIntegrated"),
        IsSigned = ReadBool(value, "IsSigned"),
        IsPaused = ReadBool(value, "IsPaused"),
        DynamicUpdate = ReadString(value, "DynamicUpdate", 64),
        ReplicationScope = ReadString(value, "ReplicationScope", 64),
        DirectoryPartitionName = ReadString(value, "DirectoryPartitionName", 512),
        ZoneFile = ReadString(value, "ZoneFile", 512),
        VirtualizationInstance = ReadString(value, "VirtualizationInstance", 128),
        ZoneScopes = ReadStrings(value, "ZoneScopes", 128),
        IsAutoCreated = ReadBool(value, "IsAutoCreated"),
        MasterServers = ReadStrings(value, "MasterServers", 64),
        ForwarderTimeoutSeconds = ReadInt(value, "ForwarderTimeoutSeconds"),
        UseRecursion = value.Properties["UseRecursion"]?.Value is null ? null : ReadBool(value, "UseRecursion"),
    };
    private static HostAgentDnsRecordInventoryItem MapRecord(PSObject value) => new()
    {
        RelativeName = ReadString(value, "RelativeName", 253) ?? "@",
        RecordType = ReadString(value, "RecordType", 32) ?? "Unknown",
        RecordDataJson = ReadString(value, "RecordDataJson", 256_000) ?? "{}",
        TimeToLiveSeconds = ReadInt(value, "TimeToLiveSeconds") ?? 0,
        Timestamp = ReadDateTime(value, "Timestamp"),
        ZoneScope = ReadString(value, "ZoneScope", 128),
        VirtualizationInstance = ReadString(value, "VirtualizationInstance", 128),
    };
    private static HostAgentDnsRecordMutationResult MapMutation(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsRecordMutationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS record operation completed." : "The DNS record operation failed."),
            Before = ReadRecordJson(value, "BeforeRecordJson"),
            After = ReadRecordJson(value, "AfterRecordJson"),
        };
    }
    private static HostAgentDnsRecordInventoryItem? ReadRecordJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<HostAgentDnsRecordInventoryItem>(json, HostAgentProtocol.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private static HostAgentDnsZoneMutationResult MapZoneMutation(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsZoneMutationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS zone operation completed." : "The DNS zone operation failed."),
            Before = ReadZoneJson(value, "BeforeZoneJson"),
            After = ReadZoneJson(value, "AfterZoneJson"),
        };
    }
    private static HostAgentDnsZoneInventoryItem? ReadZoneJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<HostAgentDnsZoneInventoryItem>(json, HostAgentProtocol.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private static HostAgentDnsServerSettingsResult MapServerSettingsResult(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsServerSettingsResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS server settings operation completed." : "The DNS server settings operation failed."),
            Before = ReadServerSettingsJson(value, "BeforeJson"),
            After = ReadServerSettingsJson(value, "AfterJson"),
        };
    }
    private static HostAgentDnsServerSettings? ReadServerSettingsJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<HostAgentDnsServerSettings>(json, HostAgentProtocol.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private static HostAgentDnsPolicyConfigurationResult MapPolicyConfigurationResult(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsPolicyConfigurationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS policy operation completed." : "The DNS policy operation failed."),
            Before = ReadPolicyConfigurationJson(value, "BeforeJson"),
            After = ReadPolicyConfigurationJson(value, "AfterJson"),
        };
    }
    private static HostAgentDnsPolicyConfiguration? ReadPolicyConfigurationJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<HostAgentDnsPolicyConfiguration>(json, HostAgentProtocol.Json); }
        catch (JsonException) { return null; }
    }
    private static HostAgentDnssecConfigurationResult MapDnssecConfigurationResult(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnssecConfigurationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNSSEC operation completed." : "The DNSSEC operation failed."),
            Before = ReadDnssecConfigurationJson(value, "BeforeJson"),
            After = ReadDnssecConfigurationJson(value, "AfterJson"),
        };
    }
    private static HostAgentDnssecConfiguration? ReadDnssecConfigurationJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<HostAgentDnssecConfiguration>(json, HostAgentProtocol.Json); }
        catch (JsonException) { return null; }
    }
    private static HostAgentDnsScavengingConfigurationResult MapScavengingConfigurationResult(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsScavengingConfigurationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS scavenging operation completed." : "The DNS scavenging operation failed."),
            Before = ReadScavengingConfigurationJson(value, "BeforeJson"),
            After = ReadScavengingConfigurationJson(value, "AfterJson"),
        };
    }
    private static HostAgentDnsScavengingConfiguration? ReadScavengingConfigurationJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<HostAgentDnsScavengingConfiguration>(json, HostAgentProtocol.Json); }
        catch (JsonException) { return null; }
    }
    private static HostAgentDnsNetworkConfigurationResult MapNetworkConfigurationResult(PSObject value)
    {
        var success = ReadBool(value, "Success");
        return new HostAgentDnsNetworkConfigurationResult
        {
            Success = success,
            FailureKind = ReadString(value, "FailureKind", 64),
            Message = ReadString(value, "Message", 2000)
                ?? (success ? "DNS network configuration operation completed." : "The DNS network configuration operation failed."),
            Before = ReadNetworkConfigurationJson(value, "BeforeJson"),
            After = ReadNetworkConfigurationJson(value, "AfterJson"),
        };
    }
    private static HostAgentDnsNetworkConfiguration? ReadNetworkConfigurationJson(PSObject value, string property)
    {
        var json = value.Properties[property]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<HostAgentDnsNetworkConfiguration>(json, HostAgentProtocol.Json); }
        catch (JsonException) { return null; }
    }
    private static HostAgentDnsProbeResult Failure(string kind, string message, bool network, bool tls, bool authentication = false) =>
        new()
        {
            Success = false,
            FailureKind = kind,
            Message = message,
            NetworkReachable = network,
            TlsValidated = tls,
            AuthenticationSucceeded = authentication
        };
    private static HostAgentDnsInventoryPage InventoryFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsRecordMutationResult MutationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsZoneMutationResult ZoneMutationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsServerSettingsResult ServerSettingsFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsPolicyConfigurationResult PolicyConfigurationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnssecConfigurationResult DnssecConfigurationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsScavengingConfigurationResult ScavengingConfigurationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static HostAgentDnsNetworkConfigurationResult NetworkConfigurationFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static IReadOnlyList<T> FitPayload<T>(IEnumerable<T> source)
    {
        var result = new List<T>();
        var bytes = 0;
        foreach (var item in source)
        {
            var itemBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item, HostAgentProtocol.Json).Length;
            if (bytes + itemBytes > InventoryPayloadBudgetBytes) break;
            result.Add(item);
            bytes += itemBytes;
        }
        return result;
    }
    private sealed record TlsProbeResult(bool NetworkReachable, bool Valid);
}
