// Dùng kiểu kết quả thống kê lượt vào cổng.
using AudioGuide.Core.DTOs;
// Dùng các thực thể nhật ký và phiên thiết bị.
using AudioGuide.Core.Entities;
// Dùng DbContext của dự án.
using AudioGuide.Infrastructure.Data;
// Dùng lớp thao tác dữ liệu lượt truy cập.
using AudioGuide.Infrastructure.Repositories;
// Dùng EF Core để kết nối, truy vấn và xóa dữ liệu kiểm thử.
using Microsoft.EntityFrameworkCore;
// Dùng các thuộc tính và phép xác nhận của xUnit.
using Xunit;

// Đặt các lớp bên dưới trong namespace của dự án kiểm thử.
namespace AudioGuide.Tests;

// Định nghĩa thuộc tính xUnit chỉ chạy khi đã cấu hình SQL Server kiểm thử.
public sealed class SqlFactAttribute : FactAttribute
{ // Bắt đầu khai báo thuộc tính SqlFact.
    // Kiểm tra cấu hình khi xUnit nhận diện bài test.
    public SqlFactAttribute()
    { // Bắt đầu hàm khởi tạo.
        // Nếu thiếu chuỗi kết nối, đánh dấu bài test là Skipped và nêu lý do.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")))
            // Báo biến môi trường phải trỏ vào database kiểm thử dùng riêng.
            Skip = "Set AUDIOGUIDE_TEST_SQL to a disposable SQL Server database.";
    } // Kết thúc hàm khởi tạo.
} // Kết thúc lớp SqlFactAttribute.

// Tập hợp các bài test chạy trên SQL Server thật.
public class SqlIntegrationTests
{ // Bắt đầu lớp kiểm thử.
    // Dùng thuộc tính tùy chỉnh để xUnit có thể bỏ qua bài test khi thiếu cấu hình.
    [SqlFact]
    // Kiểm tra chỉ đếm GateEntry theo UTC trong khoảng thời gian yêu cầu.
    public async Task HourlyGateQueryCountsOnlyUtcGateEventsInsideWindow()
    { // Bắt đầu bài kiểm thử thống kê.
        // Lấy chuỗi kết nối đã được kiểm tra có giá trị khi nhận diện test.
        var connection = Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!;
        // Cấu hình DbContext dùng SQL Server và hỗ trợ kiểu dữ liệu hình học.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection, s => s.UseNetTopologySuite()).Options;
        // Mở phạm vi sử dụng DbContext và tự giải phóng khi ra khỏi phạm vi.
        await using var db = new AppDbContext(options);
        // Tạo ID riêng để lúc dọn dữ liệu chỉ xóa dữ liệu của lần test này.
        var visitor = Guid.NewGuid().ToString("N");
        // Đặt mốc bắt đầu xa thời điểm hiện tại để hạn chế trùng với nhật ký khác.
        var start = DateTime.UtcNow.Date.AddYears(50).AddHours(17);
        // Đặt mốc kết thúc cuối khung kiểm thử.
        var end = start.AddDays(7).AddMinutes(-1);
        // Tạo hàm phụ nhận loại sự kiện, thời điểm và cờ UTC.
        QrScanLog Event(string type, DateTime time, bool utc = true) =>
            // Tạo mỗi sự kiện với ID riêng và cùng mã khách kiểm thử.
            new() { EventId = Guid.NewGuid(), VisitorId = visitor, EventType = type,
                    // Ghi thời điểm, cờ UTC và mã QR cổng.
                    ScannedAt = time, IsUtc = utc, QrToken = "MUSEUM-MAIN-ENTRY" };

        // Thêm GateEntry hợp lệ ở đầu khoảng và trong giờ 17 ngày tiếp theo.
        db.QrScanLogs.AddRange(Event("GateEntry", start), Event("GateEntry", start.AddDays(1).AddMinutes(59)),
            // Thêm sự kiện đúng mốc cuối và hai sự kiện nằm ngoài khoảng.
            Event("GateEntry", end), Event("GateEntry", start.AddSeconds(-1)), Event("GateEntry", end.AddSeconds(1)),
            // Thêm ba sự kiện bị loại vì cờ UTC hoặc loại sự kiện.
            Event("GateEntry", start.AddMinutes(10), false), Event("PlayStarted", start.AddMinutes(20)), Event("Legacy", start));
        // Lưu tập dữ liệu kiểm thử vào SQL Server.
        await db.SaveChangesAsync();
        // Thực hiện truy vấn và chuẩn bị dọn dữ liệu kể cả khi xác nhận thất bại.
        try
        { // Bắt đầu phần gọi repository.
            // Lấy số lượt vào cổng theo giờ trong khoảng đã định.
            var hours = await new VisitorRepository(db).HourlyGateEntriesAsync(start, end, default);
            // Sắp xếp theo giờ và kiểm tra kết quả gồm 1 lượt giờ 16, 2 lượt giờ 17.
            Assert.Equal(new[] { new HourlyGateEntries(16, 1), new HourlyGateEntries(17, 2) }, hours.OrderBy(row => row.Hour));
        } // Kết thúc phần xác nhận.
        // Dọn các hàng do bài test tạo.
        finally
        { // Bắt đầu phần dọn nhật ký.
            // Chỉ xóa nhật ký có VisitorId riêng của bài test này.
            await db.QrScanLogs.Where(e => e.VisitorId == visitor).ExecuteDeleteAsync();
        } // Kết thúc phần dọn nhật ký.
    } // Kết thúc bài kiểm thử thống kê.

