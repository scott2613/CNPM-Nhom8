using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
using AudioGuide.Core.Services;
using AudioGuide.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;
using System.Text.Json;
namespace AudioGuide.API.Controllers;
[ApiController,Route("api/[controller]"),EnableRateLimiting("visitor")]
public sealed class QrGuideController(IAudioGuideService guide,IVisitorService visitors,IWebHostEnvironment environment,IConfiguration configuration) : ControllerBase {
 [HttpGet("scan/{token}")]
 public async Task<IActionResult> Scan(string token,string? lang="vi",bool preview=false){
  if(await visitors.IsBlockedAsync(HttpContext.Visitor().Id,HttpContext.RequestAborted))return StatusCode(403,new{message="Thiết bị đã bị chặn."});
  var language=LanguagePolicy.Normalize(lang);var pois=await guide.MapAsync(token,language,HttpContext.RequestAborted);
  var html=await System.IO.File.ReadAllTextAsync(Path.Combine(environment.WebRootPath,"templates","QrGuideMap.html"),HttpContext.RequestAborted);
  html=html.Replace("{{POIS_JSON}}",JsonSerializer.Serialize(pois,new JsonSerializerOptions(JsonSerializerDefaults.Web)))
   .Replace("{{TARGET_LANG}}",language).Replace("{{VOICE_LANG}}",language=="zh"?"zh-CN":language=="en"?"en-US":language+"-"+language.ToUpperInvariant()).Replace("{{UI_BADGE}}","🏛️ DI TÍCH SÀI GÒN");
  foreach(var l in LanguagePolicy.Supported)html=html.Replace("{{"+l.ToUpperInvariant()+"_SELECTED}}",l==language?"selected":"");
  Response.Headers.CacheControl="no-store";return Content(html,"text/html; charset=utf-8");
 }
 [HttpPost("entry")]
 public async Task<IActionResult> Entry(EventRequest request){await visitors.RecordAsync(HttpContext.Visitor(),request,null,HttpContext.RequestAborted);return Ok(new{success=true});}
 [HttpPost("log-view/{token}")]
 public async Task<IActionResult> LogView(string token,EventRequest request){await visitors.RecordAsync(HttpContext.Visitor(),request,token,HttpContext.RequestAborted);return Ok(new{success=true});}
 [HttpPost("poi-view/{code}")]
 public async Task<IActionResult> PoiView(string code, EventRequest request)
 {
  await visitors.RecordPoiViewAsync(HttpContext.Visitor(), request, code, HttpContext.RequestAborted);
  return Ok(new { success = true });
 }
 [HttpGet("generate/{idOrToken}")]
 public IActionResult GenerateQr(string idOrToken){
  if(idOrToken!="MUSEUM-MAIN-ENTRY")return NotFound(new{message="Hệ thống chỉ có một QR cổng."});
  var url=configuration["PublicBaseUrl"]!.TrimEnd('/')+"/api/QrGuide/scan/MUSEUM-MAIN-ENTRY";
  using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(url,QRCodeGenerator.ECCLevel.M);using var qr=new PngByteQRCode(data);
  return File(qr.GetGraphic(20),"image/png");
 }
}
