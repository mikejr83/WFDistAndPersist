using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contracts.Models;
using NLog;

namespace Distributor.Activities
{
  public class CallClient : ExecuteWebServiceActivity
  {
    
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    // If your activity returns a value, derive from CodeActivity<TResult>
    // and return the value from the Execute method.
    protected override Guid Execute(CodeActivityContext context)
    {
      _Logger.Trace("Entering Execute()");


      _Logger.Info("Sending request for the remote invoker.");

      Guid bookmarkId = Guid.NewGuid();
      //Guid workflowInstanceId = Guid.NewGuid();

      _Logger.Info("Talking to the client! Sending bookmark id: {0} and workflow instance id: {1}.", bookmarkId, context.WorkflowInstanceId);

      new ClientServiceProxy().Invoke(new ClientRequestDTO()
      {
        BookmarkId = bookmarkId.ToString(),
        WorkflowInstanceId = context.WorkflowInstanceId
      });

      _Logger.Trace("Exiting Execute()");
      return bookmarkId;
    }
  }
}
