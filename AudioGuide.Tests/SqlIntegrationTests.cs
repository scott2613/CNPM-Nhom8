using AudioGuide.Core.DTOs;
using AudioGuide.Core.Entities;
using AudioGuide.Infrastructure.Data;
using AudioGuide.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AudioGuide.Tests;
public sealed class SqlFactAttribute:FactAttribute{public SqlFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")))Skip="Set AUDIOGUIDE_TEST_SQL to a disposable SQL Server database.";}}
public class SqlIntegrationTests {
 [SqlFact]
 public async Task HourlyGateQueryCountsOnlyUtcGateEventsInsideWindow()
 {
  var connection = Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!;
  var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection, s => s.UseNetTopologySuite()).Options;
  await using var db = new AppDbContext(options);
  var visitor = Guid.NewGuid().ToString("N");
  var start = DateTime.UtcNow.Date.AddYears(50).AddHours(17);
  var end = start.AddDays(7).AddMinutes(-1);
  QrScanLog Event(string type, DateTime time, bool utc = true) =>
   new() { EventId = Guid.NewGuid(), VisitorId = visitor, EventType = type,
    ScannedAt = time, IsUtc = utc, QrToken = "MUSEUM-MAIN-ENTRY" };
  db.QrScanLogs.AddRange(Event("GateEntry", start), Event("GateEntry", start.AddDays(1).AddMinutes(59)),
   Event("GateEntry", end), Event("GateEntry", start.AddSeconds(-1)), Event("GateEntry", end.AddSeconds(1)),
   Event("GateEntry", start.AddMinutes(10), false), Event("PlayStarted", start.AddMinutes(20)), Event("Legacy", start));
  await db.SaveChangesAsync();
  try
  {
   var hours = await new VisitorRepository(db).HourlyGateEntriesAsync(start, end, default);
   Assert.Equal(new[] { new HourlyGateEntries(16, 1), new HourlyGateEntries(17, 2) }, hours.OrderBy(row => row.Hour));
  }
  finally
  {
   await db.QrScanLogs.Where(e => e.VisitorId == visitor).ExecuteDeleteAsync();
  }
 }

 [SqlFact]public async Task EventRetryAndConcurrentVisitorsDoNotLoseCounts(){
  var connection=Environment.GetEnvironmentVariable("AUDIOGUIDE_TEST_SQL")!;
  var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection,s=>s.UseNetTopologySuite()).Options;
  // Test DB must be isolated. Never delete or EnsureCreated on a production connection.
  var visitor=new VisitorIdentity(Guid.NewGuid().ToString("N"),"127.0.0.1","Windows");var eventId=Guid.NewGuid();
  async Task Record(Guid id){await using var db=new AppDbContext(options);await new VisitorRepository(db).RecordAsync(visitor,id,"GateEntry",null,"vi",DateTime.UtcNow,default);}
  try{
   await Record(eventId);await Record(eventId);
   // Sequential duplicate then independent concurrent writes exercise the actual SQL transaction/index path.
   await Task.WhenAll(Enumerable.Range(0,5).Select(_=>Record(Guid.NewGuid())));
   await using var check=new AppDbContext(options);Assert.Equal(6,await check.QrScanLogs.CountAsync(e=>e.VisitorId==visitor.Id));Assert.Equal(6,(await check.DeviceSessions.SingleAsync(d=>d.DeviceFingerprint==visitor.Id)).TotalScans);
  }finally{await using var cleanup=new AppDbContext(options);await cleanup.QrScanLogs.Where(e=>e.VisitorId==visitor.Id).ExecuteDeleteAsync();await cleanup.DeviceSessions.Where(d=>d.DeviceFingerprint==visitor.Id).ExecuteDeleteAsync();}
 }
}
