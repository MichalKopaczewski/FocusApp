using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Media;
using System.Windows.Media;

namespace FocusApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private TimeSpan _timeLeft;
        private bool _isWorkPeriod;
        private string _settingsPath = "tff_settings.json";

        // Settings for Time Free Focus
        private int _tffWorkMin = 20;
        private int _tffWorkMax = 45;
        private int _tffRestPercent = 10;

        // Session tracking
        private DateTime _sessionStart;
        private TimeSpan _workDuration;
        private TimeSpan _restDuration;
        private bool _manualStop;
        private string _sessionLogPath = "sessions.json";

        public MainWindow()
        {
            InitializeComponent();
            TimerModeComboBox.SelectionChanged += TimerModeComboBox_SelectionChanged;
            StartButton.Click += StartButton_Click;
            StopButton.Click += StopButton_Click;
            LoadTFFSettings();
            UpdateTFFUI();
        }

        private void TimerModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimerModeComboBox.SelectedIndex == 0) // Pomodoro
            {
                PomodoroPanel.Visibility = Visibility.Visible;
                TimeFreeFocusPanel.Visibility = Visibility.Collapsed;
            }
            else // Time Free Focus
            {
                PomodoroPanel.Visibility = Visibility.Collapsed;
                TimeFreeFocusPanel.Visibility = Visibility.Visible;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (TimerModeComboBox.SelectedIndex == 0)
            {
                // Pomodoro
                if (!int.TryParse(PomodoroWorkTimeTextBox.Text, out var workMin) || workMin <= 0 ||
                    !int.TryParse(PomodoroRestTimeTextBox.Text, out var restMin) || restMin <= 0)
                {
                    MessageBox.Show("Please enter valid work and rest times.");
                    return;
                }

                _isWorkPeriod = true;
                _timeLeft = TimeSpan.FromMinutes(workMin);
                StartTimer();
            }
            else
            {
                // Time Free Focus
                if (!int.TryParse(TFFWorkMinTextBox.Text, out var minWork) || minWork < 1 ||
                    !int.TryParse(TFFWorkMaxTextBox.Text, out var maxWork) || maxWork < minWork ||
                    !int.TryParse(TFFRestPercentTextBox.Text, out var restPercent) || restPercent < 5 ||
                    restPercent > 20)
                {
                    MessageBox.Show(
                        "Please enter valid values for Time Free Focus. Rest percent must be 5-20% and work min/max must be valid.");
                    return;
                }

                _tffWorkMin = minWork;
                _tffWorkMax = maxWork;
                _tffRestPercent = restPercent;
                SaveTFFSettings();
                var workDuration = new Random().Next(_tffWorkMin, _tffWorkMax + 1);
                _isWorkPeriod = true;
                _timeLeft = TimeSpan.FromMinutes(workDuration);
                StartTimer();
            }

            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            TimerDisplay.Text = "00:00";
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
        }

        private void SetPeriodLabel()
        {
            PeriodLabel.Text = _isWorkPeriod ? "Work" : "Rest";
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += Timer_Tick;
            }
            if (_isWorkPeriod)
            {
                _sessionStart = DateTime.Now;
                _workDuration = _timeLeft;
                _restDuration = TimeSpan.Zero;
                _manualStop = false;
            }
            SetPeriodLabel();
            _timer.Start();
            UpdateTimerDisplay();
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
            }
            if (_isWorkPeriod || _restDuration > TimeSpan.Zero)
            {
                _manualStop = true;
                SaveSessionLog();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timeLeft = _timeLeft.Add(TimeSpan.FromSeconds(-1));
            UpdateTimerDisplay();
            if (_timeLeft.TotalSeconds <= 0)
            {
                _timer.Stop();
                PlayFinishSound();
                BringWindowToFront();
                if (TimerModeComboBox.SelectedIndex == 0)
                {
                    // Pomodoro: switch between work/rest
                    if (_isWorkPeriod)
                    {
                        if (int.TryParse(PomodoroRestTimeTextBox.Text, out var restMin))
                        {
                            _isWorkPeriod = false;
                            _restDuration = TimeSpan.FromMinutes(restMin);
                            _timeLeft = _restDuration;
                            SetPeriodLabel();
                            _timer.Start();
                        }
                    }
                    else
                    {
                        SaveSessionLog();
                        MessageBox.Show("Pomodoro session complete!");
                        StartButton.Visibility = Visibility.Visible;
                        StopButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // Time Free Focus: switch between work/rest
                    if (_isWorkPeriod)
                    {
                        var restDuration = (int)Math.Round(_workDuration.TotalMinutes * _tffRestPercent / 100.0);
                        restDuration = Math.Max(1, restDuration);
                        _isWorkPeriod = false;
                        _restDuration = TimeSpan.FromMinutes(restDuration);
                        _timeLeft = _restDuration;
                        SetPeriodLabel();
                        _timer.Start();
                    }
                    else
                    {
                        SaveSessionLog();
                        MessageBox.Show("Focus session complete!");
                        StartButton.Visibility = Visibility.Visible;
                        StopButton.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateTimerDisplay()
        {
            TimerDisplay.Text = _timeLeft.ToString(@"mm\:ss");
            SetPeriodLabel();
        }

        private void PlayFinishSound()
        {
            string[] musicFiles = { "finish.mp3", "finish.wav" };
            string musicPath = null;
            foreach (var file in musicFiles)
            {
                if (File.Exists(file))
                {
                    musicPath = file;
                    break;
                }
            }

            if (musicPath != null)
            {
                try
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(Path.GetFullPath(musicPath)));
                    player.Volume = 1.0;
                    player.Play();
                }
                catch
                {
                    SystemSounds.Beep.Play();
                }
            }
            else
            {
                SystemSounds.Beep.Play();
            }
        }

        private void BringWindowToFront()
        {
            Topmost = true;
            Activate();
            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            resetTimer.Tick += (s, e) =>
            {
                Topmost = false;
                resetTimer.Stop();
            };
            resetTimer.Start();
        }

        private void SaveTFFSettings()
        {
            var settings = new { WorkMin = _tffWorkMin, WorkMax = _tffWorkMax, RestPercent = _tffRestPercent };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings));
        }

        private void LoadTFFSettings()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<TFFSettings>(json);
                    if (settings != null)
                    {
                        _tffWorkMin = settings.WorkMin;
                        _tffWorkMax = settings.WorkMax;
                        _tffRestPercent = settings.RestPercent;
                    }
                }
                catch
                {
                }
            }
        }

        private void UpdateTFFUI()
        {
            TFFWorkMinTextBox.Text = _tffWorkMin.ToString();
            TFFWorkMaxTextBox.Text = _tffWorkMax.ToString();
            TFFRestPercentTextBox.Text = _tffRestPercent.ToString();
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
                Mode = TimerModeComboBox.SelectedIndex == 0 ? "Pomodoro" : "Time Free Focus"
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
                File.WriteAllText(_sessionLogPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private class TFFSettings
        {
            public int WorkMin { get; set; }
            public int WorkMax { get; set; }
            public int RestPercent { get; set; }
        }

        private class SessionLog
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public TimeSpan WorkDuration { get; set; }
            public TimeSpan RestDuration { get; set; }
            public bool StoppedManually { get; set; }
            public string Mode { get; set; }
        }
    }
}