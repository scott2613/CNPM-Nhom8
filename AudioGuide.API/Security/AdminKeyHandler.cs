using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
namespace AudioGuide.API.Security;
public sealed class AdminKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,IConfiguration configuration)
 : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
 protected override Task<AuthenticateResult> HandleAuthenticateAsync(){
  var actual=Request.Headers["X-Admin-Key"].ToString();var expected=configuration["Security:AdminApiKey"]??"";
  if(actual.Length==0)return Task.FromResult(AuthenticateResult.NoResult());
  if(expected.Length<32||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(actual)),SHA256.HashData(Encoding.UTF8.GetBytes(expected))))return Task.FromResult(AuthenticateResult.Fail("Invalid key"));
  var identity=new ClaimsIdentity([new Claim(ClaimTypes.Name,"AdminServer"),new Claim(ClaimTypes.Role,"Admin")],Scheme.Name);
  return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
 }
}
