using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Contracts.Models;
using Contracts.Services;
using NLog;

namespace Distributor
{
  public class ClientServiceProxy : ClientBase<IClientService>, IClientService
  {
    Logger _Logger = LogManager.GetCurrentClassLogger();

    public ClientServiceProxy() : base(typeof(IClientService).FullName) { }

    #region IClientService Members

    public void Invoke(ClientRequestDTO request)
    {
      _Logger.Trace("Sending request to client service");
      this.Channel.Invoke(request);
      _Logger.Trace("Finished sending request to client service");
    }

    public void Shutdown()
    {
      _Logger.Trace("Enter");
      this.Channel.Shutdown();
      _Logger.Trace("Exit");
    }

    #endregion
  }
}
