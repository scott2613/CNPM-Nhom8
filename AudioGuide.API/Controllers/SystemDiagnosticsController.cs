using System.Diagnostics;
using AudioGuide.Core.Interfaces;
using AudioGuide.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize(Policy = "Admin")]
public sealed class SystemDiagnosticsController(IAnalyticsService service, IPoiRepository pois,
    AppDbContext context, IWebHostEnvironment environment, ILogger<SystemDiagnosticsController> logger) : ControllerBase
{
    [HttpGet("health-check")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var connection = context.Database.GetDbConnection();
        var connected = false;
        int? count = null;
        string? error = null;
        try
        {
            connected = await service.ReadyAsync(cancellationToken);
            if (connected)
            {
                var schemaReady = await context.Database.SqlQueryRaw<int>("""
                    SELECT CASE WHEN COL_LENGTH('dbo.Pois', 'RowVersion') IS NOT NULL
                        AND COL_LENGTH('dbo.QrScanLogs', 'EventType') IS NOT NULL
                        AND COL_LENGTH('dbo.QrScanLogs', 'IsUtc') IS NOT NULL
                        AND COL_LENGTH('dbo.QrScanLogs', 'VisitorId') IS NOT NULL
                        AND COL_LENGTH('dbo.DeviceSessions', 'PreferredLanguage') IS NOT NULL
                        THEN 1 ELSE 0 END AS [Value]
                    """).SingleAsync(cancellationToken) == 1;
                if (schemaReady) count = await pois.CountAsync(cancellationToken);
                else error = "Đã kết nối SQL Server nhưng cấu trúc bảng còn thiếu. Hãy đối chiếu với AudioGuideDB.sql.";
            }
            else error = "Không kết nối được cơ sở dữ liệu.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không đọc được thông tin cơ sở dữ liệu.");
            error = connected ? "Đã kết nối SQL Server nhưng truy vấn kiểm tra thất bại." : "Không kết nối được cơ sở dữ liệu.";
        }
        return Ok(new
        {
            timestamp = DateTime.UtcNow.AddHours(7).ToString("HH:mm:ss dd/MM/yyyy"),
            api = new { status = "Online", host = Request.Host.Value, scheme = Request.Scheme, environment = environment.EnvironmentName },
            database = new
            {
                status = count.HasValue ? "Connected" : connected ? "QueryFailed" : "Disconnected",
                server = connection.DataSource, database = connection.Database,
                currentPoiCount = count, responseTimeMs = timer.ElapsedMilliseconds, error
            }
        });
    }
}
