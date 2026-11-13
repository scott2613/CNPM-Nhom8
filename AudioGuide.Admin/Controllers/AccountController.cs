using System.Net;
using System.Security.Claims;
using AudioGuide.Accounts;
using AudioGuide.Admin.Services;
using AudioGuide.LocalDevelopment;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AudioGuide.Admin.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(
    IConfiguration configuration, IWebHostEnvironment environment, AdminAccountClient accounts) : Controller
{
    private string InitialUsername => configuration["Security:AdminUsername"] ?? "admin";

    private bool CanSetUp => environment.IsDevelopment() && Request.IsHttps
        && HttpContext.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address)
        && Uri.TryCreate($"https://{Request.Host}", UriKind.Absolute, out var origin) && origin.IsLoopback;

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Login(string? returnUrl, CancellationToken ct)
    {
        try
        {
            return LoginPage(await GetOrImportAccountAsync(ct), returnUrl);
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            return Unavailable();
        }
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string? username, string? password, string? returnUrl, CancellationToken ct)
    {
        try
        {
            var existing = await GetOrImportAccountAsync(ct);
            if (existing is null)
                return LoginPage(null, returnUrl);

            var verified = AdminAccountRules.IsUsernameValid(username)
                && password is { Length: > 0 and <= AdminAccountRules.MaximumPasswordLength }
                ? await accounts.VerifyAsync(new AdminLoginRequest(username!, password), ct)
                : null;
            if (verified is null)
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return LoginPage(existing, returnUrl);
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, verified.Username),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(AdminAccountRules.VersionClaim, verified.Version.ToString("D"))
                ], CookieAuthenticationDefaults.AuthenticationScheme)));
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            return Unavailable();
        }
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Setup(string? password, string? confirmPassword, CancellationToken ct)
    {
        if (!CanSetUp)
            return NotFound();

        try
        {
            if (await GetOrImportAccountAsync(ct) is not null)
                return RedirectToAction(nameof(Login));

            if (!AdminAccountRules.IsNewPasswordValid(password) || password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu cần từ 4 đến 1024 ký tự và hai ô phải giống nhau.");
                return LoginPage(null, null);
            }

            var hash = new PasswordHasher<string>().HashPassword(InitialUsername, password!);
            var status = await accounts.InitializeAsync(new AdminAccountSeed(InitialUsername, hash), ct);
            if (status == HttpStatusCode.NoContent)
                TempData["SetupComplete"] = "Đã tạo mật khẩu. Hãy đăng nhập.";
            else if (status != HttpStatusCode.Conflict)
                return Unavailable();

            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            return Unavailable();
        }
    }

    [HttpGet]
    public IActionResult Settings() => View();

    [HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Settings(string? currentPassword, string? newUsername,
        string? newPassword, string? confirmPassword, CancellationToken ct)
    {
        newUsername = string.IsNullOrWhiteSpace(newUsername) ? null : newUsername.Trim();
        if (currentPassword is not { Length: > 0 and <= AdminAccountRules.MaximumPasswordLength })
            ModelState.AddModelError("", "Vui lòng nhập mật khẩu hiện tại.");
        if (newUsername is not null && !AdminAccountRules.IsUsernameValid(newUsername))
            ModelState.AddModelError("", "Tên đăng nhập mới cần có tối đa 100 ký tự và không chứa ký tự điều khiển.");
        if ((newUsername is null || newUsername == User.Identity?.Name) && newPassword is null)
            ModelState.AddModelError("", "Hãy nhập tên đăng nhập mới hoặc mật khẩu mới.");
        if ((newPassword is not null || confirmPassword is not null)
            && (!AdminAccountRules.IsNewPasswordValid(newPassword) || newPassword != confirmPassword))
            ModelState.AddModelError("", "Mật khẩu mới cần từ 4 đến 1024 ký tự và hai ô phải giống nhau.");
        if (!ModelState.IsValid)
            return View();

        if (!Guid.TryParse(User.FindFirstValue(AdminAccountRules.VersionClaim), out var version))
            return await RequireNewLoginAsync("Hãy đăng nhập lại để đổi thông tin tài khoản.");

        try
        {
            var status = await accounts.ChangeAsync(new AdminAccountChange(version, currentPassword!, newUsername, newPassword), ct);
            switch (status)
            {
                case HttpStatusCode.NoContent:
                    return await RequireNewLoginAsync("Đã lưu thông tin tài khoản. Hãy đăng nhập lại.");
                case HttpStatusCode.Conflict:
                    return await RequireNewLoginAsync("Tài khoản vừa được cập nhật. Hãy đăng nhập lại.");
                case HttpStatusCode.Unauthorized:
                    ModelState.AddModelError("", "Mật khẩu hiện tại không đúng.");
                    return View();
                case HttpStatusCode.BadRequest:
                    ModelState.AddModelError("", "Thông tin mới chưa hợp lệ. Hãy kiểm tra lại.");
                    return View();
                default:
                    return Unavailable();
            }
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            return Unavailable();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    private async Task<AdminAccountStatus?> GetOrImportAccountAsync(CancellationToken ct)
    {
        var account = await accounts.GetStatusAsync(ct);
        if (account is not null)
            return account;

        var hash = configuration["Security:AdminPasswordHash"];
        if (string.IsNullOrWhiteSpace(hash) && environment.IsDevelopment())
            hash = LocalAdminSecrets.ReadPasswordHash();

        if (string.IsNullOrWhiteSpace(hash))
            return null;

        var status = await accounts.InitializeAsync(new AdminAccountSeed(InitialUsername, hash), ct);
        if (status is not (HttpStatusCode.NoContent or HttpStatusCode.Conflict))
            throw new HttpRequestException("Unable to import admin account.", null, status);

        return await accounts.GetStatusAsync(ct);
    }

    private IActionResult LoginPage(AdminAccountStatus? account, string? returnUrl)
    {
        if (account is null && !CanSetUp)
            return NotFound();

        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        ViewBag.Setup = account is null;
        ViewBag.Username = account?.Username ?? InitialUsername;
        return View("Login");
    }

    private async Task<IActionResult> RequireNewLoginAsync(string message)
    {
        await HttpContext.SignOutAsync();
        TempData["SetupComplete"] = message;
        return RedirectToAction(nameof(Login));
    }

    private static IActionResult Unavailable() => new ObjectResult(
        "Chưa kết nối được kho tài khoản. Kiểm tra API, kết nối SQL Server và bảng AdminAccounts.")
        { StatusCode = StatusCodes.Status503ServiceUnavailable };

    private static bool IsUnavailable(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested && error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException;
}
