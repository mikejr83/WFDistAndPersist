using System;
using System.Activities;
using System.Activities.DurableInstancing;
using System.Activities.Tracking;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.DurableInstancing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Contracts;
using Distributor.Workflow;
using NLog;

namespace Distributor.Factory
{
  public class WFAppFactory
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();
    static object _LOCK = new object();
    static WaitCollection Waits = new WaitCollection();
    static ConcurrentDictionary<Guid, WorkflowApplication> _WFCache = new ConcurrentDictionary<Guid, WorkflowApplication>();

    public static readonly SqlWorkflowInstanceStore Store = null;

    static WFAppFactory()
    {
      string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnString"].ConnectionString;
      _Logger.Debug("Connection string for workflow persistence: " + connectionString);
      Store = new SqlWorkflowInstanceStore(connectionString)
      {
        InstanceCompletionAction = InstanceCompletionAction.DeleteNothing
        //,InstanceEncodingOption = InstanceEncodingOption.GZip 
      };

      try
      {
        InstanceHandle instanceHandle = Store.CreateInstanceHandle();
        InstanceView view = Store.Execute(instanceHandle, new CreateWorkflowOwnerCommand(), TimeSpan.FromSeconds(30));
        instanceHandle.Free();
        Store.DefaultInstanceOwner = view.InstanceOwner;
      }
      catch (Exception ex)
      {
        _Logger.FatalException("Cannot setup the instance handle!", ex);
        throw;
      }
    }

    static void ConfigureWorkflowApplication(WorkflowApplication wfApp)
    {
      wfApp.InstanceStore = Store;

      /*
       * Setting up the handlers for workflow actions.
       * 
       * Aborted - Invoked when the workflow instance is aborted.
       * Completed - Invoked when the workflow instance is completed. This will follow the other handlers.
       * OnUnhandledException - Invoked when the workflow instance encounters an unhandled exception
       * PersitableIdle - Invoked when the workflow instance is idle and can be persisted.
       * Unloaded - Executed when the workflow is unloaded.
       */
      wfApp.Aborted = WFAppFactory.HandleWorkflowAborted;
      wfApp.Completed = WFAppFactory.WorkflowCompleted;
      wfApp.OnUnhandledException = WFAppFactory.HandleWorkflowUnhandledException;
      wfApp.PersistableIdle = WFAppFactory.PersistableIdleHandler;
      wfApp.Unloaded = WFAppFactory.HandleWorkflowUnloaded;

      #region Extensions
      StatusTrackingParticipant stp = new StatusTrackingParticipant
      {
        TrackingProfile = new TrackingProfile
        {
          Queries = 
          {
            //Querying all states. 
            new ActivityStateQuery
            {
                ActivityName = "*",
                States = { "*" }
            },
            //Querying all workflow instances ? not sure on the terminology here.
            new WorkflowInstanceQuery
            {
              States = { WorkflowInstanceStates.Aborted,
                WorkflowInstanceStates.Canceled,
                WorkflowInstanceStates.Completed,
                WorkflowInstanceStates.Deleted,
                WorkflowInstanceStates.Idle, 
                WorkflowInstanceStates.Persisted, 
                WorkflowInstanceStates.Resumed, 
                WorkflowInstanceStates.Started, 
                WorkflowInstanceStates.Suspended, 
                WorkflowInstanceStates.Terminated,
                WorkflowInstanceStates.UnhandledException, 
                WorkflowInstanceStates.Unloaded, 
                WorkflowInstanceStates.Unsuspended, 
                WorkflowInstanceStates.Updated,
                WorkflowInstanceStates.UpdateFailed }
            },
            //querying all activty schedules ? not sure on the terminology here.
            new ActivityScheduledQuery
            {
              ActivityName = "*",
              ChildActivityName = "*"
            }
          }
        }
      };

      /*
       * Adding the status tracking participant to the workflow application's extensions.
       */
      wfApp.Extensions.Add(stp);

      StringWriter sw = new StringWriter();
      wfApp.Extensions.Add(sw);
      #endregion
    }

