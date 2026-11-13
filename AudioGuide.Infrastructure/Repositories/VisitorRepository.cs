using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
using AudioGuide.Core.Interfaces;
using AudioGuide.Core.Services;
using AudioGuide.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace AudioGuide.Infrastructure.Repositories;
public sealed class VisitorRepository(AppDbContext db) : IVisitorRepository {
 public Task<bool> IsBlockedAsync(string visitor,CancellationToken ct)=>db.DeviceSessions.AsNoTracking().AnyAsync(d=>d.DeviceFingerprint==visitor&&d.IsBlocked,ct);
 private async Task<DeviceSession> TouchLockedAsync(VisitorIdentity visitor,string language,DateTime now,CancellationToken ct) {
  var device=await db.DeviceSessions.FromSqlInterpolated($"SELECT * FROM dbo.DeviceSessions WITH (UPDLOCK,HOLDLOCK) WHERE DeviceFingerprint={visitor.Id}").SingleOrDefaultAsync(ct);
  if(device?.IsBlocked==true)throw new DomainException(403,"Thiết bị bị chặn.");
  if(device==null){device=new(){DeviceFingerprint=visitor.Id,CreatedAt=now};db.DeviceSessions.Add(device);}
  device.LastActiveAt=now;device.IpAddress=visitor.Ip;device.PreferredLanguage=language;
  device.DeviceName=VisitorService.DeviceLabel(visitor.UserAgent);device.Platform=device.DeviceName;
  device.Browser=visitor.UserAgent.Contains("Edg")?"Edge":visitor.UserAgent.Contains("Chrome")?"Chrome":visitor.UserAgent.Contains("Safari")?"Safari":"Web";
  return device;
 }
 public async Task TouchAsync(VisitorIdentity visitor,string language,DateTime now,CancellationToken ct) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await TouchLockedAsync(visitor,language,now,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
 }
 public async Task RecordAsync(VisitorIdentity visitor,Guid eventId,string type,Poi? poi,string language,DateTime now,CancellationToken ct) {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var existing=await db.QrScanLogs.FromSqlInterpolated($"SELECT * FROM dbo.QrScanLogs WITH (UPDLOCK,HOLDLOCK) WHERE EventId={eventId}").SingleOrDefaultAsync(ct);
  if(existing!=null){if(existing.VisitorId!=visitor.Id||existing.EventType!=type||existing.PoiId!=poi?.Id)throw new DomainException(409,"EventId đã dùng cho sự kiện khác.");return;}
  var device=await TouchLockedAsync(visitor,language,now,ct);
  db.QrScanLogs.Add(new(){EventId=eventId,EventType=type,VisitorId=visitor.Id,PoiId=poi?.Id,PoiCode=poi?.Code,QrToken="MUSEUM-MAIN-ENTRY",LanguageCode=language,ScannedAt=now,IsUtc=true,IpAddress=visitor.Ip,UserAgent=visitor.UserAgent});
  if(type!="PoiViewed")device.TotalScans++;
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
 }
 public Task<List<DevicePresenceDto>> DevicePresenceAsync(CancellationToken ct) =>
  db.DeviceSessions.AsNoTracking().OrderByDescending(d => d.LastActiveAt).ThenBy(d => d.Id).Take(500)
    .Select(d => new DevicePresenceDto(d.Id, d.DeviceFingerprint, d.IsBlocked, false)).ToListAsync(ct);

 public async Task<List<DeviceDto>> DevicesAsync(CancellationToken ct) {
  var rows=await db.DeviceSessions.AsNoTracking().OrderByDescending(d=>d.LastActiveAt).ThenBy(d=>d.Id).Take(500).ToListAsync(ct);
  var ids=rows.Select(d=>d.DeviceFingerprint).ToArray();
  var counts=await db.QrScanLogs.Where(e=>e.IsUtc&&e.EventType!="PoiViewed"&&e.VisitorId!=null&&ids.Contains(e.VisitorId)).GroupBy(e=>e.VisitorId!).ToDictionaryAsync(g=>g.Key,g=>g.Count(),ct);
  return rows.Select(d=>new DeviceDto(d.Id,d.DeviceFingerprint,d.IpAddress,d.DeviceName,d.Platform,d.Browser,counts.GetValueOrDefault(d.DeviceFingerprint),d.IsBlocked,DateTime.SpecifyKind(d.LastActiveAt,DateTimeKind.Utc),DateTime.SpecifyKind(d.CreatedAt,DateTimeKind.Utc),false)).ToList();
 }
 public async Task<bool?> ToggleBlockAsync(int id,CancellationToken ct){var d=await db.DeviceSessions.FindAsync([id],ct);if(d==null)return null;d.IsBlocked=!d.IsBlocked;await db.SaveChangesAsync(ct);return d.IsBlocked;}
 public async Task<bool> DeleteAsync(int id,CancellationToken ct){
  await using var tx=await db.Database.BeginTransactionAsync(ct);var d=await db.DeviceSessions.FindAsync([id],ct);if(d==null)return false;
  await db.QrScanLogs.Where(e=>e.VisitorId==d.DeviceFingerprint).ExecuteDeleteAsync(ct);
  db.DeviceSessions.Remove(d);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
 }
 public async Task<List<QrScanLog>> EventsAsync(DateTime? from,DateTime until,CancellationToken ct){
  var q=db.QrScanLogs.AsNoTracking().Where(e=>e.IsUtc&&(e.EventType=="GateEntry"||e.EventType=="PlayStarted")&&e.ScannedAt<=until);
  if(from.HasValue)q=q.Where(e=>e.ScannedAt>=from.Value);
  // Hard bound prevents unbounded in-memory reports. Refuse rather than silently truncate totals.
  var rows=await q.OrderByDescending(e=>e.ScannedAt).Take(50001).ToListAsync(ct);
  if(rows.Count>50000)throw new DomainException(422,"Khoảng báo cáo quá lớn. Hãy chọn khoảng ngắn hơn.");return rows;
 }
 public Task<bool> ReadyAsync(CancellationToken ct)=>db.Database.CanConnectAsync(ct);

 public Task<List<HourlyGateEntries>> HourlyGateEntriesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
  db.QrScanLogs.AsNoTracking()
   .Where(entry => entry.IsUtc && entry.EventType == "GateEntry" && entry.ScannedAt >= fromUtc && entry.ScannedAt <= toUtc)
   .GroupBy(entry => entry.ScannedAt.Hour)
   .Select(group => new HourlyGateEntries(group.Key, group.LongCount()))
   .ToListAsync(ct);

 public Task<List<PoiInteractionCount>> PoiInteractionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
  db.QrScanLogs.AsNoTracking()
   .Where(entry => entry.IsUtc && entry.PoiId.HasValue && entry.ScannedAt >= fromUtc && entry.ScannedAt <= toUtc
    && (entry.EventType == "PoiViewed" || entry.EventType == "PlayStarted"))
   .GroupBy(entry => entry.PoiId!.Value)
   .Select(group => new PoiInteractionCount(group.Key,
    group.Sum(entry => entry.EventType == "PoiViewed" ? 1L : 0L),
    group.Sum(entry => entry.EventType == "PlayStarted" ? 1L : 0L)))
   .ToListAsync(ct);
}
