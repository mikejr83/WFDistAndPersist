using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distributor.Activities
{
  public abstract class ResponseHandlerActivity : CodeActivity
  {
    [Browsable(false)]
    public InArgument<object> Response { get; set; }
  }
}
