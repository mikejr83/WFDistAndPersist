using System;
using System.Activities.Tracking;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Distributor
{
  public class StatusTrackingParticipant : TrackingParticipant
  {
    Logger _Logger = LogManager.GetCurrentClassLogger();

    protected override void Track(TrackingRecord record, TimeSpan timeout)
    {
      if (record is ActivityStateRecord)
        this.HandleActivityState(record as ActivityStateRecord);
      else if (record is WorkflowInstanceRecord)
        this.HandleWorflowInstanceTracking(record as WorkflowInstanceRecord);
      else if (record is ActivityScheduledRecord)
        this.HandleActivityScheduledRecord(record as ActivityScheduledRecord);
      else
      {

      }
    }

    void HandleActivityState(ActivityStateRecord asr)
    {
      if (asr != null)
      {
        switch (asr.State)
        {
          case ActivityStates.Executing:
            _Logger.Trace("Executing: " + asr.Activity.Name);
            break;

          case ActivityStates.Closed:
            _Logger.Trace("Closed: " + asr.Activity.Name);
            break;

          case ActivityStates.Canceled:
            _Logger.Debug("Canceled: " + asr.Activity.Name);
            break;

          case ActivityStates.Faulted:
            _Logger.Debug("Faulted: " + asr.Activity.Name);
            break;
          default:
            break;
        }
      }
    }

    void HandleWorflowInstanceTracking(WorkflowInstanceRecord record)
    {

      //InstallerLogger.LogWorkflowInformation(record);
    }

    void HandleActivityScheduledRecord(ActivityScheduledRecord record)
    {
      if (record.Activity == null) return;

      //InstallerLogger.PrepareLoggingNode(record.InstanceId, record.Activity, record.Child);
    }
  }
}
