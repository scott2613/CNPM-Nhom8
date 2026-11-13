using System.ComponentModel.DataAnnotations;
namespace AudioGuide.Core.DTOs;
public sealed class PoiSaveDto {
 [Range(0,int.MaxValue)] public int Id {get;set;}
 [Required,StringLength(50),RegularExpression(@"^[A-Za-z0-9_-]+$")] public string Code {get;set;}="";
 [Required,StringLength(200)] public string TitleVi {get;set;}="";
 [StringLength(10000)] public string? DescVi {get;set;}
 [Range(0,10)] public int Floor {get;set;}
 [Range(-90d,90d)] public double Lat {get;set;}
 [Range(-180d,180d)] public double Lng {get;set;}
 [StringLength(500)] public string? ImageUrl {get;set;}
 public bool IsActive {get;set;}=true;
 [Range(1d,100d)] public double TriggerRadiusMeters {get;set;}=5;
 public string? RowVersion {get;set;}
}
