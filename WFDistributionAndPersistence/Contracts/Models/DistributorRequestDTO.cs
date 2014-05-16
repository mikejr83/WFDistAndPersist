using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace Contracts.Models
{
  [MessageContract(WrapperName = "DistributorRequestDTO", WrapperNamespace = "http://www.testuri.com/schemas/msgcontracts/DistributorRequestDTO")]
  public class DistributorRequestDTO
  {
    [MessageHeader]
    public string BookmarkId { get; set; }

    [MessageHeader]
    public Guid WorkflowInstanceId { get; set; }

  }
}
