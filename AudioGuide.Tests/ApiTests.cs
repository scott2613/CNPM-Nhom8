using AudioGuide.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace AudioGuide.Tests;
public sealed class ApiFactory:WebApplicationFactory<Program> {
 public const string TunnelOrigin = "https://nq88b866-7084.asse.devtunnels.ms";
 public ApiFactory(){Environment.SetEnvironmentVariable("Security__AdminApiKey",new string('x',48));Environment.SetEnvironmentVariable("PublicBaseUrl",TunnelOrigin);}

 protected override void ConfigureWebHost(IWebHostBuilder builder){
  builder.UseEnvironment("Development");
  builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{{"Security:AdminApiKey",new string('x',48)},{"PublicBaseUrl",TunnelOrigin},{"ConnectionStrings:DefaultConnection","Server=localhost;Database=unused;Trusted_Connection=True"}}));
  builder.ConfigureServices(s=>{s.RemoveAll<IPoiRepository>();s.RemoveAll<IVisitorRepository>();s.AddSingleton<IPoiRepository,FakePois>();s.AddSingleton<IVisitorRepository,FakeVisitors>();});
 }
}
public class ApiTests:IClassFixture<ApiFactory> {
 private readonly HttpClient client;
 public ApiTests(ApiFactory factory){client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});}
 [Theory]
 [InlineData(ApiFactory.TunnelOrigin)]
 [InlineData("http://localhost")]
 public async Task QrVisitorAppearsInRealtimeAndDisconnects(string origin)
 {
  var session = Guid.NewGuid().ToString("N");
  using var map = await client.GetAsync(origin + "/api/QrGuide/scan/MUSEUM-MAIN-ENTRY");
  Assert.Equal(HttpStatusCode.OK, map.StatusCode);
  Assert.Contains("AG_VISITOR=", Assert.Single(map.Headers.GetValues("Set-Cookie")));
  async Task<HttpStatusCode> SendAsync(string endpoint, bool preview)
  {
   using var request = new HttpRequestMessage(HttpMethod.Post, origin + "/api/analytics/" + endpoint)
   { Content = JsonContent.Create(new { sessionId = session, poiCode = "MAP_VIEW", preview }) };
   request.Headers.Add("Origin", origin);
   request.Headers.Add("User-Agent", "Mozilla/5.0 Android");
   using var response = await client.SendAsync(request);
   return response.StatusCode;
  }
  async Task<AudioGuide.Core.DTOs.RealtimeMetrics> ReadAsync()
  {
   using var request = new HttpRequestMessage(HttpMethod.Get, "/api/analytics/realtime-metrics");
   request.Headers.Add("X-Admin-Key", new string('x', 48));
   using var response = await client.SendAsync(request);
   response.EnsureSuccessStatusCode();
   return (await response.Content.ReadFromJsonAsync<AudioGuide.Core.DTOs.RealtimeMetrics>())!;
  }
  Assert.Equal(HttpStatusCode.OK, await SendAsync("heartbeat", true));
  Assert.DoesNotContain((await ReadAsync()).Logs, row => row.Id == session);
  Assert.Equal(HttpStatusCode.OK, await SendAsync("heartbeat", false));
  Assert.Contains((await ReadAsync()).Logs, row => row.Id == session && row.Device == "Android" && row.PoiCode == "MAP_VIEW");
  Assert.Equal(HttpStatusCode.OK, await SendAsync("disconnect", false));
  Assert.DoesNotContain((await ReadAsync()).Logs, row => row.Id == session);
 }
 [Fact]
 public async Task PoiViewAcceptsVisitorAndPoiReportRequiresAdmin()
 {
  using var recorded = await client.PostAsJsonAsync("/api/QrGuide/poi-view/POI-1", new { eventId = Guid.NewGuid() });
  Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
  const string path = "/api/analytics/poi-interactions?range=30d";
  using var unauthorized = await client.GetAsync(path);
  Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
  using var request = new HttpRequestMessage(HttpMethod.Get, path);
  request.Headers.Add("X-Admin-Key", new string('x', 48));
  using var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  var report = await response.Content.ReadFromJsonAsync<AudioGuide.Core.DTOs.PoiInteractionReport>();
  Assert.NotNull(report);
  Assert.Equal("POI-1", Assert.Single(report.Locations).Code);
 }
 [Fact]
 public async Task GateEntryChartIsProtectedAndHandlesEmptyAndInvalidRanges()
 {
  const string path = "/api/analytics/gate-entries-by-hour";
  using var unauthorized = await client.GetAsync(path);
  Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
  using var request = new HttpRequestMessage(HttpMethod.Get, path + "?range=30d");
  request.Headers.Add("X-Admin-Key", new string('x', 48));
  using var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  Assert.True(response.Headers.CacheControl?.NoStore);
  var report = await response.Content.ReadFromJsonAsync<AudioGuide.Core.DTOs.GateEntryReport>();
  Assert.NotNull(report);
  Assert.Equal(24, report.Hours.Count);
  Assert.All(report.Hours, row => Assert.Equal(0, row.Count));
  using var invalid = new HttpRequestMessage(HttpMethod.Get, path + "?range=invalid");
  invalid.Headers.Add("X-Admin-Key", new string('x', 48));
  using var invalidResponse = await client.SendAsync(invalid);
  Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
  Assert.Contains("Hãy chọn khoảng thống kê", await invalidResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
 }
 [Fact]
 public async Task PresenceEndpointRequiresAdminAndReturnsJson()
 {
  Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/presence")).StatusCode);
  using var request = new HttpRequestMessage(HttpMethod.Get, "/api/devices/presence");
  request.Headers.Add("X-Admin-Key", new string('x', 48));
  using var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
  Assert.Empty((await response.Content.ReadFromJsonAsync<List<AudioGuide.Core.DTOs.DevicePresenceDto>>())!);
 }
 [Theory]
 [InlineData("GET", "/api/admin-account")]
 [InlineData("POST", "/api/admin-account/initialize")]
 [InlineData("POST", "/api/admin-account/login")]
 [InlineData("PUT", "/api/admin-account")]
 public async Task SharedAccountEndpointsRequireServerKey(string method, string path)
 {
  using var request = new HttpRequestMessage(new HttpMethod(method), path);
  using var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
 }
 [Fact]public async Task AdminReadRequiresAuthentication()=>Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/devices")).StatusCode);
 [Fact]public async Task AdminWriteRequiresAuthentication()=>Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/analytics/sync-system",null)).StatusCode);
 [Fact]public async Task ServerKeyAllowsAdmin(){using var request=new HttpRequestMessage(HttpMethod.Get,"/api/devices");request.Headers.Add("X-Admin-Key",new string('x',48));Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(request)).StatusCode);}
 [Fact]public async Task SpoofedKeyRejected(){using var request=new HttpRequestMessage(HttpMethod.Get,"/api/devices");request.Headers.Add("X-Admin-Key","wrong");Assert.Equal(HttpStatusCode.Unauthorized,(await client.SendAsync(request)).StatusCode);}
 [Fact]public async Task UnknownGateReturns404()=>Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/QrGuide/scan/museum-fake")).StatusCode);
 [Fact]public async Task GateReturnsPreservedMap(){var response=await client.GetAsync("/api/QrGuide/scan/MUSEUM-MAIN-ENTRY?preview=true");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("glassPlayerBox",await response.Content.ReadAsStringAsync());}
 [Fact]public async Task NoPerPoiQr()=>Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/QrGuide/generate/POI-1")).StatusCode);
 [Fact]public async Task ReadinessWorks()=>Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
 [Fact]public async Task MissingEventIdReturns400()=>Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/QrGuide/entry",new{eventId=Guid.Empty})).StatusCode);
 [Fact]public async Task CrossSiteMutationRejected(){using var request=new HttpRequestMessage(HttpMethod.Post,"/api/QrGuide/entry"){Content=JsonContent.Create(new{eventId=Guid.NewGuid()})};request.Headers.Add("Origin","https://untrusted.example");Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(request)).StatusCode);}
}