    static IEnumerable<ActivityArgument> FindActivityArguements(Activity activity)
    {
      PropertyInfo[] pInfos = activity.GetType().GetProperties();

      var oh = pInfos.Select(pi => ActivityArgument.CreateFromPropertyInfo(pi)).Where(a => a.Name != "Id" && a.Name != "DisplayName");

      return oh;
    }

    #region Actions
    /// <summary>
    /// Run a workflow application with an activity that is defined by the given identity.
    /// </summary>
    /// <param name="identity"></param>
    public static void RunWorkflow(WorkflowIdentity identity)
    {
      lock (_LOCK)
      {
        WorkflowApplication wfApp = new WorkflowApplication(WorkflowIdentityMap.GetWorkflowDefinition(identity), identity);

        ConfigureWorkflowApplication(wfApp);

        wfApp.Run();

        _WFCache.TryAdd(wfApp.Id, wfApp);
      }
    }

    /// <summary>
    /// Abort a workflow based on its instance ID.
    /// </summary>
    /// <param name="workflowInstanceId"></param>
    public static void AbortWorkflow(Guid workflowInstanceId)
    {
      _Logger.Trace("Enter");

      lock (_LOCK)
      {
        WorkflowApplication wfApp = LoadWorkflowApplication(workflowInstanceId);

        _Logger.Info("Aborting workflow...");

        wfApp.Abort("Aborting from console.");
      }

      _Logger.Trace("Exit");
    }

    /// <summary>
    /// Terminate a workflow based on its instance ID.
    /// </summary>
    /// <param name="workflowInstanceId"></param>
    public static void TerminateWorkflow(Guid workflowInstanceId)
    {
      _Logger.Trace("Enter");

      lock (_Logger)
      {
        WorkflowApplication wfApp = LoadWorkflowApplication(workflowInstanceId);

        _Logger.Debug("Executing the termination command.");

        try
        {
          wfApp.Terminate(new CanceledWorkflowException("Canceled from the console."));
        }
        catch (Exception e)
        {
          _Logger.ErrorException("Error while trying to terminate.", e);
        }
      }

      _Logger.Trace("Exit");
    }

    public static void CancelWorkflow(Guid workflowInsanceId)
    {
      lock(_LOCK)
      {
        WorkflowApplication wfApp = LoadWorkflowApplication(workflowInsanceId);

        wfApp.Cancel();
      }
    }

    /// <summary>
    /// Resume a bookmark in a workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The instance identifier of the workflow.</param>
    /// <param name="bookmarkName">The bookmark to resume.</param>
    public static void ResumeBookmark(Guid workflowInstanceId, string bookmarkName)
    {
      /*
       * Resume the bookmark with no set value for the callback method.
       */
      ResumeBookmark(workflowInstanceId, bookmarkName, null);
    }

    /// <summary>
    /// Resumes a bookmark with a given value for a workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The instance identifier of the workflow.</param>
    /// <param name="bookmarkName">The bookmark to resume.</param>
    /// <param name="value">The value to be passed to the bookmark's callback function.</param>
    public static void ResumeBookmark(Guid workflowInstanceId, string bookmarkName, object value)
    {
      _Logger.Trace("Enter");

      BookmarkResumptionResult result = BookmarkResumptionResult.NotFound;
      WorkflowApplication wfApp = null;

      /*
       * Attempt to load the workflow application. Handle the InstanceCompleteException in case the WorkflowApplication
       * has been completed.
       */
      try
      {
        wfApp = LoadWorkflowApplication(workflowInstanceId);
      }
      catch (InstanceCompleteException)
      {
        _Logger.Warn("The instance {0} is complete. Cannot resume {1} bookmark.", workflowInstanceId, bookmarkName);
        return;
      }

      /*
       * Once the WorkflowApplication has been loaded resume the bookmark.
       */
      try
      {
        result = wfApp.ResumeBookmark(bookmarkName, value);
      }
      catch (Exception e)
      {
        _Logger.ErrorException("Couldn't resume the bookmark due to an exception!", e);
        throw;
      }

      switch (result)
      {
        case BookmarkResumptionResult.NotFound:
          break;
        case BookmarkResumptionResult.NotReady:

          break;
        case BookmarkResumptionResult.Success:
          break;
        default:
          break;
      }

      _Logger.Debug("Resumption Result: {0}", result);


      _Logger.Trace("Exit");
    }
    #endregion

