using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
using AudioGuide.Core.Interfaces;
using AudioGuide.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
namespace AudioGuide.Infrastructure.Repositories;
public sealed class PoiRepository(AppDbContext db) : IPoiRepository {
 public Task<int> CountAsync(CancellationToken ct)=>db.Pois.CountAsync(ct);
 public Task<List<Poi>> ListAsync(CancellationToken ct)=>db.Pois.AsNoTracking().Include(p=>p.Translations).OrderBy(p=>p.Id).ToListAsync(ct);
 public Task<Poi?> FindAsync(int id,CancellationToken ct)=>db.Pois.Include(p=>p.Translations).FirstOrDefaultAsync(p=>p.Id==id,ct);
 public Task<Poi?> FindCodeAsync(string code,CancellationToken ct)=>db.Pois.AsNoTracking().Include(p=>p.Translations).FirstOrDefaultAsync(p=>p.Code==code,ct);
 public Task<Dictionary<int,int>> PlayCountsAsync(CancellationToken ct)=>db.QrScanLogs.Where(e=>e.EventType=="PlayStarted"&&e.IsUtc&&e.PoiId!=null).GroupBy(e=>e.PoiId!.Value).ToDictionaryAsync(g=>g.Key,g=>g.Count(),ct);
 public async Task SaveAsync(Poi poi,CancellationToken ct) {
  if(poi.Id==0)db.Pois.Add(poi);
  else db.Entry(poi).Property(p => p.Code).IsModified = true;
  await db.SaveChangesAsync(ct);
 }
 public async Task<bool> DeleteAsync(int id,CancellationToken ct) {
  var poi=await db.Pois.FirstOrDefaultAsync(p=>p.Id==id,ct);if(poi==null)return false;
  // SET NULL retains historical event snapshots and independent gate metadata.
  db.Pois.Remove(poi);await db.SaveChangesAsync(ct);return true;
 }
 public async Task<bool?> ToggleAsync(int id,CancellationToken ct) {
  var poi=await db.Pois.FirstOrDefaultAsync(p=>p.Id==id,ct);if(poi==null)return null;
  poi.IsActive=!poi.IsActive;await db.SaveChangesAsync(ct);return poi.IsActive;
 }
 public Task<bool> GateEnabledAsync(string token,CancellationToken ct)=>db.QrCodes.AsNoTracking().AnyAsync(q=>q.QrToken==token&&q.QrToken=="MUSEUM-MAIN-ENTRY"&&q.PoiId==null&&q.IsActive,ct);
 public async Task<PoiNarrativeDto?> NearestAsync(Point location,int floor,string language,CancellationToken ct) {
  var result=await db.Pois.AsNoTracking().Where(p=>p.IsActive&&p.Floor==floor&&p.Location.Distance(location)<=p.TriggerRadiusMeters&&p.Translations.Any(t=>t.LanguageCode==language||t.LanguageCode=="vi"))
   .OrderBy(p=>p.Location.Distance(location)).ThenBy(p=>p.Id).Select(p=>new {p.Id,p.Code,Distance=p.Location.Distance(location),Translation=p.Translations.Where(t=>t.LanguageCode==language||t.LanguageCode=="vi").OrderByDescending(t=>t.LanguageCode==language).First()}).FirstOrDefaultAsync(ct);
  if(result==null)return null;
  return new(){PoiId=result.Id,Code=result.Code,Title=result.Translation.Title,Description=result.Translation.Description,AudioUrl="",DurationSeconds=0,LanguageCode=result.Translation.LanguageCode,DistanceMeters=Math.Round(result.Distance,2)};
 }
}
