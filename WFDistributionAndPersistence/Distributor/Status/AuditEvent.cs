using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distributor.Status
{
  public class AuditEvent
  {
    public string Message { get; set; }

    public string LogLevel { get; set; }

    public Exception Exception { get; set; }

    public DateTime Timestamp { get; set; }

    public Guid CorrelationId { get; set; }

    public AuditEvent()
    {
      this.Timestamp = DateTime.Now;
      this.LogLevel = NLog.LogLevel.Debug.Name;
    }
  }
}
