using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Contracts.Models;
using NLog;

namespace Client
{
  public class TaskOps : IEnumerable<ClientRequestDTO>
  {
    static Logger _Logger = LogManager.GetCurrentClassLogger();
    static TaskOps _Instance = null;
    static object _LOCK = new object();

    public static TaskOps Instance
    {
      get
      {
        _Logger.Trace("Enter");

        lock (_LOCK)
        {
          if (_Instance == null)
          {
            _Logger.Trace("Instance null");
            try
            {
              _Instance = new TaskOps();
            }
            catch (Exception e)
            {
              _Logger.ErrorException("What the duck?", e);
            }
          }

        }
        return _Instance;
      }
    }

    ConcurrentBag<ClientRequestDTO> _Requests = new ConcurrentBag<ClientRequestDTO>();

    Timer _Timer = null;

    public event EventHandler RequestsUpdated;
    public bool ProcessInstantly { get; set; }

    TaskOps()
    {
      _Logger.Trace("Enter");

      Random r = new Random((int)DateTime.Now.Ticks);
      int seconds = r.Next(1, 10);
      _Logger.Debug("Setting timer with interval of {0} second(s).", seconds);
      _Timer = new Timer(seconds * 1000);
      _Timer.AutoReset = false;
      _Timer.Elapsed += Timer_Elapsed;

      this.ProcessInstantly = false;

      _Logger.Trace("Exit");
    }

    void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      _Logger.Trace("Enter");


      ClientRequestDTO request = null;

      _Logger.Debug("There are {0} request(s) to be processed.", this._Requests.Count);

      if (this._Requests.TryTake(out request))
        this.ProcessRequest(request);

      if (this.RequestsUpdated != null)
        this.RequestsUpdated(this, EventArgs.Empty);

      if (!this._Requests.IsEmpty)
      {
        Random r = new Random((int)DateTime.Now.Ticks);
        Timer timer = sender as Timer;
        timer.AutoReset = false;
        int seconds = r.Next(1, 10);
        _Logger.Debug("Timer finished. I will see you in {0} seconds!", seconds);


        timer.Interval = seconds * 1000;
        timer.Start();
      }
      else
      {
        _Logger.Debug("Nothing more to do. I will go to sleep.");
        Timer timer = sender as Timer;
        timer.Stop();
      }
    }

    public void ProcessRequest(ClientRequestDTO requestDTO)
    {
      _Logger.Trace("Enter");

      string logMessage = string.Format("Client is finished processing. Sending a response back to the distributor with bookmark: {0} and workflow instance id: {1}",
        requestDTO.BookmarkId, requestDTO.WorkflowInstanceId);
      _Logger.Info(logMessage);

      new DistributorProxy().Invoke(new DistributorRequestDTO()
      {
        BookmarkId = requestDTO.BookmarkId,
        WorkflowInstanceId = requestDTO.WorkflowInstanceId
      });

      new DistributorProxy().RegisterAuditEvent(new AuditEventDTO()
      {
        CorrelationId = Guid.Parse(requestDTO.BookmarkId),
        Message = logMessage,
        LogLevel = LogLevel.Info.Name
      });
    }

    public void AddRequest(ClientRequestDTO requestDTO)
    {
      _Logger.Trace("Enter");

      if (!this.ProcessInstantly)
      {
        this._Requests.Add(requestDTO);

        if (this.RequestsUpdated != null)
          this.RequestsUpdated(this, EventArgs.Empty); 
      }
      else
      {
        this.ProcessRequest(requestDTO);
      }
    }

    public void StartTimer()
    {
      if (!this._Timer.Enabled)
      {
        _Logger.Info("Starting timer. Will execute in {0} second(s).", this._Timer.Interval / 1000);
        this._Timer.Start();
      }
    }

    #region IEnumerable<ClientRequestDTO> Members

    public IEnumerator<ClientRequestDTO> GetEnumerator()
    {
      return this._Requests.GetEnumerator();
    }

    #endregion

    #region IEnumerable Members

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return this._Requests.GetEnumerator();
    }

    #endregion
  }
}
