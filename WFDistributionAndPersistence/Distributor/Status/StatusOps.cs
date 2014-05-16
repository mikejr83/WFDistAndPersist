using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distributor.Status
{
  public class StatusOps
  {
    static ConcurrentDictionary<Guid, StatusTracker> _Trackers = new ConcurrentDictionary<Guid, StatusTracker>();

    public static void TrackAuditEvent(AuditEvent auditEvent)
    {
      if (_Trackers.ContainsKey(auditEvent.CorrelationId))
        _Trackers[auditEvent.CorrelationId].RegisterAuditEvent(auditEvent);
      else
        throw new NotImplementedException();
    }

    public static void CreateNewTracker(string name, Guid correlationId)
    {
      StatusTracker tracker = new StatusTracker(correlationId, name);

      tracker.RegisterAuditEvent(new AuditEvent() { CorrelationId = correlationId, Message = string.Format("Tracker {0} created.", name) });

      _Trackers.TryAdd(correlationId, tracker);
    }
  }
}
