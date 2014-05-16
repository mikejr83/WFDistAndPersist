using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Reflection;
using System.Runtime.DurableInstancing;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using Contracts.Models;
using NLog;

namespace Distributor
{
  class Program
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    ServiceHost _DistributorHost = null;

    //new SqlWorkflowInstanceStore(ConfigurationManager.ConnectionStrings["DefaultConnString"].ConnectionString);
    
    static void Main(string[] args)
    {
      _Logger.Info("Distributor Startup...");

      Program p = new Program();

      try
      {
        p.StartDistributorSvcHost();
      }
      catch (Exception e)
      {
        _Logger.ErrorException("Error when starting the svc host.", e);
      }

      

      string readString = null;
      while(readString == null || !readString.Equals("q", StringComparison.OrdinalIgnoreCase))
      {
        readString = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(readString))
        {
          _Logger.Debug("No command given!");
          continue;
        }
        else
          _Logger.Debug("{0} command given.", readString);

        switch(readString.ToLowerInvariant().FirstOrDefault())
        {
          case 'a':
            p.AbortWorkflow(readString);
            break;

          case 't':
            p.TerminateWorkflow(readString);
            break;

          case 'q':
            new ClientServiceProxy().Shutdown();
            continue;

          case 'c':
            p.CancelWorkflow(readString);
            break;

          case 'w':
            try
            {
              p.DoWorkflow(readString);
            }
            catch(Exception e)
            {
              _Logger.ErrorException("Error when invoking workflow.", e);
            }
            break;

          default:
            try
            {
              p.TalkToClient();
            }
            catch (Exception e)
            {
              _Logger.ErrorException("Error when sending message to client.", e);
            }
            break;
        }        
      }
      