    static WorkflowApplication LoadWorkflowApplication(Guid workflowInstanceId)
    {
      WorkflowApplication workflowApplication = null;
      WorkflowApplicationInstance runningInstance = null;
      Waiter waiter = null;

      /*
       * Depending on when the load is requested there are two ways of performing the load. 
       * 
       * The easiest is when the workflow application is about to persist and unload a check is made to see if there 
       * are any waits on the workflow. If a wait exists then it is released with a reference to the workflow application 
       * and the persist and unload  functionality is deferred.
       * 
       * If a workflow has been unloaded then a runnable instance of the workflow application must be loaded from which
       * the application itself can then be loaded with the instance information.
       * 
       * The following code will wait until either a workflow application or a WorkflowApplicationInstance is handed
       * back. The wait could produce a condition where a wait is registered after the persist but before the 
       * workflow application is unloaded. The "Unloaded" method will do a final check and see that a wait exists. At
       * that point, however, the workflow application is essentially unloaded and can be thrown away (we do not want 
       * to transfer it to another thread context for processing). The "Unloaded" handler will then release the lock
       * without any workflow application allowing the while loop to see that the lock has been released but there
       * is no workflow application or instance. The next iteration of the loop should pick up a runnable instance
       * object which will reload the workflow application.
       */
      try
      {
        while (workflowApplication == null && runningInstance == null)
        {
          if (!TryLoadInstance(workflowInstanceId, out runningInstance, out waiter))
          {
            _Logger.Info("Waiting for instance to become free.");
            waiter.WaitWorkflowApplication((wfApp) => workflowApplication = wfApp);
            _Logger.Debug("Thread {0} coming back online for processing.", Thread.CurrentThread.ManagedThreadId);
          }
        }
      }
      catch (InstanceCompleteException)
      {
        /*
         * Catch the instance completion method just to log this. Since the LoadWorkflowApplication is internal this class
         * the other methods should handle this case appropriately (so this is rethrown).
         */
        _Logger.Debug("Instance is complete. Cannot get instance.");
        throw;
      }

      /*
       * Debugging feedback regarding the out come of the above workflow application or instance loading.
       */
      if (runningInstance != null)
      {
        _Logger.Debug("The running instance was found!");
      }
      else if (workflowApplication != null)
      {
        _Logger.Debug("Workflow application handed back from the waiter successfully!");
      }
      else
        _Logger.Warn("An instance for ID {0} couldn't be found. The waiter did not return a workflow application either!");

      /*
       * In the case where a workflow application is not in memory the instance is used to create a new object.
       */
      if (workflowApplication == null)
      {
        if (runningInstance.DefinitionIdentity != null)
        {
          _Logger.Debug("The running instance has a workflow identity name of {0}", runningInstance.DefinitionIdentity.Name);
        }
        else
          _Logger.Warn("The running instance does not have a definition identity!");

        #region Instantiate Activity
        _Logger.Info("Resuming the {0} instance of the {1} workflow.", workflowInstanceId, runningInstance.DefinitionIdentity.Name);

        Activity workflowActivity = null;

        /*
         * A map of workflow identities to activities is maintained. The instance's definition identity is used to get a definition
         * of the workflow activity to create the workflow application object.
         */
        try
        {
          workflowActivity = WorkflowIdentityMap.GetWorkflowDefinition(runningInstance.DefinitionIdentity);
        }
        catch (Exception e)
        {
          _Logger.ErrorException("Error getting the workflow definition.", e);
          throw;
        }
        #endregion

        #region Create and Load

        try
        {
          _Logger.Debug("Creating the WorkflowApplication object.");
          workflowApplication = new WorkflowApplication(workflowActivity, runningInstance.DefinitionIdentity);
        }
        catch (Exception e)
        {
          _Logger.ErrorException("Error when creating the workflow application.", e);
          throw;
        }

        /*
         * Once the application has been instantiated the standard configuration for durable instancing,
         * handling of state changes and events, etc is performed.
         */
        try
        {
          _Logger.Debug("Configure the WorkflowApplication object.");
          WFAppFactory.ConfigureWorkflowApplication(workflowApplication);
        }
        catch (Exception e)
        {
          _Logger.ErrorException("Error while attempting to configure the workflow application.", e);
          throw;
        }

        /*
         * Load the workflow application from the running instance.
         */
        try
        {
          _Logger.Debug("Loading the workflow...");
          workflowApplication.Load(runningInstance);
        }
        catch (InvalidOperationException e)
        {
          _Logger.ErrorException("Error while attempting to load the running instance of the workflow application.", e);
        }
        catch (InstanceHandleConflictException e)
        {
          _Logger.ErrorException("Error while attempting to load the running instance of the workflow application.", e);
          _Logger.Debug("Going to guess that I can't resume the workflow because I need to go to the state for a different bookmark.");
        }
        catch (Exception e)
        {
          _Logger.ErrorException("Error while attempting to load the running instance of the workflow application.", e);
          throw;
        }
        #endregion
      }

      /*
       * Add the now loaded workflow application to the cache of running workflows. This will allow the persist method to determine
       * if it needs to persist and unload or to do nothing and defer processing to a waiting thread passing the workflow application
       * to that context.
       */
      _WFCache.TryAdd(workflowInstanceId, workflowApplication);

      return workflowApplication;
    }

