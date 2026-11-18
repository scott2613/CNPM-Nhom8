using AudioGuide.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.API.Controllers;

[ApiController]
[Route("api/qr-access")]
public sealed class QrAccessController(QrAccessWindow window, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { window = window.Status(), canControl = CanControl() });
    }

    [HttpPost("open")]
    public IActionResult Open()
    {
        if (!CanControl()) return StatusCode(403, new { message = "Chức năng mở cổng cần quyền quản trị." });
        Response.Headers.CacheControl = "no-store";
        return Ok(window.Open());
    }

    [HttpPost("simulate")]
    public IActionResult Simulate()
    {
        if (!CanControl()) return StatusCode(403, new { message = "Chức năng mô phỏng cần quyền quản trị." });
        var start = DateTimeOffset.UtcNow;
        var results = new[] { 0, 30, 59, 60, 61 }.Select(seconds =>
        {
            var status = QrAccessWindow.Describe(start, start.AddSeconds(seconds));
            return new { elapsedSeconds = seconds, status.CanAccess, status.RemainingSeconds,
                expectedHttpStatus = status.CanAccess ? 200 : 403 };
        });
        return Ok(new { message = "Mô phỏng mốc thời gian; cổng thật giữ thời hạn đang chạy.", results });
    }

    private bool CanControl() => environment.IsDevelopment()
        || (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"));
}
