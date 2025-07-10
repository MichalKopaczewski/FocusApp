using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Media;
using System.Windows.Media;
using System.Collections.ObjectModel;
using FocusApp;

namespace FocusApp
{
    public class UserSettings
    {
        public int LastTimerMode { get; set; } = 0;
    }

    public partial class MainWindow : Window
    {
        private FocusSessionManager _sessionManager = new();
        private string _settingsPath = "tff_settings.json";
        private string _userSettingsPath = "user_settings.json";
        private UserSettings _userSettings = new();
        private TimerStatus _lastStatus = TimerStatus.None;

        public MainWindow()
        {
            InitializeComponent();
            LoadUserSettings();
            TimerModeComboBox.SelectedIndex = _userSettings.LastTimerMode;
            _sessionManager.SetDispatcher(Dispatcher.CurrentDispatcher);
            TimerModeComboBox.SelectionChanged += TimerModeComboBox_SelectionChanged;
            StartButton.Click += StartButton_Click;
            StopButton.Click += StopButton_Click;
            AddDistractionButton.Click += AddDistractionButton_Click;
            _sessionManager.StatusChanged += OnStatusChanged;
            _sessionManager.SessionCompleted += OnSessionCompleted;
            _sessionManager.TimerTick += OnTimerTick;
            DistractionsListBox.ItemsSource = _sessionManager.Distractions;
            LoadTFFSettings();
            UpdateTFFUI();
            SetPeriodLabel(_sessionManager.Status);
            TimerDisplay.Text = "00:00";
        }

        private void TimerModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PomodoroPanel.Visibility = TimerModeComboBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            TimeFreeFocusPanel.Visibility =
                TimerModeComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            _userSettings.LastTimerMode = TimerModeComboBox.SelectedIndex;
            SaveUserSettings();
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

                _sessionManager.StartWork(workMin);
            }
            else
            {
                // Time Free Focus
                if (!int.TryParse(TFFWorkMinTextBox.Text, out var minWork) || minWork < 1 ||
                    !int.TryParse(TFFWorkMaxTextBox.Text, out var maxWork) || maxWork < minWork ||
                    !int.TryParse(TFFRestMinTextBox.Text, out var minRest) || minRest < 8 ||
                    !int.TryParse(TFFRestMaxTextBox.Text, out var maxRest) || maxRest < minRest)
                {
                    MessageBox.Show(
                        "Please enter valid values for Time Free Focus. Work min/max must be valid and rest min must be at least 8.");
                    return;
                }

                _sessionManager.ConfigureTFF(minWork, maxWork, minRest, maxRest);
                _sessionManager.StartRandomTFFWork();
            }

            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _sessionManager.Stop();
            TimerDisplay.Text = "00:00";
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
        }

        private void AddDistractionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(DoingTextBox.Text) ||
                !string.IsNullOrWhiteSpace(DistractedTextBox.Text) ||
                !string.IsNullOrWhiteSpace(FeltTextBox.Text) ||
                !string.IsNullOrWhiteSpace(DifferentlyTextBox.Text))
            {
                _sessionManager.AddDistraction(new Distraction
                {
                    WhatIDid = DoingTextBox.Text,
                    WhatDistractedMe = DistractedTextBox.Text,
                    HowIFelt = FeltTextBox.Text,
                    WhatCouldIDoDifferently = DifferentlyTextBox.Text
                });
                DoingTextBox.Text = "";
                DistractedTextBox.Text = "";
                FeltTextBox.Text = "";
                DifferentlyTextBox.Text = "";
            }
        }

        private void OnStatusChanged(TimerStatus status, TimeSpan timeLeft)
        {
            // Play sound and bring window to front when transitioning from Work to Rest or from Work/Rest to None
            if ((_lastStatus == TimerStatus.Work && status == TimerStatus.Rest) ||
                ((_lastStatus == TimerStatus.Work || _lastStatus == TimerStatus.Rest) && status == TimerStatus.None))
            {
                PlayFinishSound();
                BringWindowToFront();
            }
            _lastStatus = status;
            SetPeriodLabel(status);
            TimerDisplay.Text = timeLeft.ToString(@"mm\:ss");
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

        private void OnSessionCompleted()
        {
            MessageBox.Show(TimerModeComboBox.SelectedIndex == 0
                ? "Pomodoro session complete!"
                : "Focus session complete!");
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
        }

        private void OnTimerTick()
        {
            TimerDisplay.Text = _sessionManager.TimeLeft.ToString(@"mm\:ss");
        }

        private void SetPeriodLabel(TimerStatus status)
        {
            // Hide timer in TFF work period
            if (TimerModeComboBox.SelectedIndex == 1 && status == TimerStatus.Work)
            {
                TimerDisplay.Visibility = Visibility.Collapsed;
            }
            else
            {
                TimerDisplay.Visibility = Visibility.Visible;
            }
            switch (status)
            {
                case TimerStatus.Work:
                    PeriodLabel.Text = "Work";
                    DistractionNotesPanel.Visibility = Visibility.Visible;
                    break;
                case TimerStatus.Rest:
                    PeriodLabel.Text = "Rest";
                    DistractionNotesPanel.Visibility = Visibility.Collapsed;
                    break;
                default:
                    PeriodLabel.Text = "None";
                    DistractionNotesPanel.Visibility = Visibility.Collapsed;
                    break;
            }
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
                        TFFWorkMinTextBox.Text = settings.WorkMin.ToString();
                        TFFWorkMaxTextBox.Text = settings.WorkMax.ToString();
                        // Optionally set TFFRestMinTextBox and TFFRestMaxTextBox if you want to persist them
                    }
                }
                catch
                {
                }
            }
        }

        private void UpdateTFFUI()
        {
            // Optionally update UI fields if needed
        }

        private void LoadUserSettings()
        {
            if (File.Exists(_userSettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_userSettingsPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null)
                        _userSettings = settings;
                }
                catch { }
            }
        }

        private void SaveUserSettings()
        {
            try
            {
                File.WriteAllText(_userSettingsPath, JsonSerializer.Serialize(_userSettings));
            }
            catch { }
        }
    }
}