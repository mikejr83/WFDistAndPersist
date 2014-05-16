using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distributor.Status
{
  public class StatusTracker : IEnumerable<AuditEvent>
  {
    ConcurrentBag<AuditEvent> _Events = new ConcurrentBag<AuditEvent>();
    
    public Guid CorrelationId { get; private set; }
    public string TrackerName { get; private set; }

    public StatusTracker(Guid correlationId, string name)
    {
      this.CorrelationId = correlationId;
      this.TrackerName = name;
    }

    public void RegisterAuditEvent(AuditEvent auditEvent)
    {
      this._Events.Add(auditEvent);
    }

    #region IEnumerable<AuditEvent> Members

    public IEnumerator<AuditEvent> GetEnumerator()
    {
      return this._Events.OrderByDescending(e => e.Timestamp).GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return this._Events.OrderByDescending(e => e.Timestamp).GetEnumerator();
    }

    #endregion
  }
}
