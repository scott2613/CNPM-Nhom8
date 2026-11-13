using AudioGuide.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AudioGuide.API.Controllers;
[ApiController,Route("api/[controller]"),Authorize(Policy="Admin")]
public sealed class DevicesController(IVisitorService visitors,IAnalyticsService analytics) : ControllerBase {
 [HttpGet] public async Task<IActionResult> List()=>Ok(await visitors.DevicesAsync(HttpContext.RequestAborted));
 [HttpGet("presence"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
 public async Task<IActionResult> Presence(CancellationToken ct) => Ok(await visitors.DevicePresenceAsync(ct));
 [HttpPost("toggle-block/{id:int}")]public async Task<IActionResult> Toggle(int id){var state=await visitors.ToggleBlockAsync(id,HttpContext.RequestAborted);return state.HasValue?Ok(new{success=true,isBlocked=state.Value,message=state.Value?"Đã chặn thiết bị.":"Đã bỏ chặn thiết bị."}):NotFound(new{message="Không tìm thấy thiết bị."});}
 [HttpDelete("{id:int}")]public async Task<IActionResult> Delete(int id)=>await visitors.DeleteAsync(id,HttpContext.RequestAborted)?Ok(new{success=true,message="Đã xóa thiết bị và sự kiện liên quan."}):NotFound();
 [HttpPost("sync-devices")]public async Task<IActionResult> Refresh()=>Ok(new{success=true,totalDevices=(await visitors.DevicesAsync(HttpContext.RequestAborted)).Count,message="Đã làm mới danh sách; không suy đoán thiết bị từ IP."});
 [HttpGet("history")]public async Task<IActionResult> History()=>Ok(await analytics.HistoryAsync("24h",HttpContext.RequestAborted));
}
