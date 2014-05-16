using System;
using System.Activities;
using System.Activities.Expressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Distributor.Status;
using NLog;

namespace Distributor.Activities
{
  [Designer(typeof(CallAndWaitDesigner))]
  public class CallAndWait : NativeActivity
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    [Browsable(false)]
    [RequiredArgument]
    public ExecuteWebServiceActivity ExecuteWebServiceActivity { get; set; }
    [Browsable(false)]
    public ResponseHandlerActivity HandleResponseActivity { get; set; }

    public InArgument<string> TrackingDisplayName { get; set; }

    protected override bool CanInduceIdle { get { return true; } }

    Variable<object> _ResponseVariable;

    Tuple<Activity, ActivityAction<object>> _Action = null;

    public CallAndWait()
    {
      this._ResponseVariable = new Variable<object>();
    }

    protected override void CacheMetadata(NativeActivityMetadata metadata)
    {
      metadata.SetArgumentsCollection(metadata.GetArgumentsWithReflection());

      metadata.AddImplementationVariable(_ResponseVariable);

      if (this.HandleResponseActivity != null)
      {
        ActivityAction<object> wrapper = new ActivityAction<object>();
        DelegateInArgument<object> activityToActionBinderArg = new DelegateInArgument<object>();
        wrapper.Argument = activityToActionBinderArg;
        this.HandleResponseActivity.Response = activityToActionBinderArg;

        wrapper.Handler = this.HandleResponseActivity;
        metadata.AddDelegate(wrapper);

        _Action = new Tuple<Activity, ActivityAction<object>>(this.HandleResponseActivity, wrapper);
      }

      metadata.AddChild(this.ExecuteWebServiceActivity);
    }

    protected override void Execute(NativeActivityContext context)
    {
      _Logger.Trace("Enter");

      if (this.ExecuteWebServiceActivity != null)
      {
        context.ScheduleActivity<Guid>(this.ExecuteWebServiceActivity,
            new CompletionCallback<Guid>(this.ExecuteWebServiceActivityCompetionCallback),
            new FaultCallback(this.ExecuteWebServiceActivityFaultCallback));
      }
      _Logger.Trace("Exit");
    }

    void ExecuteWebServiceActivityCompetionCallback(NativeActivityContext context, ActivityInstance instance, Guid result)
    {
      _Logger.Trace("Enter");

      string bookmarkName = result.ToString();

      string trackingDisplayName = this.TrackingDisplayName.Get(context);

      StatusOps.CreateNewTracker(string.IsNullOrEmpty(trackingDisplayName) ? instance.Activity.DisplayName : trackingDisplayName, result);

      _Logger.Debug("Creating a bookmark \"{0}\".", bookmarkName);

      context.CreateBookmark(bookmarkName, new BookmarkCallback(this.Callback));
      _Logger.Trace("Exit");
    }

    void ExecuteWebServiceActivityFaultCallback(NativeActivityFaultContext context, Exception exception, ActivityInstance instance)
    {

    }

    void Callback(NativeActivityContext context, Bookmark bookmark, object value)
    {
      _Logger.Trace("Enter");

      this._ResponseVariable.Set(context, value);

      context.ScheduleAction(_Action.Item2, value);

      _Logger.Trace("Exit");
    }
  }
}