      _Logger.Info("Stopping Distributor...");
    }

    void TalkToClient()
    {
      string bookmarkId = Guid.NewGuid().ToString();
      Guid workflowInstanceId = Guid.NewGuid();

      _Logger.Info("Talking to the client! Sending bookmark id: {0} and workflow instance id: {1}.", bookmarkId, workflowInstanceId);

      new ClientServiceProxy().Invoke(new ClientRequestDTO()
      {
        BookmarkId = bookmarkId,
        WorkflowInstanceId = workflowInstanceId
      });
    }

    void DoWorkflow(string inputLine)
    {
      char op = inputLine.Length > 1 ? inputLine[1] : '1';

      switch (op)
      {
        case '1':
          _Logger.Info("Invoking workflow #1.");
          Distributor.Factory.WFAppFactory.RunWorkflow(new System.Activities.WorkflowIdentity("Workflow1", new Version(1, 0), "Distributor"));
          break;

        case '2':
          _Logger.Info("Invoking workflow #2.");
          Distributor.Factory.WFAppFactory.RunWorkflow(new System.Activities.WorkflowIdentity("Workflow2", new Version(1, 0), "Distributor"));
          break;

        case '3':
          _Logger.Info("Invoking workflow #3.");
          Distributor.Factory.WFAppFactory.RunWorkflow(new System.Activities.WorkflowIdentity("Workflow3", new Version(1, 0), "Distributor"));
          break;

        default:
          throw new NotImplementedException();
          break;
      }
      
    }

    void AbortWorkflow(string inputLine)
    {      
      inputLine = inputLine.Substring(1).Trim();

      Guid instanceId;

      if (Guid.TryParse(inputLine, out instanceId))
      {
        _Logger.Info("Aborting the {0} workflow.", instanceId);

        Factory.WFAppFactory.AbortWorkflow(instanceId);
      }
      else
        _Logger.Error("Couldn't parse the workflow instance id.");

    }

    void TerminateWorkflow(string inputLine)
    {
      inputLine = inputLine.Substring(1).Trim();

      Guid instanceId;

      if (Guid.TryParse(inputLine, out instanceId))
      {
        _Logger.Info("Terminating the {0} workflow.", instanceId);

        Factory.WFAppFactory.TerminateWorkflow(instanceId);
      }
      else
        _Logger.Error("Couldn't parse the workflow instance id.");
    }

    void CancelWorkflow(string inputLine)
    {
      inputLine = inputLine.Substring(1).Trim();

      Guid instanceId;

      if (Guid.TryParse(inputLine, out instanceId))
      {
        _Logger.Info("Canceling the {0} workflow.", instanceId);

        Factory.WFAppFactory.CancelWorkflow(instanceId);
      }
      else
        _Logger.Error("Couldn't parse the workflow instance id.");
    }

    void StartDistributorSvcHost()
    {
      this._DistributorHost = new ServiceHost(typeof(DistributorServiceHandler));

      foreach (var endpoint in this._DistributorHost.Description.Endpoints)
      {
        this.InitializeMSMQ(endpoint.Address.Uri);
      }

      string svcHostAddress =
        string.Join(", ", this._DistributorHost.Description.Endpoints.Select(a => a.Address.ToString()).ToArray());
      _Logger.Debug("Preparing to listen for commands at address: {0}", svcHostAddress);

      this._DistributorHost.Open();
      _Logger.Debug("Listening for commands at address: {0}", svcHostAddress);
    }

    void InitializeMSMQ(Uri address)
    {
      string path = address.PathAndQuery.Replace("private", "private$");
      path = path.Replace("/", "\\");

      bool foundQueue = false;
      foreach (MessageQueue queue in MessageQueue.GetPrivateQueuesByMachine(Environment.MachineName))
      {
        if (queue.Path.EndsWith(path))
        {
          foundQueue = true;
          break;
        }
      }

      if (!foundQueue)
      {
        path = "." + path;
        MessageQueue queue = MessageQueue.Create(path, true);
        queue.Label = path.Substring(2);

        foreach (MessageQueueAccessControlEntry entry in this.LoadAccessControlEntries()) { queue.SetPermissions(entry); }
      }
    }

    protected IEnumerable<MessageQueueAccessControlEntry> LoadAccessControlEntries()
    {
      MessageQueueAccessRights baseRights = MessageQueueAccessRights.DeleteJournalMessage | MessageQueueAccessRights.DeleteMessage |
        MessageQueueAccessRights.GenericRead | MessageQueueAccessRights.GenericWrite | MessageQueueAccessRights.GetQueueProperties |
        MessageQueueAccessRights.PeekMessage | MessageQueueAccessRights.ReceiveJournalMessage | MessageQueueAccessRights.ReceiveMessage |
        MessageQueueAccessRights.WriteMessage;

      List<MessageQueueAccessControlEntry> accessEntries = new List<MessageQueueAccessControlEntry>();

      var currentIdentity = WindowsIdentity.GetCurrent();

      Trustee currentTrustee = new Trustee(currentIdentity.Name, Environment.MachineName, TrusteeType.User);
      Trustee everyoneTrustee = new Trustee("Everyone") { TrusteeType = TrusteeType.Group };
      Trustee adminTrustee = new Trustee("Administrators", Environment.MachineName, TrusteeType.Group);

      accessEntries.Add(new MessageQueueAccessControlEntry(currentTrustee,
        MessageQueueAccessRights.FullControl, AccessControlEntryType.Revoke));
      accessEntries.Add(new MessageQueueAccessControlEntry(currentTrustee,
        baseRights, AccessControlEntryType.Allow));

      accessEntries.Add(new MessageQueueAccessControlEntry(everyoneTrustee,
        baseRights, AccessControlEntryType.Revoke));

      accessEntries.Add(new MessageQueueAccessControlEntry(adminTrustee,
        MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow));

      accessEntries.Add(new MessageQueueAccessControlEntry(new Trustee("ANONYMOUS LOGON") { TrusteeType = TrusteeType.Unknown },
        baseRights, AccessControlEntryType.Revoke));

#if DEBUG
      accessEntries.Add(new MessageQueueAccessControlEntry(everyoneTrustee,
        MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow));

      accessEntries.Add(new MessageQueueAccessControlEntry(new Trustee("ANONYMOUS LOGON") { TrusteeType = TrusteeType.Unknown },
        MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow));
#endif

      return accessEntries;
    }

    
  }
}
