using Torii.Api.Models;

namespace Torii.Api.Exceptions;

/// <summary>
/// Valid credentials, but the account is still waiting for approval or was declined (HTTP 403).
/// <see cref="Code"/> is <c>ACCOUNT_PENDING</c> or <c>ACCOUNT_REJECTED</c>.
/// </summary>
public sealed class AccountNotActiveException(AccountStatus status) : ToriiException(
    status == AccountStatus.PENDING
        ? "Your account is waiting for an administrator's approval. You will be able to sign in once it is approved."
        : "Your account request was declined by an administrator.")
{
    public AccountStatus Status { get; } = status;

    public override int StatusCode => StatusCodes.Status403Forbidden;

    public override string Title => Status == AccountStatus.PENDING
        ? "Account pending approval"
        : "Account request declined";

    public override string Code => $"ACCOUNT_{Status}";
}
