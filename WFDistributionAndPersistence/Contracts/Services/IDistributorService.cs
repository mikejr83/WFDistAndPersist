using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Contracts.Models;

namespace Contracts.Services
{
  [ServiceContract(Name = "IDistributorService", Namespace = "http://www.testuri.com/schemas/services/IDistributorService")]
  public interface IDistributorService
  {
    [OperationContract(IsOneWay = true)]
    void Invoke(DistributorRequestDTO dto);

    [OperationContract(IsOneWay = true)]
    void RegisterAuditEvent(AuditEventDTO dto);
  }
}