    /// <summary>
    /// Tries to load a Workflow Application's instance information. If this is not possible a waiter is registered.
    /// </summary>
    /// <param name="workflowInstanceId">The instance identifier for the workflow.</param>
    /// <param name="wfAppInstance">The instance that will be handed back.</param>
    /// <param name="waiter">A waiter which will be automatically registered if an instance is in use.</param>
    /// <returns>Success or failure for the loading of the instance.</returns>
    static bool TryLoadInstance(Guid workflowInstanceId, out WorkflowApplicationInstance wfAppInstance, out Waiter waiter)
    {
      wfAppInstance = null;
      waiter = null;
      bool loaded = false;

      try
      {
        wfAppInstance = WorkflowApplication.GetInstance(workflowInstanceId, WFAppFactory.Store);
        loaded = true;
      }
      catch (InstanceNotReadyException)
      {
        _Logger.Info("Instance isn't ready.");
      }
      catch (InstanceHandleConflictException)
      {
        _Logger.Info("Conflict on loading the instance.");
      }
      catch (InstanceCompleteException)
      {
        _Logger.Warn("Instance {0} has been completed.", workflowInstanceId);
        throw;
      }
      catch (Exception e)
      {
        _Logger.Error("An exception occurred while waiting to get the instance.");
        _Logger.ErrorException("Error getting an the running instance.", e);
        throw;
      }

      if (!loaded)
      {
        lock (_LOCK)
        {
          waiter = Waits.RegisterWait(workflowInstanceId);
        }
      }

      return loaded;
    }

    #region Workflow Application Handlers
    static void HandleWorkflowUnloaded(WorkflowApplicationEventArgs args)
    {
      _Logger.Trace("Enter");

      HandleStringWriterOutput(args.GetInstanceExtensions<StringWriter>());

      /*
       * In a serialized manner remove the current workflow application from the workflow cache.
       * It is unloaded so it is no longer needed. Check to see if any waits have been registered
       * for this workflow application.  If there is handle the next wait for the workflow instance
       * identifier. Using the identifier to cause a null workflow applciation object to be passed
       * to the waiter. This should cause an instance to be loaded instead of trying to reuse an 
       * unloaded workflow application.
       */
      lock (_LOCK)
      {
        WorkflowApplication wfApp = null;
        if (_WFCache.ContainsKey(args.InstanceId))
          _WFCache.TryRemove(args.InstanceId, out wfApp);

        if (Waits.DoesWorkflowHaveWaits(args.InstanceId))
          _Logger.Debug("Unloaded with waits?!");

        Waits.HandleNextWait(args.InstanceId);
      }

      _Logger.Info(string.Format("Workflow instance {0} is unloaded.", args.InstanceId));
      _Logger.Trace("Exit");
    }

