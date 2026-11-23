using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Services.Singpass;

/// <summary>
/// Singpass sign-in and account requests:
/// <list type="number">
/// <item><see cref="StartAsync"/> pushes the authorization request and keeps the attempt
/// (by state, 10 minutes, the time Singpass gives the user);</item>
/// <item><see cref="CallbackAsync"/> completes it: a linked account is signed in (or told it is
/// pending / declined); an unknown identity gets a single-use registration token;</item>
/// <item><see cref="RegisterAsync"/> turns that token into a PENDING SINGPASS account, reviewed by
/// the administrators like any other request.</item>
/// </list>
/// Attempts live in memory: a restart in the middle of a sign-in only asks the user to retry.
/// </summary>
public sealed class SingpassAuthService(
    ToriiDbContext db,
    SingpassConfigManager configs,
    SingpassClient singpass,
    IAuthService auth,
    IMemoryCache cache,
    ILogger<SingpassAuthService> logger)
{
    private static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RegistrationLifetime = TimeSpan.FromMinutes(15);

    private sealed record PendingRegistration(string Subject);

    public async Task<SingpassStatusDto> GetStatusAsync(CancellationToken ct = default)
        => new((await configs.GetAsync(ct)).IsAvailable);

    public async Task<SingpassStartResponse> StartAsync(CancellationToken ct = default)
    {
        var config = await GetAvailableConfigAsync(ct);
        var (url, session) = await singpass.PushAuthorizationAsync(config, ct);
        cache.Set(AttemptKey(session.State), session, AttemptLifetime);
        return new SingpassStartResponse(url);
    }

    public async Task<SingpassCallbackResponse> CallbackAsync(SingpassCallbackRequest request, CancellationToken ct = default)
    {
        var config = await GetAvailableConfigAsync(ct);

        // Single use: the attempt is dropped whatever happens next.
        SingpassAuthSession? session = null;
        if (request.State is not null && cache.TryGetValue(AttemptKey(request.State), out session))
            cache.Remove(AttemptKey(request.State));
        if (session is null)
            throw new SingpassException("This Singpass sign-in has expired. Please try again.", StatusCodes.Status400BadRequest);

        if (request.Iss is not null && request.Iss != session.Issuer)
            throw new SingpassException("The Singpass response does not come from the expected issuer.", StatusCodes.Status400BadRequest);
        if (request.Error is not null)
        {
            logger.LogWarning("Singpass returned an error to the callback: {Error}.", request.Error);
            throw new SingpassException(request.Error == "access_denied"
                ? "The Singpass sign-in was cancelled."
                : "Singpass could not complete the sign-in. Please try again.", StatusCodes.Status400BadRequest);
        }
        if (string.IsNullOrEmpty(request.Code))
            throw new SingpassException("The Singpass response is incomplete.", StatusCodes.Status400BadRequest);

        var identity = await singpass.ExchangeCodeAsync(config, session, request.Code, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.SingpassSubject == identity.Subject && !u.IsDeleted, ct);
        if (user is not null)
        {
            var response = await auth.SignInAsync(user, ct);
            logger.LogInformation("Singpass sign-in: user {UserId}.", user.Id);
            return new SingpassCallbackResponse(SingpassCallbackOutcome.SIGNED_IN, response, null);
        }

        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        cache.Set(RegistrationKey(token), new PendingRegistration(identity.Subject), RegistrationLifetime);
        return new SingpassCallbackResponse(
            SingpassCallbackOutcome.REGISTRATION_REQUIRED,
            null,
            new SingpassRegistrationDto(token, identity.Name, identity.Email));
    }

    public async Task<RegistrationResponse> RegisterAsync(SingpassRegisterRequest request, CancellationToken ct = default)
    {
        await GetAvailableConfigAsync(ct);
        if (!cache.TryGetValue(RegistrationKey(request.Token), out PendingRegistration? pending) || pending is null)
            throw new SingpassException("Your Singpass verification has expired. Please sign up with Singpass again.",
                StatusCodes.Status400BadRequest);

        // No password: a random, never revealed one keeps the column filled; the account signs in with Singpass.
        var unusablePassword = BCrypt.Net.BCrypt.HashPassword(Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)));
        var result = await auth.RequestAccountAsync(
            new AccountRequest(request.Email, request.DisplayName, request.Position, unusablePassword,
                AccountType.SINGPASS, pending.Subject),
            ct);

        cache.Remove(RegistrationKey(request.Token));
        return result;
    }

    private async Task<SingpassConfig> GetAvailableConfigAsync(CancellationToken ct)
    {
        var config = await configs.GetAsync(ct);
        return config.IsAvailable
            ? config
            : throw new SingpassException("Singpass sign-in is not available.", StatusCodes.Status403Forbidden);
    }

    private static string AttemptKey(string state) => $"singpass:attempt:{state}";
    private static string RegistrationKey(string token) => $"singpass:registration:{token}";
}
