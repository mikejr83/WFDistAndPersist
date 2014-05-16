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
  public class CallClientResponseBookmarkOut : ResponseHandlerActivity
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    public OutArgument<Guid> BookmarkId { get; set; }

    protected override void Execute(System.Activities.CodeActivityContext context)
    {
      _Logger.Trace("Enter");

      object response = this.Response.Get(context);

      DistributorRequestDTO requestDTO = response as DistributorRequestDTO;

      this.BookmarkId.Set(context, new Guid(requestDTO.BookmarkId));

      _Logger.Trace("Enter");
    }
  }
}
