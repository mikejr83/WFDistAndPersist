using System;
using System.Activities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distributor.Workflow
{
  /// <summary>
  /// Mapping of identities to activities.
  /// </summary>
  public class WorkflowIdentityMap
  {
    static ConcurrentDictionary<WorkflowIdentity, Activity> _IdentityMap = new ConcurrentDictionary<WorkflowIdentity, Activity>();

    static WorkflowIdentityMap()
    {
      //TODO: For Mef implementations this registration code should be dynamic.

      _IdentityMap.TryAdd(new WorkflowIdentity("Workflow1", new Version(1, 0), "Distributor"), new Workflow1());
      _IdentityMap.TryAdd(new WorkflowIdentity("Workflow2", new Version(1, 0), "Distributor"), new Workflow2());
      _IdentityMap.TryAdd(new WorkflowIdentity("Workflow3", new Version(1, 0), "Distributor"), new Workflow3());
    }

    /// <summary>
    /// Retrieve a workflow definition based on a given identity.
    /// </summary>
    /// <param name="identity"></param>
    /// <returns>Corresponding activity for the identity.</returns>
    public static Activity GetWorkflowDefinition(WorkflowIdentity identity)
    {
      return _IdentityMap[identity];
    }
  }
}
