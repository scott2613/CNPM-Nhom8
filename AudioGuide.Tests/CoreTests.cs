using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
using AudioGuide.Core.Interfaces;
using AudioGuide.Core.Services;
using AudioGuide.Infrastructure.Presence;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Xunit;
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace AudioGuide.Tests;
public class CoreTests {
 [Theory][InlineData("en-US","en")][InlineData("zh-CN","zh")][InlineData("fr","fr")][InlineData("ru_RU","ru")][InlineData("xx","vi")][InlineData(null,"vi")]
 public void LanguageNormalization(string? input,string expected)=>Assert.Equal(expected,LanguagePolicy.Normalize(input));
 [Theory][InlineData("1p",1)][InlineData("1h",60)][InlineData("12h",720)][InlineData("24h",1440)][InlineData("7d",10080)][InlineData("30d",43200)]
 public void ExactAnalyticsWindow(string range,int minutes){var now=new DateTime(2026,11,4,12,0,0,DateTimeKind.Utc);Assert.Equal(now.AddMinutes(-minutes),AnalyticsWindow.Start(range,now));}
 [Fact]public void UnknownRangeIsRejected()=>Assert.Throws<DomainException>(()=>AnalyticsWindow.Start("bad",DateTime.UtcNow));
 [Theory]
 [InlineData("1d", 1)]
 [InlineData("7d", 7)]
 [InlineData("30d", 30)]
 public async Task GateEntryChartUsesVietnameseCalendarAndFillsAllHours(string range, int days)
 {
  var now = new DateTimeOffset(2026, 11, 4, 18, 20, 0, TimeSpan.Zero);
  var repo = new FakeVisitors { GateEntries = [new(16, 2), new(17, 4), new(18, 3)],
   PreviousGateEntries = [new(17, 2), new(19, 7)] };
  var clock = new FixedClock(now);
  var service = new AnalyticsService(repo, new FakePois(),
   new InMemoryPresenceStore(Options.Create(new PresenceOptions()), clock), clock);
  var report = await service.GateEntriesAsync(range, default);
  var expectedStart = new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc).AddDays(1 - days);
  Assert.Equal((expectedStart, now.UtcDateTime), repo.GateWindows[0]);
  Assert.Equal((expectedStart.AddDays(-days), now.UtcDateTime.AddDays(-days)), repo.GateWindows[1]);
  Assert.Equal(expectedStart, report.FromUtc);
  Assert.Equal(now.UtcDateTime, report.ToUtc);
  Assert.Equal((expectedStart.AddDays(-days), now.UtcDateTime.AddDays(-days)), (report.PreviousFromUtc, report.PreviousToUtc));
  Assert.Equal(Enumerable.Range(0, 24), report.Hours.Select(row => row.Hour));
  Assert.Equal(4, report.Hours[0].Count);
  Assert.Equal(3, report.Hours[1].Count);
  Assert.Equal(2, report.Hours[23].Count);
  Assert.Equal(0, report.Hours[2].Count);
  Assert.Equal(2, report.Hours[0].PreviousCount);
  Assert.Equal(7, report.Hours[2].PreviousCount);
 }
 [Fact]
 public async Task PoiReportKeepsViewAndPlaybackSeparateAndIncludesZeroCounts()
 {
  var now = new DateTimeOffset(2026, 11, 4, 18, 20, 0, TimeSpan.Zero);
  var clock = new FixedClock(now);
  var repo = new FakeVisitors { PoiCounts = [new(1, 4, 2), new(9, 100, 90)],
   PreviousPoiCounts = [new(1, 8, 1), new(2, 0, 1)] };
  var pois = new FakePois { AdditionalPois = [new() { Id = 2, Code = "POI-2" }] };
  var service = new AnalyticsService(repo, pois,
   new InMemoryPresenceStore(Options.Create(new PresenceOptions()), clock), clock);
  var report = await service.PoiInteractionsAsync("7d", default);
  Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0, DateTimeKind.Utc), report.FromUtc);
  Assert.Equal((report.FromUtc, now.UtcDateTime), repo.PoiWindows[0]);
  Assert.Equal((report.FromUtc.AddDays(-7), now.UtcDateTime.AddDays(-7)), repo.PoiWindows[1]);
  Assert.Equal(new PoiInteractionRow("POI-1", "POI một", 4, 2, 8, 1), report.Locations[0]);
  Assert.Equal(new PoiInteractionRow("POI-2", "POI-2", 0, 0, 0, 1), report.Locations[1]);
  Assert.Equal(2, report.Locations.Count);
 }
 [Fact]
 public async Task PoiViewIsAnIndependentEventAndSkipsPreview()
 {
  var repo = new FakeVisitors();
  var service = Make(repo);
  var visitor = new VisitorIdentity("a", "ip", "Windows");
  await service.RecordPoiViewAsync(visitor, new() { Preview = true }, "POI-1", default);
  var missing = await Assert.ThrowsAsync<DomainException>(() => service.RecordPoiViewAsync(
   visitor, new() { EventId = Guid.NewGuid() }, "missing", default));
  Assert.Equal(404, missing.Status);
  await service.RecordPoiViewAsync(visitor, new() { EventId = Guid.NewGuid() }, "POI-1", default);
  Assert.Equal("PoiViewed", Assert.Single(repo.Types));
 }
 [Fact]public void PresenceOwnershipAndExpiry(){var now=DateTime.UtcNow;var store=new InMemoryPresenceStore(Options.Create(new PresenceOptions()),TimeProvider.System);store.Put(new("a","tab","P","Title",true,"ip","Windows","en",now));store.Remove("b","tab");Assert.Single(store.Active(now));Assert.Empty(store.Active(now.AddSeconds(21)));}
 [Fact]public void PresenceTabsAreIndependent(){var now=DateTime.UtcNow;var store=new InMemoryPresenceStore(Options.Create(new PresenceOptions()),TimeProvider.System);store.Put(new("a","tab1","","",false,"","","vi",now));store.Put(new("a","tab2","","",false,"","","vi",now));store.Remove("a","tab1");Assert.Equal("tab2",Assert.Single(store.Active(now)).SessionId);}
 [Fact]
 public async Task DeviceStatusFollowsHeartbeatAndDisconnectInsteadOfStoredTime()
 {
  var repo = new FakeVisitors();
  repo.Devices.Add(new(1,"a","ip","PC","Windows","Edge",0,false,DateTime.UtcNow.AddHours(7),DateTime.UtcNow,true));
  var service = Make(repo);
  Assert.False(Assert.Single(await service.DevicesAsync(default)).IsOnline);
  await service.HeartbeatAsync(new("a","ip","Windows"), new() { SessionId = "tab" }, default);
  Assert.True(Assert.Single(await service.DevicesAsync(default)).IsOnline);
  repo.Devices[0] = repo.Devices[0] with { IsBlocked = true };
  Assert.False(Assert.Single(await service.DevicesAsync(default)).IsOnline);
  repo.Devices[0] = repo.Devices[0] with { IsBlocked = false };
  service.Disconnect("a","tab");
  Assert.False(Assert.Single(await service.DevicesAsync(default)).IsOnline);
 }
 [Fact]public async Task PreviewNeverWrites(){var repo=new FakeVisitors();var service=Make(repo);await service.RecordAsync(new("a","ip","Windows"),new(){Preview=true},null,default);await service.HeartbeatAsync(new("a","ip","Windows"),new(){Preview=true,SessionId="tab"},default);Assert.Equal(0,repo.Writes);}
 [Fact]public async Task MissingEventIdRejected(){var e=await Assert.ThrowsAsync<DomainException>(()=>Make(new()).RecordAsync(new("a","ip",""),new(),null,default));Assert.Equal(400,e.Status);}
 [Fact]public async Task UnknownPoiRejected(){var e=await Assert.ThrowsAsync<DomainException>(()=>Make(new()).RecordAsync(new("a","ip",""),new(){EventId=Guid.NewGuid()},"unknown",default));Assert.Equal(404,e.Status);}
 [Fact]public async Task BlockedVisitorRejected(){var e=await Assert.ThrowsAsync<DomainException>(()=>Make(new(){Blocked=true}).RecordAsync(new("a","ip",""),new(){EventId=Guid.NewGuid()},null,default));Assert.Equal(403,e.Status);}
 [Fact]public async Task EntryAndPlaybackAreDifferentEvents(){var repo=new FakeVisitors();var service=Make(repo);await service.RecordAsync(new("a","ip",""),new(){EventId=Guid.NewGuid()},null,default);await service.RecordAsync(new("a","ip",""),new(){EventId=Guid.NewGuid()},"POI-1",default);Assert.Equal(new[]{"GateEntry","PlayStarted"},repo.Types);}
 [Fact]
 public async Task FailedTranslationDoesNotChangeExistingContent()
 {
  var repo = new FakePois();
  var service = new PoiManagementService(repo, new FailingTranslation());
  var error = await Assert.ThrowsAsync<DomainException>(() => service.SavePoiAsync(
   new() { Code = repo.Item.Code, Id = 1, RowVersion = "AQ==", TitleVi = "Tiêu đề mới", DescVi = "Nội dung", Lat = 10, Lng = 106 }));
  Assert.Equal(503, error.Status);
  Assert.Null(repo.Saved);
  Assert.Equal("POI một", Assert.Single(repo.Item.Translations).Title);
 }

 [Fact]
 public async Task SavingVietnameseContentCreatesAllFiveLanguages()
 {
  var repo = new FakePois();
  var service = new PoiManagementService(repo, new SampleTranslation());
  var result = await service.SavePoiAsync(new() { Code = "NEW", TitleVi = "Tiêu đề", DescVi = "Nội dung", Lat = 10, Lng = 106 });
  Assert.True(result.Found);
  Assert.Equal(new[] { "en", "fr", "ru", "vi", "zh" }, repo.Saved!.Translations.Select(t => t.LanguageCode).Order());
  Assert.All(repo.Saved.Translations, row => Assert.False(string.IsNullOrWhiteSpace(row.Description)));
 }

 [Fact]public async Task InvalidGateIsRejected(){var e=await Assert.ThrowsAsync<DomainException>(()=>new AudioGuideService(new FakePois()).MapAsync("museum-anything","vi"));Assert.Equal(404,e.Status);}
 [Fact]public async Task MissingCoordinatesRejected()=>await Assert.ThrowsAsync<DomainException>(()=>new AudioGuideService(new FakePois()).ResolveGpsLocationAsync(new()));
 [Fact]public void CoreDoesNotReferenceEfOrWeb(){var refs=typeof(AudioGuideService).Assembly.GetReferencedAssemblies();Assert.DoesNotContain(refs,r=>r.Name!.StartsWith("Microsoft.EntityFrameworkCore")||r.Name.StartsWith("Microsoft.AspNetCore")||r.Name.Contains("Infrastructure"));}
 private static VisitorService Make(FakeVisitors repo)=>new(repo,new FakePois(),new InMemoryPresenceStore(Options.Create(new PresenceOptions()),TimeProvider.System),TimeProvider.System);
 private sealed class FixedClock(DateTimeOffset now) : TimeProvider
 {
  public override DateTimeOffset GetUtcNow() => now;
 }
}
public sealed class SampleTranslation : ITranslationService {
 public Task<string> TranslateAsync(string text,string targetLanguage,string sourceLanguage="vi",CancellationToken cancellationToken=default)
  => Task.FromResult(targetLanguage + ": " + text);
 public Task<(string Title,string Description)> TranslatePoiContentAsync(string title,string description,string targetLanguage,CancellationToken cancellationToken=default)
  => Task.FromResult((targetLanguage + ": " + title, targetLanguage + ": " + description));
}
public sealed class FailingTranslation:ITranslationService {
 public Task<string> TranslateAsync(string text,string targetLanguage,string sourceLanguage="vi",CancellationToken cancellationToken=default)=>throw new HttpRequestException();
 public Task<(string Title,string Description)> TranslatePoiContentAsync(string title,string description,string targetLanguage,CancellationToken cancellationToken=default)=>throw new HttpRequestException();
}
public sealed class FakePois:IPoiRepository {
 public Poi? Saved;public Poi Item=new(){Id=1,RowVersion=[1],Code="POI-1",Location=new Point(106,10){SRID=4326},Translations=[new(){LanguageCode="vi",Title="POI một"}]};
 public List<Poi> AdditionalPois { get; init; } = [];
 public Task<int> CountAsync(CancellationToken ct)=>Task.FromResult(1);
 public Task<List<Poi>> ListAsync(CancellationToken ct)=>Task.FromResult<List<Poi>>([Item, .. AdditionalPois]);
 public Task<Poi?> FindAsync(int id,CancellationToken ct)=>Task.FromResult(id==1?Item:null);
 public Task<Poi?> FindCodeAsync(string code,CancellationToken ct)=>Task.FromResult(code==Item.Code?Item:null);
 public Task<Dictionary<int,int>> PlayCountsAsync(CancellationToken ct)=>Task.FromResult(new Dictionary<int,int>());
 public Task SaveAsync(Poi poi,CancellationToken ct){Saved=poi;return Task.CompletedTask;}
 public Task<bool> DeleteAsync(int id,CancellationToken ct)=>Task.FromResult(id==1);
 public Task<bool?> ToggleAsync(int id,CancellationToken ct)=>Task.FromResult<bool?>(id==1?false:null);
 public Task<bool> GateEnabledAsync(string token,CancellationToken ct)=>Task.FromResult(token=="MUSEUM-MAIN-ENTRY");
 public Task<PoiNarrativeDto?> NearestAsync(Point location,int floor,string language,CancellationToken ct)=>Task.FromResult<PoiNarrativeDto?>(null);
}
public sealed class FakeVisitors:IVisitorRepository {
 public List<DeviceDto> Devices { get; } = [];
 public List<HourlyGateEntries> GateEntries { get; init; } = [];
 public List<PoiInteractionCount> PoiCounts { get; init; } = [];
 public List<HourlyGateEntries> PreviousGateEntries { get; init; } = [];
 public List<PoiInteractionCount> PreviousPoiCounts { get; init; } = [];
 public List<(DateTime From, DateTime To)> GateWindows { get; } = [];
 public List<(DateTime From, DateTime To)> PoiWindows { get; } = [];
 public int Writes;public bool Blocked;public List<string> Types=[];
 public Task<bool> IsBlockedAsync(string visitor,CancellationToken ct)=>Task.FromResult(Blocked);
 public Task TouchAsync(VisitorIdentity visitor,string language,DateTime now,CancellationToken ct){Writes++;return Task.CompletedTask;}
 public Task RecordAsync(VisitorIdentity visitor,Guid eventId,string type,Poi? poi,string language,DateTime now,CancellationToken ct){Writes++;Types.Add(type);return Task.CompletedTask;}
 public Task<List<DevicePresenceDto>> DevicePresenceAsync(CancellationToken ct)=>Task.FromResult(Devices.Select(d=>new DevicePresenceDto(d.Id,d.DeviceFingerprint,d.IsBlocked,false)).ToList());
 public Task<List<DeviceDto>> DevicesAsync(CancellationToken ct)=>Task.FromResult(Devices);
 public Task<bool?> ToggleBlockAsync(int id,CancellationToken ct)=>Task.FromResult<bool?>(null);
 public Task<bool> DeleteAsync(int id,CancellationToken ct)=>Task.FromResult(false);
 public Task<List<QrScanLog>> EventsAsync(DateTime? from,DateTime until,CancellationToken ct)=>Task.FromResult(new List<QrScanLog>());
 public Task<List<HourlyGateEntries>> HourlyGateEntriesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
 {
  GateWindows.Add((fromUtc, toUtc));
  return Task.FromResult(GateWindows.Count == 1 ? GateEntries : PreviousGateEntries);
 }
 public Task<bool> ReadyAsync(CancellationToken ct)=>Task.FromResult(true);
 public Task<List<PoiInteractionCount>> PoiInteractionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
 {
  PoiWindows.Add((fromUtc, toUtc));
  return Task.FromResult(PoiWindows.Count == 1 ? PoiCounts : PreviousPoiCounts);
 }
}
