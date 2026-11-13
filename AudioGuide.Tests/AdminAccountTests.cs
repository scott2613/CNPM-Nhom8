using AudioGuide.Accounts;
using AudioGuide.API.Controllers;
using AudioGuide.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AudioGuide.Tests;

public sealed class AdminAccountTests
{
    [SqlFact]
    public async Task ImportedAccountCanRenameAndChangeToFourCharacterPassword()
    {
        var connection = Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.UseNetTopologySuite()).Options;
        await using var db = new AppDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();

        var accounts = new AdminAccountsController(db);
        Assert.IsType<NoContentResult>((await accounts.Status(default)).Result);

        const string originalName = "admin";
        const string originalPassword = "OriginalPassword";
        const string updatedName = "nhom8";
        const string newPassword = "moi4";

        var hash = new PasswordHasher<string>().HashPassword(originalName, originalPassword);
        var seed = new AdminAccountSeed(originalName, hash);
        Assert.IsType<NoContentResult>(await accounts.Initialize(seed, default));
        Assert.IsType<ConflictResult>(await accounts.Initialize(seed, default));

        async Task<AdminAccountStatus> VerifyAsync(string username, string password)
        {
            var result = await accounts.Login(new AdminLoginRequest(username, password), default);
            return Assert.IsType<AdminAccountStatus>(Assert.IsType<OkObjectResult>(result.Result).Value);
        }

        var original = await VerifyAsync(originalName, originalPassword);
        Assert.IsType<UnauthorizedResult>(await accounts.Change(
            new AdminAccountChange(original.Version, "wrong", updatedName, null), default));
        Assert.IsType<BadRequestResult>(await accounts.Change(
            new AdminAccountChange(original.Version, originalPassword, null, "123"), default));

        Assert.IsType<NoContentResult>(await accounts.Change(
            new AdminAccountChange(original.Version, originalPassword, updatedName, null), default));
        var renamed = await VerifyAsync(updatedName, originalPassword);
        Assert.NotEqual(original.Version, renamed.Version);
        Assert.IsType<UnauthorizedResult>((await accounts.Login(new AdminLoginRequest(originalName, originalPassword), default)).Result);
        Assert.IsType<ConflictResult>(await accounts.Change(
            new AdminAccountChange(original.Version, originalPassword, null, newPassword), default));

        Assert.IsType<NoContentResult>(await accounts.Change(
            new AdminAccountChange(renamed.Version, originalPassword, null, newPassword), default));
        var updated = await VerifyAsync(updatedName, newPassword);
        Assert.NotEqual(renamed.Version, updated.Version);
        Assert.IsType<UnauthorizedResult>((await accounts.Login(new AdminLoginRequest(updatedName, originalPassword), default)).Result);
    }
}
