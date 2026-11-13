using AudioGuide.Accounts;
using AudioGuide.Core.Entities;
using AudioGuide.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.API.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin-account")]
public sealed class AdminAccountsController(AppDbContext db) : ControllerBase
{
    private static readonly PasswordHasher<string> Hasher = new();

    [HttpGet]
    public async Task<ActionResult<AdminAccountStatus>> Status(CancellationToken ct)
    {
        var account = await db.AdminAccounts.AsNoTracking()
            .Select(value => new AdminAccountStatus(value.Username, value.Version))
            .SingleOrDefaultAsync(ct);
        return account is null ? NoContent() : Ok(account);
    }

    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(AdminAccountSeed seed, CancellationToken ct)
    {
        if (!AdminAccountRules.IsUsernameValid(seed.Username) || !IsSupportedHash(seed.PasswordHash))
            return BadRequest();

        var id = AdminAccount.SingleAccountId;
        var version = Guid.NewGuid();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.AdminAccounts (Id, Username, PasswordHash, Version)
            SELECT {id}, {seed.Username}, {seed.PasswordHash}, {version}
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AdminAccounts WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id});
            """, ct);

        return inserted == 1 ? NoContent() : Conflict();
    }

    [HttpPost("login")]
    public async Task<ActionResult<AdminAccountStatus>> Login(AdminLoginRequest request, CancellationToken ct)
    {
        var account = await db.AdminAccounts.AsNoTracking().SingleOrDefaultAsync(ct);
        if (account is null || request.Username != account.Username || !Verify(account, request.Password))
            return Unauthorized();

        return Ok(new AdminAccountStatus(account.Username, account.Version));
    }

    [HttpPut]
    public async Task<IActionResult> Change(AdminAccountChange request, CancellationToken ct)
    {
        if ((request.NewUsername is null && request.NewPassword is null)
            || (request.NewUsername is not null && !AdminAccountRules.IsUsernameValid(request.NewUsername))
            || (request.NewPassword is not null && !AdminAccountRules.IsNewPasswordValid(request.NewPassword)))
            return BadRequest();

        var account = await db.AdminAccounts.SingleOrDefaultAsync(ct);
        if (account is null || account.Version != request.Version)
            return Conflict();

        if (!Verify(account, request.CurrentPassword))
            return Unauthorized();

        if ((request.NewUsername is null || request.NewUsername == account.Username) && request.NewPassword is null)
            return BadRequest();

        account.Username = request.NewUsername ?? account.Username;
        account.PasswordHash = Hasher.HashPassword(account.Username, request.NewPassword ?? request.CurrentPassword);
        account.Version = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        return NoContent();
    }

    private static bool Verify(AdminAccount account, string password)
    {
        if (password is not { Length: > 0 and <= AdminAccountRules.MaximumPasswordLength })
            return false;

        try
        {
            return Hasher.VerifyHashedPassword(account.Username, account.PasswordHash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSupportedHash(string? hash)
    {
        if (hash is not { Length: > 0 and <= 512 })
            return false;

        Span<byte> bytes = stackalloc byte[384];
        return Convert.TryFromBase64String(hash, bytes, out var count)
            && ((count == 49 && bytes[0] == 0) || (count >= 45 && bytes[0] == 1));
    }
}
