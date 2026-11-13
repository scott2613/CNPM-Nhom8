using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
namespace AudioGuide.Core.Services;
public sealed class VisitorService(IVisitorRepository repository,IPoiRepository pois,IPresenceStore presence,TimeProvider clock) : IVisitorService {
 public Task RecordAsync(VisitorIdentity visitor, EventRequest request, string? poiCode, CancellationToken ct) =>
  RecordEventAsync(visitor, request, poiCode, poiCode == null ? "GateEntry" : "PlayStarted", ct);

 public Task RecordPoiViewAsync(VisitorIdentity visitor, EventRequest request, string poiCode, CancellationToken ct) =>
  RecordEventAsync(visitor, request, poiCode, "PoiViewed", ct);

 private async Task RecordEventAsync(VisitorIdentity visitor, EventRequest request, string? poiCode, string type, CancellationToken ct) {
  if(request.Preview)return;
  if(request.EventId==Guid.Empty)throw new DomainException(400,"EventId là bắt buộc.");
  if(await repository.IsBlockedAsync(visitor.Id,ct))throw new DomainException(403,"Thiết bị bị chặn.");
  var poi=poiCode==null?null:await pois.FindCodeAsync(poiCode,ct);
  if(poiCode!=null&&(poi==null||!poi.IsActive))throw new DomainException(404,"POI không hoạt động.");
  if(poiCode==null&&!await pois.GateEnabledAsync("MUSEUM-MAIN-ENTRY",ct))throw new DomainException(404,"Cổng chưa hoạt động.");
  await repository.RecordAsync(visitor,request.EventId,type,poi,LanguagePolicy.Normalize(request.LanguageCode),clock.GetUtcNow().UtcDateTime,ct);
 }
 public async Task HeartbeatAsync(VisitorIdentity visitor,HeartbeatRequest request,CancellationToken ct) {
  if(request.Preview)return;
  if(await repository.IsBlockedAsync(visitor.Id,ct))throw new DomainException(403,"Thiết bị bị chặn.");
  var poi=string.IsNullOrEmpty(request.PoiCode)||request.PoiCode=="MAP_VIEW"?null:await pois.FindCodeAsync(request.PoiCode,ct);
  var lang=LanguagePolicy.Normalize(request.LanguageCode);var now=clock.GetUtcNow().UtcDateTime;
  var title=poi?.Translations.FirstOrDefault(t=>t.LanguageCode==lang)?.Title??poi?.Translations.FirstOrDefault(t=>t.LanguageCode=="vi")?.Title??"Đang xem bản đồ";
  await repository.TouchAsync(visitor,lang,now,ct);
  presence.Put(new(visitor.Id,request.SessionId,poi?.Code??"MAP_VIEW",title,poi?.IsActive==true&&request.IsPlaying,visitor.Ip,DeviceLabel(visitor.UserAgent),lang,now));
 }
 public static string DeviceLabel(string ua)=>ua.Contains("Android")?"Android":ua.Contains("iPhone")||ua.Contains("iPad")?"iOS":ua.Contains("Windows")?"Windows PC":ua.Contains("Macintosh")?"macOS":"Trình duyệt web";
 public void Disconnect(string visitor,string session)=>presence.Remove(visitor,session);
 public async Task<List<DevicePresenceDto>> DevicePresenceAsync(CancellationToken ct)
 {
  var devices = await repository.DevicePresenceAsync(ct);
  var active = ActiveDeviceIds();
  return devices.Select(device => device with
  {
   IsOnline = !device.IsBlocked && active.Contains(device.DeviceFingerprint)
  }).ToList();
 }

 private IReadOnlySet<string> ActiveDeviceIds() =>
  presence.Active(clock.GetUtcNow().UtcDateTime).Select(session => session.VisitorId).ToHashSet(StringComparer.Ordinal);

 public async Task<List<DeviceDto>> DevicesAsync(CancellationToken ct)
 {
  var devices = await repository.DevicesAsync(ct);
  var active = ActiveDeviceIds();
  return devices.Select(device => device with
  {
   IsOnline = !device.IsBlocked && active.Contains(device.DeviceFingerprint)
  }).ToList();
 }
 public Task<bool?> ToggleBlockAsync(int id,CancellationToken ct)=>repository.ToggleBlockAsync(id,ct);
 public Task<bool> DeleteAsync(int id,CancellationToken ct)=>repository.DeleteAsync(id,ct);
 public Task<bool> IsBlockedAsync(string id,CancellationToken ct)=>repository.IsBlockedAsync(id,ct);
}
