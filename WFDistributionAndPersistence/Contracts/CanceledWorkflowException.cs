using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Contracts
{
  [Serializable]
  public class CanceledWorkflowException : Exception
  {
    public CanceledWorkflowException() : base() { }

    public CanceledWorkflowException(string message) : base(message) { }

    public CanceledWorkflowException(string message, Exception innerException) : base(message, innerException) { }

    public CanceledWorkflowException(SerializationInfo info, StreamingContext context) : base(info, context) { }
  }
}
