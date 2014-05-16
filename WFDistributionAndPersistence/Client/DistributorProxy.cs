using System.ServiceModel;
using Contracts.Models;
using Contracts.Services;
using NLog;

namespace Client
{
  public class DistributorProxy : ClientBase<IDistributorService>, IDistributorService
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    public DistributorProxy() : base(typeof(IDistributorService).FullName) { }

    #region IDistributorService Members

    public void Invoke(DistributorRequestDTO dto)
    {
      _Logger.Trace("Sending invoke request to distributor service");
      this.Channel.Invoke(dto);
      _Logger.Trace("Finished sending invoke request to distributor service");
    }

    public void RegisterAuditEvent(AuditEventDTO dto)
    {
      _Logger.Trace("Enter");
      this.Channel.RegisterAuditEvent(dto);
      _Logger.Trace("Exit");
    }

    #endregion
  }
}
