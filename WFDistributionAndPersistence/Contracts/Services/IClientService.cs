using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Contracts.Models;

namespace Contracts.Services
{
  [ServiceContract(Name = "IClientService", Namespace = "http://www.testuri.com/schemas/services/IClientService")]
  public interface IClientService
  {
    /// <summary>
    /// Remotely invoke the job
    /// </summary>
    /// <param name="request"></param>
    [OperationContract(IsOneWay = true)]
    void Invoke(ClientRequestDTO request);

    [OperationContract(IsOneWay = true)]
    void Shutdown();
  }
}
