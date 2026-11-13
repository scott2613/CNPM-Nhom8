using AudioGuide.Core.DTOs;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
namespace AudioGuide.API.Middleware;
public sealed class VisitorIdentityMiddleware(RequestDelegate next,IDataProtectionProvider provider) {
 private readonly IDataProtector protector=provider.CreateProtector("AudioGuide.Visitor.v1");
 public async Task InvokeAsync(HttpContext context){
  var path=context.Request.Path;
  if(path.StartsWithSegments("/api/QrGuide")||path.StartsWithSegments("/api/GeoGuide")||path=="/api/analytics/heartbeat"||path=="/api/analytics/disconnect"){
   string? id=null;
   if(context.Request.Cookies.TryGetValue("AG_VISITOR",out var cookie))try{id=protector.Unprotect(cookie);}catch(CryptographicException){}
   if(!Guid.TryParseExact(id,"N",out _)){
    id=Guid.NewGuid().ToString("N");context.Response.Cookies.Append("AG_VISITOR",protector.Protect(id),new CookieOptions{HttpOnly=true,Secure=context.Request.IsHttps,SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromDays(180),IsEssential=true});
   }
   var ua=context.Request.Headers.UserAgent.ToString();if(ua.Length>250)ua=ua[..250];
   context.Items["Visitor"]=new VisitorIdentity(id!,context.Connection.RemoteIpAddress?.ToString()??"unknown",ua);
  }
  await next(context);
 }
}
public static class VisitorContext {public static VisitorIdentity Visitor(this HttpContext context)=>(VisitorIdentity)context.Items["Visitor"]!;}
