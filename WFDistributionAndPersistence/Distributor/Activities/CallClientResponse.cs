using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contracts.Models;
using NLog;

namespace Distributor.Activities
{
  public class CallClientResponse : ResponseHandlerActivity
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    protected override void Execute(System.Activities.CodeActivityContext context)
    {
      _Logger.Trace("Enter");

      object response = this.Response.Get(context);

      DistributorRequestDTO requestDTO = response as DistributorRequestDTO;

      if (requestDTO != null)
        _Logger.Info("Received response DTO. BookmarkId: {0} - WorkflowInstanceId: {1}", requestDTO.BookmarkId, requestDTO.WorkflowInstanceId);
      else
        _Logger.Info("Received an object response: {0}", response.ToString());

      _Logger.Trace("Enter");
    }
  }
}
