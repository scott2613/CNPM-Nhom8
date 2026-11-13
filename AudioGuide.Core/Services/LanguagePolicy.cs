namespace AudioGuide.Core.Services;
public static class LanguagePolicy {
 public static readonly string[] Supported = ["vi","en","zh","fr","ru"];
 public static string Normalize(string? value) {
  var tag=(value??"vi").Split(',', ';')[0].Trim().Split('-', '_')[0].ToLowerInvariant();
  return Supported.Contains(tag)?tag:"vi";
 }
}
public static class AnalyticsWindow {
 public static DateTime? Start(string range,DateTime utc) => range.ToLowerInvariant() switch {
 "1p"=>utc.AddMinutes(-1),"1h" or "1g"=>utc.AddHours(-1),"12h" or "12g"=>utc.AddHours(-12),
 "24h" or "1n"=>utc.AddHours(-24),"1w" or "7d" or "7n"=>utc.AddDays(-7),
 "1m" or "30d" or "30n"=>utc.AddDays(-30),"1y" or "365d"=>utc.AddYears(-1),"all"=>null,
 _=>throw new AudioGuide.Core.DTOs.DomainException(400,"Khoảng thời gian không hợp lệ.")};
}
