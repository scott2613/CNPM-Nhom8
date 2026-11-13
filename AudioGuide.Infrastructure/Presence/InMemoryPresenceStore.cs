using AudioGuide.Core.DTOs;
using AudioGuide.Core.Interfaces;
using Microsoft.Extensions.Options;
namespace AudioGuide.Infrastructure.Presence;
public sealed class PresenceOptions {public int TtlSeconds {get;set;}=20;public int MaxSessions {get;set;}=10000;}
public sealed class InMemoryPresenceStore(IOptions<PresenceOptions> options,TimeProvider clock) : IPresenceStore {
 private readonly object gate=new();private readonly Dictionary<(string,string),PresenceSession> sessions=new();
 public int TtlSeconds=>options.Value.TtlSeconds;
 private void Prune(DateTime now){foreach(var key in sessions.Where(x=>(now-x.Value.LastSeen).TotalSeconds>TtlSeconds).Select(x=>x.Key).ToArray())sessions.Remove(key);}
 public void Put(PresenceSession session){lock(gate){Prune(clock.GetUtcNow().UtcDateTime);var key=(session.VisitorId,session.SessionId);if(sessions.Count>=options.Value.MaxSessions&&!sessions.ContainsKey(key))throw new DomainException(429,"Hệ thống đang có nhiều phiên. Vui lòng thử lại.");sessions[key]=session;}}
 public void Remove(string visitor,string session){lock(gate)sessions.Remove((visitor,session));}
 public IReadOnlyList<PresenceSession> Active(DateTime now){lock(gate){Prune(now);return sessions.Values.OrderByDescending(x=>x.LastSeen).ToArray();}}
}