    // Dùng cấu hình SQL Server riêng cho bài test chống đếm trùng và ghi đồng thời.
    [SqlFact]
    // Kiểm tra gửi lại cùng EventId và nhiều lượt ghi đồng thời.
    public async Task EventRetryAndConcurrentVisitorsDoNotLoseCounts()
    { // Bắt đầu bài kiểm thử gửi lại sự kiện.
        // Đọc chuỗi kết nối database test.
        var connection = Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!;
        // Chuẩn bị tùy chọn kết nối để từng thao tác dùng một DbContext riêng.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection, s => s.UseNetTopologySuite()).Options;
        // Dùng database riêng và ID thiết bị riêng để cô lập dữ liệu vừa ghi.
        var visitor = new VisitorIdentity(Guid.NewGuid().ToString("N"), "127.0.0.1", "Windows");
        // Tạo EventId sẽ được gửi hai lần để kiểm tra chống đếm trùng.
        var eventId = Guid.NewGuid();
        // Tạo hàm ghi một lượt vào cổng với EventId chỉ định.
        async Task Record(Guid id)
        { // Bắt đầu phạm vi của một thao tác ghi.
            // Tạo DbContext riêng để các thao tác đồng thời không dùng chung context.
            await using var db = new AppDbContext(options);
            // Ghi lượt vào cổng bằng repository thực của dự án.
            await new VisitorRepository(db).RecordAsync(visitor, id, "GateEntry", null, "vi", DateTime.UtcNow, default);
        } // Kết thúc hàm ghi một lượt.
        // Chuẩn bị dọn dữ liệu kể cả khi thao tác ghi hoặc xác nhận thất bại.
        try
        { // Bắt đầu phần ghi và xác nhận.
            // Ghi lần đầu cho EventId đã tạo.
            await Record(eventId);
            // Gửi lại đúng EventId để kiểm tra lượt này chỉ được tính một lần.
            await Record(eventId);
            // Gửi song song năm lượt độc lập với năm EventId mới.
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Record(Guid.NewGuid())));
            // Tạo DbContext để đọc kết quả lưu thực tế.
            await using var check = new AppDbContext(options);
            // Kiểm tra nhật ký có sáu lượt hợp lệ của thiết bị kiểm thử.
            Assert.Equal(6, await check.QrScanLogs.CountAsync(e => e.VisitorId == visitor.Id));
            // Kiểm tra bộ đếm trong phiên thiết bị cũng có giá trị sáu.
            Assert.Equal(6, (await check.DeviceSessions.SingleAsync(d => d.DeviceFingerprint == visitor.Id)).TotalScans);
        } // Kết thúc phần ghi và xác nhận.
        // Dọn cả nhật ký lẫn phiên thiết bị phát sinh từ bài test.
        finally
        { // Bắt đầu phần dọn dữ liệu.
            // Tạo DbContext mới cho thao tác dọn dữ liệu.
            await using var cleanup = new AppDbContext(options);
            // Xóa nhật ký theo đúng ID khách kiểm thử.
            await cleanup.QrScanLogs.Where(e => e.VisitorId == visitor.Id).ExecuteDeleteAsync();
            // Xóa phiên của đúng thiết bị kiểm thử.
            await cleanup.DeviceSessions.Where(d => d.DeviceFingerprint == visitor.Id).ExecuteDeleteAsync();
        } // Kết thúc phần dọn dữ liệu.
    } // Kết thúc bài kiểm thử gửi lại sự kiện.
} // Kết thúc lớp SqlIntegrationTests.
