using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace Contracts.Models
{
  [MessageContract(WrapperName = "ClientRequestDTO", WrapperNamespace = "http://www.sumtotalsystems.com/schemas/msgcontracts/ClientRequestDTO")]
  public class ClientRequestDTO
  {
    /// <summary>
    /// Gets or sets the correlation id for this request
    /// </summary>
    [MessageHeader]
    public string BookmarkId { get; set; }

    [MessageHeader]
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the SystemAgentTask associated to the remote invoker
    /// </summary>
    //[MessageBodyMember(Name = "SystemAgentJob", Order = 1)]
    //public SystemAgentJob SystemAgentJob { get; set; } 
  }
}
