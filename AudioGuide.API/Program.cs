using AudioGuide.LocalDevelopment;
using AudioGuide.API.Errors;
using AudioGuide.API.Middleware;
using AudioGuide.API.Security;
using AudioGuide.Core.Interfaces;
using AudioGuide.Core.Services;
using AudioGuide.Infrastructure.Data;
using AudioGuide.Infrastructure.Repositories;
using AudioGuide.Infrastructure.Presence;
using AudioGuide.Infrastructure.Translation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using System.Net;
var builder=WebApplication.CreateBuilder(args);
var connection=builder.Configuration.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
var key=builder.Configuration["Security:AdminApiKey"];
if (string.IsNullOrWhiteSpace(key) && builder.Environment.IsDevelopment())
    builder.Configuration["Security:AdminApiKey"] = key = LocalAdminSecrets.ReadOrCreateApiKey();
if(string.IsNullOrWhiteSpace(key)||key.Length<32)throw new InvalidOperationException("Configure Security:AdminApiKey with at least 32 random characters.");
if(!Uri.TryCreate(builder.Configuration["PublicBaseUrl"],UriKind.Absolute,out var publicUrl)||publicUrl.Scheme is not ("https" or "http"))throw new InvalidOperationException("Configure PublicBaseUrl.");
if(!builder.Environment.IsDevelopment()&&publicUrl.Scheme!="https")throw new InvalidOperationException("PublicBaseUrl must use HTTPS in production.");
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(connection,s=>s.UseNetTopologySuite()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPoiRepository,PoiRepository>();builder.Services.AddScoped<IVisitorRepository,VisitorRepository>();
builder.Services.AddScoped<IAudioGuideService,AudioGuideService>();builder.Services.AddScoped<IPoiManagementService,PoiManagementService>();
builder.Services.AddScoped<IVisitorService,VisitorService>();builder.Services.AddScoped<IAnalyticsService,AnalyticsService>();
builder.Services.AddOptions<PresenceOptions>().BindConfiguration("Presence").Validate(o=>o.TtlSeconds>=10&&o.TtlSeconds<=120&&o.MaxSessions>0).ValidateOnStart();
builder.Services.AddSingleton<IPresenceStore,InMemoryPresenceStore>();
builder.Services.AddHttpClient<ITranslationService,TranslationService>(c=>c.Timeout=TimeSpan.FromSeconds(8));
builder.Services.AddDataProtection().SetApplicationName("AudioGuide.API");
var keys=builder.Configuration["DataProtection:KeyPath"];if(!string.IsNullOrWhiteSpace(keys))builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddAuthentication("AdminKey").AddScheme<AuthenticationSchemeOptions,AdminKeyHandler>("AdminKey",_=>{});
builder.Services.AddAuthorization(o=>o.AddPolicy("Admin",p=>p.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.Configure<ForwardedHeadersOptions>(o=>{
 o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
 foreach(var ip in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>()??[])o.KnownProxies.Add(IPAddress.Parse(ip));
});
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("visitor",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=180,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();builder.Services.AddProblemDetails();builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
var app=builder.Build();
var publicOrigin = publicUrl.GetLeftPart(UriPartial.Authority);
app.UseForwardedHeaders();app.UseExceptionHandler();
if(!app.Environment.IsDevelopment())app.UseHsts();
app.Use(async(ctx,next)=>{ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="same-origin";
 // Visitor mutations are same-origin. Cross-site cookies are never an admin credential.
 if (!HttpMethods.IsGet(ctx.Request.Method) && ctx.Request.Headers.TryGetValue("Origin", out var header))
 {
  var origin = header.ToString();
  var requestOrigin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
  var localDevelopment = app.Environment.IsDevelopment()
   && Uri.TryCreate(requestOrigin, UriKind.Absolute, out var localUrl) && localUrl.IsLoopback
   && string.Equals(origin, localUrl.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
  if (!localDevelopment && !string.Equals(origin, publicOrigin, StringComparison.OrdinalIgnoreCase))
  {
   await Results.Problem(statusCode: 403, detail: "Địa chỉ đang mở bản đồ chưa khớp PublicBaseUrl của API.").ExecuteAsync(ctx);
   return;
  }
 }
 await next();});
app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<VisitorIdentityMiddleware>();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.MapGet("/",()=>Results.Redirect("/AudioGuideHub.html"));app.MapGet("/hub",()=>Results.Redirect("/AudioGuideHub.html"));
app.MapGet("/health/live",()=>Results.Ok(new {status="Healthy"}));app.MapGet("/health",()=>Results.Ok(new {status="Healthy"}));
app.MapGet("/health/ready",async(IAnalyticsService service,CancellationToken ct)=>{try{return await service.ReadyAsync(ct)?Results.Ok(new {status="Healthy"}):Results.StatusCode(503);}catch{return Results.StatusCode(503);}});
app.MapControllers();app.Run();
public partial class Program {}
