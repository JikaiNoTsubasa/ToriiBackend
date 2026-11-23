namespace Torii.Api.Models;

/// <summary>
/// Lifecycle of an account. Self-registered accounts start <see cref="PENDING"/> and wait for an
/// administrator; only <see cref="ACTIVE"/> accounts (that are also enabled) can sign in.
/// </summary>
public enum AccountStatus
{
    /// <summary>Requested through /register, waiting for an administrator's decision.</summary>
    PENDING,

    /// <summary>Approved (or created by an administrator).</summary>
    ACTIVE,

    /// <summary>Request declined by an administrator; the account cannot sign in.</summary>
    REJECTED,
}
