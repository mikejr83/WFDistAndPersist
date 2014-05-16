using System;
using System.ServiceModel;
using System.Threading;
using Contracts.Models;
using Contracts.Services;
using NLog;

namespace Client
{
  /// <summary>
  /// Handler for remote invocation of system job.
  /// </summary>
  [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
  public class ClientServiceHandler : IClientService
  {
    static Lazy<ClientServiceHandler> _Instance = new Lazy<ClientServiceHandler>(() => new ClientServiceHandler());

    public static ClientServiceHandler Instance { get { return _Instance.Value; } }

    public event EventHandler OnShutdown;

    static ClientServiceHandler()
    {

    }

    ClientServiceHandler() { }

    #region Private Members
    private static Logger _Logger = LogManager.GetCurrentClassLogger();
    #endregion

    /// <summary>
    /// Invoke remote jobs.
    /// </summary>
    /// <param name="request"></param>
    public virtual void Invoke(ClientRequestDTO request)
    {
      _Logger.Trace("Enter Invoke handler for client service.");
      _Logger.Debug("Received a ClientRequestDTO object with workflow instance id: {0} and bookmark id: {1}", request.WorkflowInstanceId, request.BookmarkId);
      TaskOps.Instance.AddRequest(request);

      _Logger.Trace("Exiting Invoke handler for client service.");
    }

    public virtual void Shutdown()
    {
      _Logger.Trace("Enter");
      if (this.OnShutdown != null)
      {
        _Logger.Debug("The handler has OnShutdown handlers. Invoking...");
        this.OnShutdown(this, EventArgs.Empty);
      }
      _Logger.Trace("Exit.");
    }
  }
}
