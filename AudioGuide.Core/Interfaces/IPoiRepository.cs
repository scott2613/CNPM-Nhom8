using AudioGuide.Core.Entities;
using AudioGuide.Core.DTOs;
using NetTopologySuite.Geometries;
namespace AudioGuide.Core.Interfaces;
public interface IPoiRepository {
 Task<int> CountAsync(CancellationToken ct);
 Task<List<Poi>> ListAsync(CancellationToken ct);
 Task<Poi?> FindAsync(int id,CancellationToken ct);
 Task<Poi?> FindCodeAsync(string code,CancellationToken ct);
 Task<Dictionary<int,int>> PlayCountsAsync(CancellationToken ct);
 Task SaveAsync(Poi poi,CancellationToken ct);
 Task<bool> DeleteAsync(int id,CancellationToken ct);
 Task<bool?> ToggleAsync(int id,CancellationToken ct);
 Task<bool> GateEnabledAsync(string token,CancellationToken ct);
 Task<PoiNarrativeDto?> NearestAsync(Point location,int floor,string language,CancellationToken ct);
}
