using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
using NetTopologySuite.Geometries;
namespace AudioGuide.Core.Services;
public sealed class AudioGuideService(IPoiRepository repository) : IAudioGuideService {
 public static string NormalizeLanguage(string? value)=>LanguagePolicy.Normalize(value);
 public async Task<List<PoiMapItem>> MapAsync(string token,string? language,CancellationToken ct=default) {
  if(!await repository.GateEnabledAsync(token,ct))throw new DomainException(404,"QR cổng không hợp lệ hoặc đã ngừng hoạt động.");
  var lang=LanguagePolicy.Normalize(language);
  var pois=(await repository.ListAsync(ct)).Where(p=>p.IsActive).OrderBy(p=>p.Floor).ThenBy(p=>p.Id).ToList();
  var counts=await repository.ViewCountsAsync(ct);
  var min=pois.Count==0?0:pois.Min(p=>counts.GetValueOrDefault(p.Id));
  var max=pois.Count==0?0:pois.Max(p=>counts.GetValueOrDefault(p.Id));
  return pois.Select(p=>{
   var t=p.Translations.FirstOrDefault(t=>t.LanguageCode==lang)??p.Translations.FirstOrDefault(t=>t.LanguageCode=="vi");
   return new PoiMapItem(p.Id,p.Code,p.Floor,p.Location.Y,p.Location.X,t?.Title??p.Code,t?.Description??"",p.ImageUrl,t?.LanguageCode??"vi")
   {
    TriggerRadiusMeters = p.TriggerRadiusMeters,
    ViewCount = counts.GetValueOrDefault(p.Id),
    VisitRank = min==max?"normal":counts.GetValueOrDefault(p.Id)==max?"most":counts.GetValueOrDefault(p.Id)==min?"least":"normal"
   };
  }).ToList();
 }
 public Task<PoiNarrativeDto?> ResolveGpsLocationAsync(GpsLocationRequest request,CancellationToken cancellationToken=default) {
  if(request.Latitude is null||request.Longitude is null||!double.IsFinite(request.Latitude.Value)||!double.IsFinite(request.Longitude.Value)||Math.Abs(request.Latitude.Value)>90||Math.Abs(request.Longitude.Value)>180)
   throw new DomainException(400,"Tọa độ không hợp lệ.");
  var point=new Point(request.Longitude.Value,request.Latitude.Value){SRID=4326};
  return repository.NearestAsync(point, request.Floor, LanguagePolicy.Normalize(request.LanguageCode),
   cancellationToken, request.ActivePoiId);
 }
}

