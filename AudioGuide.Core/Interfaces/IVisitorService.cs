using AudioGuide.Core.DTOs;
namespace AudioGuide.Core.Interfaces;
public interface IVisitorService {
 Task RecordAsync(VisitorIdentity visitor,EventRequest request,string? poiCode,CancellationToken ct);
 Task RecordPoiViewAsync(VisitorIdentity visitor, EventRequest request, string poiCode, CancellationToken ct);
 Task HeartbeatAsync(VisitorIdentity visitor,HeartbeatRequest request,CancellationToken ct);
 void Disconnect(string visitor,string session);
 Task<List<DevicePresenceDto>> DevicePresenceAsync(CancellationToken ct);
 Task<List<DeviceDto>> DevicesAsync(CancellationToken ct);
 Task<bool?> ToggleBlockAsync(int id,CancellationToken ct);
 Task<bool> DeleteAsync(int id,CancellationToken ct);
 Task<bool> IsBlockedAsync(string id,CancellationToken ct);
}
public interface IAnalyticsService {
 Task<List<HistoryGroup>> HistoryAsync(string range,CancellationToken ct);
 Task<RealtimeMetrics> RealtimeAsync(CancellationToken ct);
 Task<GateEntryReport> GateEntriesAsync(string range, CancellationToken ct);
 Task<PoiInteractionReport> PoiInteractionsAsync(string range, CancellationToken ct);
 Task<bool> ReadyAsync(CancellationToken ct);
}
