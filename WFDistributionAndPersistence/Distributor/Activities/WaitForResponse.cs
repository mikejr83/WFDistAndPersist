using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Distributor.Activities
{
  public class WaitForResponse  : NativeActivity<object>
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    protected override bool CanInduceIdle { get { return true; } }

    public InArgument<string> BookmarkName { get; set; }

    protected override void Execute(NativeActivityContext context)
    {
      _Logger.Trace("Entering Execute()");
      _Logger.Debug("Creating a bookmark \"{0}\".", this.BookmarkName.Get(context));
      context.CreateBookmark(this.BookmarkName.Get(context), new BookmarkCallback(this.Callback));
      _Logger.Trace("Exiting Execute()");
    }

    void Callback(NativeActivityContext context, Bookmark bookmark, object value)
    {
      _Logger.Trace("Entering Execute()");
      this.Result.Set(context, value);
      _Logger.Trace("Exiting Execute()");
    }
  }
}