    static void HandleWorkflowAborted(WorkflowApplicationAbortedEventArgs e)
    {
      _Logger.Trace("Enter");

      HandleStringWriterOutput(e.GetInstanceExtensions<StringWriter>());

      _Logger.WarnException(string.Format("Aborting workflow instance {0}.", e.InstanceId), e.Reason);

      _Logger.Trace("Exit");
    }

    static void WorkflowCompleted(WorkflowApplicationCompletedEventArgs args)
    {
      _Logger.Trace("Enter");

      HandleStringWriterOutput(args.GetInstanceExtensions<StringWriter>());

      _Logger.Info(string.Format("Workflow {0} completed. State: {1}", args.InstanceId, args.CompletionState));

      switch (args.CompletionState)
      {
        case ActivityInstanceState.Canceled:
          _Logger.Warn("Workflow has now entered the canceled state!");
          break;
        case ActivityInstanceState.Closed:
          _Logger.Info("The workflow instance {0} has closed.", args.InstanceId);
          break;
        case ActivityInstanceState.Executing:
          _Logger.Info("The workflow instance {0} is executing...", args.InstanceId);
          break;
        case ActivityInstanceState.Faulted:
          if (args.TerminationException is CanceledWorkflowException)
            _Logger.Warn("The workflow was canceled. Reason: {0}", (args.TerminationException as CanceledWorkflowException).Message);
          else
            _Logger.ErrorException("The workflow completed in a faulted state.", args.TerminationException);
          break;
        default:
          break;
      }

      _Logger.Trace("Exit");
    }

    static PersistableIdleAction PersistableIdleHandler(WorkflowApplicationIdleEventArgs args)
    {
      _Logger.Trace("Enter");
      HandleStringWriterOutput(args.GetInstanceExtensions<StringWriter>());

      bool hasWaits = false;

      /*
       * In a serialized manner check the workflow instance for any waits before determining how the workflow 
       * should be persisted and unloaded. If there are waits then the workflow application should not be persisted
       * or unloaded. Instead the workflow application object should be handed off to the next waiter.
       * 
       * If there are no waits then persist and unload the workflow so that it can be resumed later if need be.
       */
      lock (_LOCK)
      {
        if (hasWaits = Waits.DoesWorkflowHaveWaits(args.InstanceId))
        {
          _Logger.Debug("The instance {0} has waits. Not going to persist the workflow and unload it.", args.InstanceId);

          Waits.HandleNextWait(_WFCache[args.InstanceId]);
        }
        else
          _Logger.Debug("No waits were found for {0}.", args.InstanceId);
      }

      return hasWaits ? PersistableIdleAction.None : PersistableIdleAction.Unload;

    }

    static UnhandledExceptionAction HandleWorkflowUnhandledException(WorkflowApplicationUnhandledExceptionEventArgs e)
    {
      _Logger.Trace("Enter");

      HandleStringWriterOutput(e.GetInstanceExtensions<StringWriter>());

      _Logger.ErrorException(string.Format("An exception occurred in {0} activity in the {1} workflow instance.", e.ExceptionSource.DisplayName, e.InstanceId), e.UnhandledException);

      return UnhandledExceptionAction.Terminate;
    }

    static void HandleStringWriterOutput(IEnumerable<StringWriter> writers)
    {
      foreach (var writer in writers)
      {
        string text = writer.ToString();
        if (!string.IsNullOrWhiteSpace(text))
          _Logger.Debug(text);
      }

    }
    #endregion

    class ActivityArgument
    {
      public static ActivityArgument CreateFromPropertyInfo(PropertyInfo pi)
      {
        ActivityArgument aa = null;

        if (pi.PropertyType.IsGenericType && pi.PropertyType.BaseType == typeof(InArgument))
        {
          aa = new ActivityArgument(pi.Name, pi.PropertyType.GenericTypeArguments.FirstOrDefault());
        }
        else
          aa = new ActivityArgument(pi.Name, pi.PropertyType);

        return aa;
      }

      public string Name { get; private set; }
      public Type Type { get; private set; }

      public ActivityArgument(string name, Type type)
      {
        this.Name = name;
        this.Type = type;
      }
    }
  }
}
