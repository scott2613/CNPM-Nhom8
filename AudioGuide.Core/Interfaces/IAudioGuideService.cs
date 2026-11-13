using AudioGuide.Core.DTOs;
namespace AudioGuide.Core.Interfaces;
public interface IAudioGuideService {
 Task<List<PoiMapItem>> MapAsync(string token,string? language,CancellationToken ct=default);
 Task<PoiNarrativeDto?> ResolveGpsLocationAsync(GpsLocationRequest request,CancellationToken cancellationToken=default);
}
