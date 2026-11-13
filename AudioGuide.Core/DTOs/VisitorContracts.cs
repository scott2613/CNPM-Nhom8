using System.ComponentModel.DataAnnotations;
namespace AudioGuide.Core.DTOs;
public sealed record VisitorIdentity(string Id, string Ip, string UserAgent);
public sealed class HeartbeatRequest {
 [Required, StringLength(80), RegularExpression(@"^[A-Za-z0-9_-]+$")] public string SessionId {get;set;} = "";
 [StringLength(50)] public string? PoiCode {get;set;}
 public bool IsPlaying {get;set;}
 [StringLength(10)] public string LanguageCode {get;set;} = "vi";
 public bool Preview {get;set;}
}
public sealed class EventRequest {
 public Guid EventId {get;set;}
 [StringLength(10)] public string LanguageCode {get;set;} = "vi";
 public bool Preview {get;set;}
}
public sealed record PresenceSession(string VisitorId,string SessionId,string PoiCode,string PoiName,bool IsPlaying,string IpAddress,string Device,string LanguageCode,DateTime LastSeen);
public sealed record DeviceDto(int Id,string DeviceFingerprint,string IpAddress,string DeviceName,string Platform,string Browser,int TotalScans,bool IsBlocked,DateTime LastActiveAt,DateTime CreatedAt,bool IsOnline);
public sealed record DevicePresenceDto(int Id, string DeviceFingerprint, bool IsBlocked, bool IsOnline);
public sealed record HistoryDetail(string PoiCode,string PoiName,int Count);
public sealed record HistoryGroup(string IpAddress,string Device,int TotalScans,string LastAccess,List<HistoryDetail> Details);
public sealed record RealtimeLog(string Id,string PoiCode,string PoiName,string IpAddress,string Device,string Lang,string ScannedAt);
public sealed record RealtimeMetrics(int TotalScansToday,int ActiveUsersNow,string TopPoi,string PeakHour,List<RealtimeLog> Logs,int EntryCountToday);
public sealed record HourlyGateEntries(int Hour, long Count);
public sealed record GateEntryHour(int Hour, long Count, long PreviousCount);
public sealed record GateEntryReport(DateTime FromUtc, DateTime ToUtc, IReadOnlyList<GateEntryHour> Hours,
    DateTime PreviousFromUtc, DateTime PreviousToUtc);
public sealed record PoiInteractionCount(int PoiId, long ViewCount, long PlayCount);
public sealed record PoiInteractionRow(string Code, string Title, long ViewCount, long PlayCount,
    long PreviousViewCount = 0, long PreviousPlayCount = 0);
public sealed record PoiInteractionReport(DateTime FromUtc, DateTime ToUtc, IReadOnlyList<PoiInteractionRow> Locations,
    DateTime PreviousFromUtc, DateTime PreviousToUtc);
public sealed record SavePoiResult(bool Found);
public sealed record PoiMapItem(int Id,string Code,int Floor,double Lat,double Lng,string Title,string Desc,string? ImageUrl,string LanguageCode);
public sealed class DomainException(int status,string message) : Exception(message) { public int Status {get;}=status; }
