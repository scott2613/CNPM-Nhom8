using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
using AudioGuide.Core.Interfaces;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
namespace AudioGuide.Core.Services;
public sealed class PoiManagementService(IPoiRepository repository,ITranslationService translator) : IPoiManagementService {
 public async Task<List<PoiAdminDto>> GetPoisForAdminAsync(CancellationToken cancellationToken=default) {
  var counts=await repository.PlayCountsAsync(cancellationToken);
  return (await repository.ListAsync(cancellationToken)).Select(p=>new PoiAdminDto {
   Id=p.Id,Code=p.Code,SortOrder=p.Id,TitleVi=p.Translations.FirstOrDefault(t=>t.LanguageCode=="vi")?.Title??p.Code,
   TitleEn=p.Translations.FirstOrDefault(t=>t.LanguageCode=="en")?.Title??"",DescVi=p.Translations.FirstOrDefault(t=>t.LanguageCode=="vi")?.Description??"",
   Floor=p.Floor,Lat=p.Location.Y,Lng=p.Location.X,ImageUrl=p.ImageUrl,IsActive=p.IsActive,ScanCount=counts.GetValueOrDefault(p.Id),
   RowVersion=Convert.ToBase64String(p.RowVersion),TriggerRadiusMeters=p.TriggerRadiusMeters
  }).ToList();
 }
 public async Task<SavePoiResult> SavePoiAsync(PoiSaveDto req,CancellationToken cancellationToken=default) {
  Validator.ValidateObject(req,new ValidationContext(req),true);
  if(!double.IsFinite(req.Lat)||!double.IsFinite(req.Lng))throw new DomainException(400,"Tọa độ không hợp lệ.");
  if(!string.IsNullOrWhiteSpace(req.ImageUrl)&&!(Uri.TryCreate(req.ImageUrl,UriKind.Absolute,out var url)&&url.Scheme=="https"))
   throw new DomainException(400,"Ảnh phải dùng URL HTTPS hợp lệ.");
  var poi=req.Id==0?new Poi():await repository.FindAsync(req.Id,cancellationToken);
  if(poi==null)return new(false);
  if(req.Id>0&&(string.IsNullOrEmpty(req.RowVersion)||req.RowVersion!=Convert.ToBase64String(poi.RowVersion)))
   throw new DomainException(409,"Dữ liệu đã thay đổi. Hãy tải lại trước khi lưu.");
  var code=req.Code.Trim().ToUpperInvariant();
  var duplicate=await repository.FindCodeAsync(code,cancellationToken);
  if(duplicate!=null&&duplicate.Id!=req.Id)throw new DomainException(409,"Mã POI đã tồn tại.");
  var title=req.TitleVi.Trim();var desc=req.DescVi?.Trim()??"";
  var vi=poi.Translations.FirstOrDefault(t=>t.LanguageCode=="vi");
  var changed=vi==null||vi.Title!=title||vi.Description!=desc;
  var pending = LanguagePolicy.Supported.Where(language => language != "vi" &&
      (changed || !poi.Translations.Any(t => t.LanguageCode == language &&
          !string.IsNullOrWhiteSpace(t.Title) && (desc.Length == 0 || !string.IsNullOrWhiteSpace(t.Description))))).ToArray();

  // Obtain all translations before changing any tracked entity.
  var translated = await Task.WhenAll(pending.Select(async language =>
  {
   try
   {
    var result = await translator.TranslatePoiContentAsync(title, desc, language, cancellationToken);
    if (string.IsNullOrWhiteSpace(result.Title) || result.Title.Trim().Length > 200 ||
        (desc.Length > 0 && string.IsNullOrWhiteSpace(result.Description)))
     throw new HttpRequestException("Incomplete translation.");
    return (Language: language, Title: result.Title.Trim(), Description: result.Description?.Trim() ?? "");
   }
   catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
   catch (Exception error) when (error is HttpRequestException or InvalidOperationException or OperationCanceledException)
   {
    throw new DomainException(503, $"Không thể tạo bản dịch ({language}). Kiểm tra khóa Google Cloud Translation và kết nối, rồi lưu lại. Nội dung chưa được thay đổi.");
   }
  }));

  void SetTranslation(string language, string translatedTitle, string description)
  {
   var row = poi.Translations.FirstOrDefault(t => t.LanguageCode == language);
   if (row is null)
   {
    row = new PoiTranslation { LanguageCode = language };
    poi.Translations.Add(row);
   }
   row.Title = translatedTitle;
   row.Description = description;
   row.AudioUrl = "";
   row.DurationSeconds = 0;
  }

  if (changed) SetTranslation("vi", title, desc);
  foreach (var item in translated) SetTranslation(item.Language, item.Title, item.Description);
  poi.Code=code;poi.Location=new Point(req.Lng,req.Lat){SRID=4326};poi.Floor=req.Floor;
  poi.IsActive=req.IsActive;poi.ImageUrl=string.IsNullOrWhiteSpace(req.ImageUrl)?null:req.ImageUrl.Trim();poi.TriggerRadiusMeters=req.TriggerRadiusMeters;
  await repository.SaveAsync(poi,cancellationToken);
  return new(true);
 }
 public Task<bool> DeletePoiAsync(int id,CancellationToken cancellationToken=default)=>repository.DeleteAsync(id,cancellationToken);
 public Task<bool?> ToggleStatusAsync(int id,CancellationToken cancellationToken=default)=>repository.ToggleAsync(id,cancellationToken);
 // Compatibility endpoint now refreshes inventory; never invents content or per-POI QR records.
 public async Task<int> SyncSystemPoisAsync(CancellationToken cancellationToken=default)=>(await repository.ListAsync(cancellationToken)).Count;
}
