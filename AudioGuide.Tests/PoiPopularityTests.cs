using AudioGuide.Core.Entities;
using AudioGuide.Core.Services;
using NetTopologySuite.Geometries;
using Xunit;
using AudioGuide.Infrastructure.Data;
using AudioGuide.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
namespace AudioGuide.Tests;
public class PoiPopularityTests
{
 [SqlFact]
 public async Task ViewCountsQueryCountsOnlyUtcPoiViews()
 {
  var options=new DbContextOptionsBuilder<AppDbContext>()
   .UseSqlServer(Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!,s=>s.UseNetTopologySuite()).Options;
  await using var db=new AppDbContext(options);
  await using var transaction=await db.Database.BeginTransactionAsync();
  var poi=Poi(0); poi.Code="POPULAR-"+Guid.NewGuid().ToString("N");
  db.Pois.Add(poi); await db.SaveChangesAsync();
  QrScanLog Event(string type,bool utc=true)=>new() {
   EventId=Guid.NewGuid(),EventType=type,IsUtc=utc,PoiId=poi.Id,
   QrToken="MUSEUM-MAIN-ENTRY",ScannedAt=DateTime.UtcNow
  };
  db.QrScanLogs.AddRange(Event("PoiViewed"),Event("PoiViewed"),
   Event("PlayStarted"),Event("GateEntry"),Event("PoiViewed",false));
  await db.SaveChangesAsync();
  var counts=await new PoiRepository(db).ViewCountsAsync(default);
  Assert.Equal(2L,counts[poi.Id]);
 }
 private static Poi Poi(int id, bool active=true)=>new() {
  Id=id, Code=$"POI-{id}", IsActive=active, Location=new Point(106,10){SRID=4326}
 };
 [Fact]
 public async Task MapRanksAllTiedExtremesAndIncludesZeroVisitPois()
 {
  var repo=new FakePois {
   ViewCounts=new(){[1]=10,[2]=10,[3]=4,[5]=500},
   AdditionalPois=[Poi(2),Poi(3),Poi(4),Poi(5,false)]
  };
  var rows=await new AudioGuideService(repo).MapAsync("MUSEUM-MAIN-ENTRY","vi");
  Assert.Equal(4,rows.Count);
  Assert.All(rows.Where(p=>p.Id==1||p.Id==2),p=>Assert.Equal("most",p.VisitRank));
  Assert.Equal("normal",rows.Single(p=>p.Id==3).VisitRank);
  Assert.Equal("least",rows.Single(p=>p.Id==4).VisitRank);
  Assert.Equal(0L,rows.Single(p=>p.Id==4).ViewCount);
  Assert.Equal(10L,rows.Single(p=>p.Id==1).ViewCount);
 }
 [Theory]
 [InlineData(0)]
 [InlineData(7)]
 public async Task EqualCountsAreNeutral(long count)
 {
  var repo=new FakePois {ViewCounts=new(){[1]=count,[2]=count},AdditionalPois=[Poi(2)]};
  var rows=await new AudioGuideService(repo).MapAsync("MUSEUM-MAIN-ENTRY","vi");
  Assert.All(rows,p=>Assert.Equal("normal",p.VisitRank));
 }
 [Fact]
 public async Task SinglePoiIsNeutral()
 {
  var rows=await new AudioGuideService(new FakePois{ViewCounts=new(){[1]=12}})
   .MapAsync("MUSEUM-MAIN-ENTRY","vi");
  Assert.Equal("normal",Assert.Single(rows).VisitRank);
 }
 [Fact]
 public async Task NoActivePoiReturnsEmptyMap()
 {
  var repo=new FakePois(); repo.Item.IsActive=false;
  Assert.Empty(await new AudioGuideService(repo).MapAsync("MUSEUM-MAIN-ENTRY","vi"));
 }
}

