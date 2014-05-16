using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Security.Principal;
using System.ServiceModel;
using System.Threading;
using Contracts.Models;
using NLog;

namespace Client
{
  class Program
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();

    static AutoResetEvent _ARE = new AutoResetEvent(false);

    static void Main(string[] args)
    {
      _Logger.Info("Client Startup...");

      Program p = new Program();
      p.StartListening();

      _ARE.WaitOne();
      p.Shutdown();
      _Logger.Info("Stopping Client...");
    }

    private ServiceHost _RemoteJobsSvcHost = null;
    ClientRequestDTO[] _ReceievedBookmarks = null;
    bool _IsShutdown = false;

    Program()
    {

    }

    public void StartListening()
    {
      this.StartRemoteJobsSvcHost();
    }

    public void Shutdown()
    {
      _Logger.Debug("Shutdown has been called.");
      _ARE.Set();
    }

    void StartRemoteJobsSvcHost()
    {
      TaskOps.Instance.RequestsUpdated += Instance_RequestsUpdated;
      this._RemoteJobsSvcHost = new ServiceHost(ClientServiceHandler.Instance);
      ClientServiceHandler.Instance.OnShutdown += Program_OnShutdown;

      foreach (var endpoint in this._RemoteJobsSvcHost.Description.Endpoints)
      {
        this.InitializeMSMQ(endpoint.Address.Uri);
      }
      string jobsSvcHostAddresses =
        string.Join(", ", this._RemoteJobsSvcHost.Description.Endpoints.Select(a => a.Address.ToString()).ToArray());
      _Logger.Debug("Preparing to listen for install commands at address: {0}", jobsSvcHostAddresses);

      this._RemoteJobsSvcHost.Open();
      _Logger.Debug("Listening for install commands at address: {0}", jobsSvcHostAddresses);


      while (!_IsShutdown)
      {
        string readString = null;
        try
        {
          readString = Reader.ReadLine(1000);
        }
        catch (TimeoutException)
        {
          continue;
        }
        

        if (_IsShutdown) continue;

        if (string.IsNullOrWhiteSpace(readString))
        {
          readString = "h";
        }
        else
          _Logger.Debug("{0} command to be executed.", readString);

        switch (readString.ToLowerInvariant().FirstOrDefault())
        {
          case 'l':
            if (this._ReceievedBookmarks == null) continue;
            for (int i = 1; i <= this._ReceievedBookmarks.Length; i++)
            {
              Console.WriteLine("{0}\tBookmark: {1}", i, this._ReceievedBookmarks[i - 1].BookmarkId);
            }
            break;

          case 's':
            string secondChar = readString.Length > 1 ? readString[1].ToString() : "1";
            var dto = this._ReceievedBookmarks[int.Parse(secondChar) - 1];
            TaskOps.Instance.ProcessRequest(dto);
            break;

          case 't':
            TaskOps.Instance.StartTimer();
            break;

          case 'a':
            this.RunAll();
            break;

          case 'i':
            _Logger.Info("Setting the task Ops to process requests instantly.");
            TaskOps.Instance.ProcessInstantly = true;
            break;

          case 'h':
            _Logger.Info("{0}l - list received requests with bookmark id.{0}s<number> - send the response back for the given request.{0}t - Start timer to process requests.{0}a - Process all received requests now.{0}i - Process requests instantly; process the requests as they are received.",
              Environment.NewLine);
            break;

          default:
            _Logger.Info("{0} isn't a command.", readString);
            goto case 'h';
        }
      }
    }

    void RunAll()
    {
      foreach (var dto in this._ReceievedBookmarks)
        TaskOps.Instance.ProcessRequest(dto);
    }

    void Instance_RequestsUpdated(object sender, EventArgs e)
    {
      this._ReceievedBookmarks = TaskOps.Instance.ToArray();
    }

    void Program_OnShutdown(object sender, EventArgs e)
    {
      _Logger.Trace("Enter");
      _Logger.Info("Shutdown event handling. Shutting down.");
      this._IsShutdown = true;
      _ARE.Set();
      _Logger.Trace("Exit");
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
