using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace Contracts.Models
{
  [MessageContract(WrapperName = "AuditEventDTO", WrapperNamespace = "http://www.testuri.com/schemas/msgcontracts/AuditEventDTO")]
  public class AuditEventDTO
  {
    [MessageHeader]
    public string Message { get; set; }

    [MessageHeader]
    public string LogLevel { get; set; }

    [MessageHeader]
    public Exception Exception { get; set; }

    [MessageHeader]
    public DateTime Timestamp { get; set; }

    [MessageHeader]
    public Guid CorrelationId { get; set; }

    public AuditEventDTO()
    {
      this.LogLevel = "DEBUG";
      this.Timestamp = DateTime.Now;
    }
  }
}
