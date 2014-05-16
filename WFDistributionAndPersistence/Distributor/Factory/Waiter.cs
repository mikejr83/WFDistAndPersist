using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Distributor.Factory
{
  public class Waiter
  {
    AutoResetEvent _WaitEvent = new AutoResetEvent(false);

    public Action<WorkflowApplication> Resume { get; private set; }
        
    public bool WaitWorkflowApplication(Action<WorkflowApplication> onResume)
    {
      this.Resume = onResume;
      bool rVal = _WaitEvent.WaitOne();

      return rVal;
    }

    public void Set(WorkflowApplication workflowApplication)
    {
      _WaitEvent.Set();

      if (this.Resume != null)
        this.Resume(workflowApplication);
    }
  }
}
