namespace ITAdmin.Domain.Enums;

public enum LicenseSeatAssignmentStatus
{
    /// <summary>The seat is currently held by this person.</summary>
    Active = 1,

    /// <summary>The seat was given back and is now free (person left, licence no longer needed).</summary>
    Released = 2,

    /// <summary>The seat was handed to another person; see the assignment that replaced it.</summary>
    Transferred = 3,
}
