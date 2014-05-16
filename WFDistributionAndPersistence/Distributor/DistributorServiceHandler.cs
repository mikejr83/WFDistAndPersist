using System;
using System.Activities;
using System.Activities.Hosting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.DurableInstancing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Contracts.Models;
using Contracts.Services;
using Distributor.Status;
using NLog;

namespace Distributor
{
  public class DistributorServiceHandler : IDistributorService
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();
    static object _LOCK = new object();
    static ConcurrentBag<string> _BookmarksToResume = new ConcurrentBag<string>();

    static DistributorServiceHandler()
    {
      Mapper.CreateMap<AuditEventDTO, AuditEvent>();
    }

    #region IDistributorService Members

    public void Invoke(DistributorRequestDTO dto)
    {
      _Logger.Trace("Enter Invoke handler for distributor service.");

      _Logger.Info("Received the following information: Bookmark Id: {0} - Workflow Instance Id: {1}", dto.BookmarkId, dto.WorkflowInstanceId);
      
      Factory.WFAppFactory.ResumeBookmark(dto.WorkflowInstanceId, dto.BookmarkId, dto);

      _Logger.Trace("Exiting Invoke handler for distributor service.");
    }

    public void RegisterAuditEvent(AuditEventDTO dto)
    {
      _Logger.Trace("Enter");

      AuditEvent auditEvent = Mapper.Map<AuditEvent>(dto);

      StatusOps.TrackAuditEvent(auditEvent);

      _Logger.Trace("Exit");
    }

    #endregion
  }
}
