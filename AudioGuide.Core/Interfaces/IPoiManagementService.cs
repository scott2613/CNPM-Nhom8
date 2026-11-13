using AudioGuide.Core.DTOs;
namespace AudioGuide.Core.Interfaces;
public interface IPoiManagementService {
 Task<List<PoiAdminDto>> GetPoisForAdminAsync(CancellationToken cancellationToken=default);
 Task<SavePoiResult> SavePoiAsync(PoiSaveDto request,CancellationToken cancellationToken=default);
 Task<bool> DeletePoiAsync(int id,CancellationToken cancellationToken=default);
 Task<bool?> ToggleStatusAsync(int id,CancellationToken cancellationToken=default);
 Task<int> SyncSystemPoisAsync(CancellationToken cancellationToken=default);
}
