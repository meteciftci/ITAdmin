namespace ITAdmin.Application.Common.Models.Notifications;

/// <summary>
/// Which flavour of SMS a notification should be sent as. Only providers that expose distinct
/// endpoints for it (currently Teknomart: OTP vs Single) act on this; others ignore it.
/// </summary>
public enum SmsSendKind
{
    /// <summary>Use the provider's configured default.</summary>
    Default = 0,

    /// <summary>One-time-password / transactional endpoint.</summary>
    Otp = 1,

    /// <summary>Standard single-recipient message endpoint.</summary>
    Single = 2,
}
