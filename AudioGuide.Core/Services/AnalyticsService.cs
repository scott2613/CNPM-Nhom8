using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
namespace AudioGuide.Core.Services;
public sealed class AnalyticsService(IVisitorRepository repository,IPoiRepository pois,IPresenceStore presence,TimeProvider clock) : IAnalyticsService {
 public async Task<GateEntryReport> GateEntriesAsync(string range, CancellationToken ct)
 {
  var (start, now, previousStart, previousEnd) = ReportWindow(range);
  var counts = (await repository.HourlyGateEntriesAsync(start, now, ct))
   .ToDictionary(row => (row.Hour + 7) % 24, row => row.Count);
  var previousCounts = (await repository.HourlyGateEntriesAsync(previousStart, previousEnd, ct))
   .ToDictionary(row => (row.Hour + 7) % 24, row => row.Count);
  var hours = Enumerable.Range(0, 24)
   .Select(hour => new GateEntryHour(hour, counts.GetValueOrDefault(hour), previousCounts.GetValueOrDefault(hour)))
   .ToArray();
  return new GateEntryReport(start, now, hours, previousStart, previousEnd);
 }

 public async Task<PoiInteractionReport> PoiInteractionsAsync(string range, CancellationToken ct)
 {
  var (start, now, previousStart, previousEnd) = ReportWindow(range);
  var counts = (await repository.PoiInteractionCountsAsync(start, now, ct)).ToDictionary(row => row.PoiId);
  var previousCounts = (await repository.PoiInteractionCountsAsync(previousStart, previousEnd, ct)).ToDictionary(row => row.PoiId);
  var locations = (await pois.ListAsync(ct)).Select(poi =>
  {
   counts.TryGetValue(poi.Id, out var count);
   previousCounts.TryGetValue(poi.Id, out var previous);
   var title = poi.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Title ?? poi.Code;
   return new PoiInteractionRow(poi.Code, title, count?.ViewCount ?? 0, count?.PlayCount ?? 0,
    previous?.ViewCount ?? 0, previous?.PlayCount ?? 0);
  }).OrderByDescending(row => row.ViewCount).ThenByDescending(row => row.PlayCount)
   .ThenBy(row => row.Code, StringComparer.Ordinal).ToArray();
  return new PoiInteractionReport(start, now, locations, previousStart, previousEnd);
 }

 private (DateTime Start, DateTime End, DateTime PreviousStart, DateTime PreviousEnd) ReportWindow(string range)
 {
  var days = range switch
  {
   "1d" => 1,
   "7d" => 7,
   "30d" => 30,
   _ => throw new DomainException(400, "Hãy chọn khoảng thống kê 1 ngày, 1 tuần hoặc 1 tháng.")
  };
  var now = clock.GetUtcNow().UtcDateTime;
  var start = now.AddHours(7).Date.AddDays(1 - days).AddHours(-7);
  return (start, now, start.AddDays(-days), now.AddDays(-days));
 }

 public async Task<List<HistoryGroup>> HistoryAsync(string range,CancellationToken ct) {
  var now=clock.GetUtcNow().UtcDateTime;var events=await repository.EventsAsync(AnalyticsWindow.Start(range,now),now,ct);
  var names=(await pois.ListAsync(ct)).ToDictionary(p=>p.Id,p=>p.Translations.FirstOrDefault(t=>t.LanguageCode=="vi")?.Title??p.Code);
  return events.GroupBy(e=>e.VisitorId).Select(g=>{var last=g.OrderByDescending(e=>e.ScannedAt).First();
   return new HistoryGroup(last.IpAddress??"",VisitorService.DeviceLabel(last.UserAgent??""),g.Count(),last.ScannedAt.AddHours(7).ToString("HH:mm:ss - dd/MM/yyyy"),
    g.GroupBy(e=>new {e.PoiId,e.PoiCode,e.EventType}).Select(x=>new HistoryDetail(x.Key.PoiCode??"MUSEUM-MAIN-ENTRY",x.Key.EventType=="GateEntry"?"Cổng chính":names.GetValueOrDefault(x.Key.PoiId??0,x.Key.PoiCode??"POI đã xóa"),x.Count())).ToList());
  }).OrderByDescending(x=>x.TotalScans).ToList();
 }
 public async Task<RealtimeMetrics> RealtimeAsync(CancellationToken ct) {
  var now=clock.GetUtcNow().UtcDateTime;var events=await repository.EventsAsync(now.AddHours(7).Date.AddHours(-7),now,ct);
  var active=presence.Active(now);var plays=events.Where(e=>e.EventType=="PlayStarted").ToList();
  var peak=plays.GroupBy(e=>e.ScannedAt.AddHours(7).Hour).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).FirstOrDefault();
  var top=active.Where(s=>s.IsPlaying).GroupBy(s=>s.PoiCode).OrderByDescending(g=>g.Select(s=>s.VisitorId).Distinct().Count()).ThenBy(g=>g.Key).FirstOrDefault()?.First().PoiName??"Chưa có người nghe";
  return new(plays.Count,active.Select(s=>s.VisitorId).Distinct().Count(),top,peak==null?"N/A":$"{peak.Key:D2}:00 - {(peak.Key+1)%24:D2}:00",
   active.Select(s=>new RealtimeLog(s.SessionId,s.PoiCode,s.PoiName,s.IpAddress,s.Device,s.LanguageCode,s.LastSeen.AddHours(7).ToString("HH:mm:ss - dd/MM/yyyy"))).ToList(),events.Count(e=>e.EventType=="GateEntry"));
 }
 public Task<bool> ReadyAsync(CancellationToken ct)=>repository.ReadyAsync(ct);
}
