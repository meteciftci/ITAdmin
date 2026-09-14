namespace ITAdmin.Domain.Enums;

public enum DnsServerEnvironment
{
    Internal,
    Public,
    Other,
}

public enum DnsAuthenticationMode
{
    Negotiate,
    BasicOverTls,
}

public enum DnsConnectionTransport
{
    Https,
    Http,
}

public enum DnsSyncScope
{
    Health,
    Zones,
    Records,
    FullInventory,
}

public enum DnsSyncTrigger
{
    Scheduled,
    Manual,
    PostMutation,
}

public enum DnsSyncStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}
