using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
using AudioGuide.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
namespace AudioGuide.API.Controllers;
[ApiController,Route("api/[controller]"),Authorize(Policy="Admin")]
public sealed class AnalyticsController(IPoiManagementService pois,IAnalyticsService analytics,IVisitorService visitors) : ControllerBase {
 [HttpGet("pois-admin")] public async Task<IActionResult> Pois()=>Ok(await pois.GetPoisForAdminAsync(HttpContext.RequestAborted));
 [HttpGet("user-access-history")] public async Task<IActionResult> History(string range="24h")=>Ok(await analytics.HistoryAsync(range,HttpContext.RequestAborted));
 [HttpGet("realtime-metrics")] public async Task<IActionResult> Realtime()=>Ok(await analytics.RealtimeAsync(HttpContext.RequestAborted));
 [HttpGet("gate-entries-by-hour")]
 [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
 public async Task<IActionResult> GateEntriesByHour(string range = "1d") =>
  Ok(await analytics.GateEntriesAsync(range, HttpContext.RequestAborted));
 [HttpGet("poi-interactions")]
 [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
 public async Task<IActionResult> PoiInteractions(string range = "1d") =>
  Ok(await analytics.PoiInteractionsAsync(range, HttpContext.RequestAborted));
 [HttpPost("save-poi")] public async Task<IActionResult> Save(PoiSaveDto request){var result=await pois.SavePoiAsync(request,HttpContext.RequestAborted);return result.Found?Ok(new{success=true,message="Đã lưu địa điểm với 5 ngôn ngữ: Việt, Anh, Trung, Pháp và Nga."}):NotFound();}
 [HttpPost("toggle-status/{id:int}")] public async Task<IActionResult> Toggle(int id){var state=await pois.ToggleStatusAsync(id,HttpContext.RequestAborted);return state.HasValue?Ok(new{success=true,isActive=state.Value}):NotFound();}
 [HttpDelete("delete-poi/{id:int}")] public async Task<IActionResult> Delete(int id)=>await pois.DeletePoiAsync(id,HttpContext.RequestAborted)?Ok(new{success=true,message="Đã xóa POI; giữ lịch sử thống kê."}):NotFound();
 [HttpPost("sync-system")] public async Task<IActionResult> Refresh()=>Ok(new{success=true,totalPois=await pois.SyncSystemPoisAsync(HttpContext.RequestAborted),message="Đã làm mới danh mục. Không thay đổi nội dung hoặc sinh QR từng POI."});
 [AllowAnonymous,EnableRateLimiting("visitor"),HttpPost("heartbeat")]
 public async Task<IActionResult> Heartbeat(HeartbeatRequest request){await visitors.HeartbeatAsync(HttpContext.Visitor(),request,HttpContext.RequestAborted);return Ok();}
 [AllowAnonymous,EnableRateLimiting("visitor"),HttpPost("disconnect")]
 public IActionResult Disconnect(HeartbeatRequest request){visitors.Disconnect(HttpContext.Visitor().Id,request.SessionId);return Ok();}
}
