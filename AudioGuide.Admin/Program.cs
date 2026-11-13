using AudioGuide.Accounts;
using AudioGuide.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using AudioGuide.LocalDevelopment;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
namespace AudioGuide.Admin;
public class Program {
 public static void Main(string[] args){
  var builder=WebApplication.CreateBuilder(args);
  var api=builder.Configuration["ApiSettings:BaseUrl"]??throw new InvalidOperationException("Configure ApiSettings:BaseUrl.");
  var key=builder.Configuration["Security:AdminApiKey"]??"";
  if (string.IsNullOrWhiteSpace(key) && builder.Environment.IsDevelopment()
      && Uri.TryCreate(api, UriKind.Absolute, out var localApi) && localApi.IsLoopback)
      key = LocalAdminSecrets.ReadOrCreateApiKey();
  if(key.Length<32)throw new InvalidOperationException("Configure Security:AdminApiKey (same server key as API).");
  builder.Services.AddScoped<AdminAccountClient>();
  builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
   o.LoginPath="/Account/Login";o.AccessDeniedPath="/Account/Login";o.Cookie.Name="AG_ADMIN";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
   o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
   o.ExpireTimeSpan=TimeSpan.FromHours(2);o.SlidingExpiration=false;
   o.Events.OnValidatePrincipal = async ctx =>
   {
    var version = ctx.Principal?.FindFirst(AdminAccountRules.VersionClaim)?.Value;
    if (Guid.TryParse(version, out var issuedVersion))
    {
     var ct = ctx.HttpContext.RequestAborted;
     try
     {
      var accounts = ctx.HttpContext.RequestServices.GetRequiredService<AdminAccountClient>();
      var current = await accounts.GetStatusAsync(ct);
      if (current?.Version == issuedVersion) return;
     }
     catch (Exception error) when (!ct.IsCancellationRequested &&
         error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
     {
      ctx.RejectPrincipal();
      return;
     }
    }
    ctx.RejectPrincipal();
    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
   };
   o.Events.OnRedirectToLogin = ctx =>
   {
    if (ctx.Request.Headers.Accept.ToString().Contains("application/json") || ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
    {
     ctx.Response.StatusCode = 401;
     ctx.Response.Headers["X-Admin-Login-Required"] = "1";
    }
    else ctx.Response.Redirect(ctx.RedirectUri);
    return Task.CompletedTask;
   };
  });
  builder.Services.AddAuthorization();
  builder.Services.AddControllersWithViews(o=>{o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());});
  builder.Services.AddAntiforgery(o=>o.HeaderName="X-CSRF-TOKEN");
  builder.Services.AddDataProtection().SetApplicationName("AudioGuide.Admin");
  var path=builder.Configuration["DataProtection:KeyPath"];if(!string.IsNullOrEmpty(path))builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(path));
  builder.Services.AddHttpClient("AudioGuideApiClient",c=>{c.BaseAddress=new Uri(api.TrimEnd('/')+"/");c.DefaultRequestHeaders.Add("Accept","application/json");c.DefaultRequestHeaders.Add("X-Admin-Key",key);c.Timeout=TimeSpan.FromSeconds(90);});
  builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=8,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
  var app=builder.Build();app.UseExceptionHandler("/Home/Error");if(!app.Environment.IsDevelopment())app.UseHsts();
  app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
  app.MapGet("/health/live",()=>Results.Ok(new{status="Healthy"})).AllowAnonymous();
  app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");app.Run();
 }
}
