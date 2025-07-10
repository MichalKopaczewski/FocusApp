using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Timers;
using System.Windows.Threading;

namespace FocusApp
{
    public enum TimerStatus { None, Work, Rest }

    public class FocusSessionManager
    {
        public TimerStatus Status { get; private set; } = TimerStatus.None;
        public TimeSpan TimeLeft { get; private set; }
        public ObservableCollection<Distraction> Distractions { get; } = new();
        public event Action<TimerStatus, TimeSpan> StatusChanged;
        public event Action SessionCompleted;
        public event Action TimerTick;

        private System.Timers.Timer _timer;
        private Dispatcher _dispatcher;
        private bool _isWorkPeriod;
        private int _tffWorkMin = 20;
        private int _tffWorkMax = 45;
        private int _tffRestMin = 2;
        private int _tffRestMax = 10;
        private DateTime _sessionStart;
        private TimeSpan _workDuration;
        private TimeSpan _restDuration;
        private bool _manualStop;
        private string _sessionLogPath = "sessions.json";
        private string _settingsPath = "tff_settings.json";
        private DateTime _targetEndTime;

        public FocusSessionManager() { }
        public FocusSessionManager(Dispatcher dispatcher) { _dispatcher = dispatcher; }
        public void SetDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

        public void ConfigureTFF(int minWork, int maxWork, int minRest, int maxRest)
        {
            _tffWorkMin = minWork;
            _tffWorkMax = maxWork;
            _tffRestMin = minRest;
            _tffRestMax = maxRest;
            SaveTFFSettings();
        }

        public void StartWork(int minutes)
        {
            _isWorkPeriod = true;
            TimeLeft = TimeSpan.FromMinutes(minutes);
            StartTimer(TimeLeft);
        }

        public void StartRandomTFFWork()
        {
            var workDuration = new Random().Next(_tffWorkMin, _tffWorkMax + 1);
            StartWork(workDuration);
        }

        public void StartRest(int minutes)
        {
            _isWorkPeriod = false;
            TimeLeft = TimeSpan.FromMinutes(minutes);
            StartTimer(TimeLeft);
        }

        public void AddDistraction(Distraction d)
        {
            Distractions.Add(d);
        }

        public void Stop()
        {
            _timer?.Stop();
            Status = TimerStatus.None;
            _manualStop = true;
            SaveSessionLog();
            InvokeOnUI(() => StatusChanged?.Invoke(Status, TimeLeft));
            InvokeOnUI(() => SessionCompleted?.Invoke());
        }

        private void StartTimer(TimeSpan duration)
        {
            _timer?.Stop();
            if (_timer == null)
            {
                _timer = new System.Timers.Timer(200); // 200ms for better accuracy
                _timer.Elapsed += Timer_Tick;
                _timer.AutoReset = true;
            }
            _targetEndTime = DateTime.Now.Add(duration);
            if (_isWorkPeriod)
            {
                Status = TimerStatus.Work;
                _sessionStart = DateTime.Now;
                _workDuration = duration;
                _restDuration = TimeSpan.Zero;
                _manualStop = false;
                Distractions.Clear();
            }
            else
            {
                Status = TimerStatus.Rest;
            }
            InvokeOnUI(() => StatusChanged?.Invoke(Status, TimeLeft));
            _timer.Start();
            InvokeOnUI(() => TimerTick?.Invoke());
        }

        private void Timer_Tick(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            TimeLeft = _targetEndTime - now;
            if (TimeLeft < TimeSpan.Zero)
                TimeLeft = TimeSpan.Zero;
            InvokeOnUI(() => TimerTick?.Invoke());
            InvokeOnUI(() => StatusChanged?.Invoke(Status, TimeLeft));
            if (TimeLeft.TotalSeconds <= 0)
            {
                _timer.Stop();
                if (_isWorkPeriod)
                {
                    // Switch to rest
                    int restDuration = new Random().Next(_tffRestMin, _tffRestMax + 1);
                    _isWorkPeriod = false;
                    Status = TimerStatus.Rest;
                    _restDuration = TimeSpan.FromMinutes(restDuration);
                    StartTimer(_restDuration);
                }
                else
                {
                    Status = TimerStatus.None;
                    InvokeOnUI(() => StatusChanged?.Invoke(Status, TimeLeft));
                    SaveSessionLog();
                    InvokeOnUI(() => SessionCompleted?.Invoke());
                }
            }
        }

        private void InvokeOnUI(Action action)
        {
            if (_dispatcher != null)
            {
                if (_dispatcher.CheckAccess())
                    action();
                else
                    _dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private void SaveSessionLog()
        {
            var session = new SessionLog
            {
                StartTime = _sessionStart,
                EndTime = DateTime.Now,
                WorkDuration = _workDuration,
                RestDuration = _restDuration,
                StoppedManually = _manualStop,
                Mode = "Time Free Focus",
                Distractions = new System.Collections.Generic.List<Distraction>(Distractions)
            };
            try
            {
                var list = new System.Collections.Generic.List<SessionLog>();
                if (File.Exists(_sessionLogPath))
                {
                    var json = File.ReadAllText(_sessionLogPath);
                    var existing = JsonSerializer.Deserialize<System.Collections.Generic.List<SessionLog>>(json);
                    if (existing != null)
                        list = existing;
                }
                list.Add(session);
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                File.WriteAllText(_sessionLogPath, JsonSerializer.Serialize(list, options));
            }
            catch { }
        }

        private void SaveTFFSettings()
        {
            var settings = new { WorkMin = _tffWorkMin, WorkMax = _tffWorkMax, RestMin = _tffRestMin, RestMax = _tffRestMax };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings));
        }
    }
}
