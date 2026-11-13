using System.ComponentModel.DataAnnotations;
namespace AudioGuide.Core.DTOs;
public sealed class GpsLocationRequest {
 [Required,Range(-180d,180d)] public double? Longitude {get;set;}
 [Required,Range(-90d,90d)] public double? Latitude {get;set;}
 [Range(0,10)] public int Floor {get;set;}
 [Range(0d,10000d)] public double AccuracyMeters {get;set;}
 public DateTimeOffset? SampledAt {get;set;}
 [StringLength(10)] public string? LanguageCode {get;set;}
}
