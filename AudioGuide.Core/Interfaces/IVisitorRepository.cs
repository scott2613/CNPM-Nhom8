using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
namespace AudioGuide.Core.Interfaces;
public interface IVisitorRepository {
 Task<bool> IsBlockedAsync(string visitor,CancellationToken ct);
 Task TouchAsync(VisitorIdentity visitor,string language,DateTime now,CancellationToken ct);
 Task RecordAsync(VisitorIdentity visitor,Guid eventId,string type,Poi? poi,string language,DateTime now,CancellationToken ct);
 Task<List<DevicePresenceDto>> DevicePresenceAsync(CancellationToken ct);
 Task<List<DeviceDto>> DevicesAsync(CancellationToken ct);
 Task<bool?> ToggleBlockAsync(int id,CancellationToken ct);
 Task<bool> DeleteAsync(int id,CancellationToken ct);
 Task<List<QrScanLog>> EventsAsync(DateTime? from,DateTime until,CancellationToken ct);
 Task<List<HourlyGateEntries>> HourlyGateEntriesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
 Task<List<PoiInteractionCount>> PoiInteractionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
 Task<bool> ReadyAsync(CancellationToken ct);
}
public interface IPresenceStore {
 void Put(PresenceSession session);
 void Remove(string visitor,string session);
 IReadOnlyList<PresenceSession> Active(DateTime now);
 int TtlSeconds {get;}
}
