using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
using AudioGuide.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace AudioGuide.API.Controllers;
[ApiController,Route("api/[controller]"),EnableRateLimiting("visitor")]
public sealed class GeoGuideController(IAudioGuideService guide,IVisitorService visitors,TimeProvider clock) : ControllerBase {
 [HttpPost("locate")]public async Task<IActionResult> Locate(GpsLocationRequest request){
  if(await visitors.IsBlockedAsync(HttpContext.Visitor().Id,HttpContext.RequestAborted))return StatusCode(403);
  if(request.SampledAt==null||Math.Abs((clock.GetUtcNow()-request.SampledAt.Value).TotalSeconds)>30)return BadRequest(new{message="Vị trí đã hết hạn."});
  if(!double.IsFinite(request.AccuracyMeters)||request.AccuracyMeters>30)return Ok(new{poi=(object?)null,message="Độ chính xác GPS chưa đủ. Hãy chọn POI trên bản đồ."});
  request.LanguageCode??=Request.Headers.AcceptLanguage.ToString();
  var result=await guide.ResolveGpsLocationAsync(request,HttpContext.RequestAborted);
  return result==null?Ok(new{poi=(object?)null}):Ok(result);
 }
}
