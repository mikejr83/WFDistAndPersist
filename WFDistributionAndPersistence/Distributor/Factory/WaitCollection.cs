using System;
using System.Activities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Distributor.Factory
{
  public class WaitCollection
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();
    static object _LOCK = new Object();

    ConcurrentDictionary<Guid, ConcurrentQueue<Waiter>> _Waits = new ConcurrentDictionary<Guid, ConcurrentQueue<Waiter>>();

    /// <summary>
    /// Checks for waits for a given workflow identifier.
    /// </summary>
    /// <param name="workflowInstanceId">Workflow instance identifier</param>
    /// <returns>True - Waits exist / False - Waits do not exist.</returns>
    public bool DoesWorkflowHaveWaits(Guid workflowInstanceId)
    {
      _Logger.Trace("Enter");
      return this._Waits.ContainsKey(workflowInstanceId) && this._Waits[workflowInstanceId] != null && this._Waits[workflowInstanceId].Count > 0;
    }

    public Waiter RegisterWait(Guid workflowInstanceId)
    {
      _Logger.Trace("Enter");
      lock(_LOCK)
      {
        Waiter waiter = new Waiter();

        _Logger.Debug("Thread ID: {0} Registering wait for {1}.", Thread.CurrentThread.ManagedThreadId, workflowInstanceId);
        if(this._Waits.ContainsKey(workflowInstanceId))
        {
          if (this._Waits[workflowInstanceId] == null)
            this._Waits[workflowInstanceId] = new ConcurrentQueue<Waiter>();

          this._Waits[workflowInstanceId].Enqueue(waiter);
        }
        else
        {
          ConcurrentQueue<Waiter> queue = new ConcurrentQueue<Waiter>();
          queue.Enqueue(waiter);
          this._Waits.TryAdd(workflowInstanceId, queue);
        }

        return waiter;
      }
    }

    /// <summary>
    /// Handle the next wait for a workflow application in memory. The workflow application will be handed to the thread context.
    /// </summary>
    /// <param name="wfApp"></param>
    public void HandleNextWait(WorkflowApplication wfApp)
    {
      _Logger.Trace("Enter");
      lock(_LOCK)
      {
        _Logger.Debug("Handling wait for {0}.", wfApp.Id);
        Waiter waiter = null;
        if (this.DoesWorkflowHaveWaits(wfApp.Id) && this._Waits[wfApp.Id].TryDequeue(out waiter))
          waiter.Set(wfApp);
      }
      _Logger.Trace("Exit");
    }

    /// <summary>
    /// Handle the next wait for a workflow instance. The workflow application will not be passed to the waiting method.
    /// </summary>
    /// <param name="workflowInstanceId">Workflow identifier</param>
    public void HandleNextWait(Guid workflowInstanceId)
    {
      _Logger.Trace("Enter");
      lock (_LOCK)
      {
        _Logger.Debug("Handling wait for {0}.", workflowInstanceId);
        Waiter waiter = null;
        if (this.DoesWorkflowHaveWaits(workflowInstanceId) && this._Waits[workflowInstanceId].TryDequeue(out waiter))
          waiter.Set(null);
      }
      _Logger.Trace("Exit");
    }
  }
}
