using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using UiaRecorder.Services;

namespace UiaRecorder.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private RecordingSession? _session;
    private bool _isRecording;
    private string _outputDirectory = string.Empty;
    private bool _allowPasswordCapture;
    private int _eventCount;
    private int _droppedCount;
    private string _elapsed = "00:00:00";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        StartCommand = new RelayCommand(StartRecording, () => !IsRecording);
        StopCommand = new RelayCommand(StopRecording, () => IsRecording);
        BrowseCommand = new RelayCommand(BrowseOutputDirectory, () => !IsRecording);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();

        OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UiaRecorder");
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BrowseCommand { get; }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (_isRecording == value)
            {
                return;
            }

            _isRecording = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (_outputDirectory == value)
            {
                return;
            }

            _outputDirectory = value;
            OnPropertyChanged();
        }
    }

    public bool AllowPasswordCapture
    {
        get => _allowPasswordCapture;
        set
        {
            if (_allowPasswordCapture == value)
            {
                return;
            }

            _allowPasswordCapture = value;
            OnPropertyChanged();
            if (_session != null)
            {
                _session.AllowPasswordCapture = value;
            }
        }
    }

    public int EventCount
    {
        get => _eventCount;
        private set
        {
            if (_eventCount == value)
            {
                return;
            }

            _eventCount = value;
            OnPropertyChanged();
        }
    }

    public int DroppedCount
    {
        get => _droppedCount;
        private set
        {
            if (_droppedCount == value)
            {
                return;
            }

            _droppedCount = value;
            OnPropertyChanged();
        }
    }

    public string Elapsed
    {
        get => _elapsed;
        private set
        {
            if (_elapsed == value)
            {
                return;
            }

            _elapsed = value;
            OnPropertyChanged();
        }
    }

    public void Shutdown()
    {
        if (IsRecording)
        {
            StopRecording();
        }
    }

    private void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UiaRecorder");
        }

        Directory.CreateDirectory(OutputDirectory);
        EventCount = 0;
        DroppedCount = 0;

        _session = new RecordingSession
        {
            OutputDirectory = OutputDirectory,
            AllowPasswordCapture = AllowPasswordCapture
        };
        _session.EventRecorded += (_, _) => IncrementEventCount();
        _session.DroppedEvent += (_, _) => IncrementDroppedCount();
        _session.Start();
        _sessionStartUtc = DateTime.UtcNow;

        IsRecording = true;
        Elapsed = "00:00:00";
        _timer.Start();
    }

    private void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        TimeSpan elapsed = DateTime.UtcNow - _sessionStartUtc;
        Elapsed = elapsed.ToString("hh\\:mm\\:ss");
        _timer.Stop();
        _session?.Stop();
        _session?.Dispose();
        _session = null;
        IsRecording = false;
    }

    private void BrowseOutputDirectory()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select output directory",
            UseDescriptionForTitle = true,
            SelectedPath = OutputDirectory
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            OutputDirectory = dialog.SelectedPath;
        }
    }

    private void UpdateElapsed()
    {
        if (!IsRecording || _session == null)
        {
            return;
        }

        TimeSpan elapsed = DateTime.UtcNow - _sessionStartUtc;
        Elapsed = elapsed.ToString("hh\\:mm\\:ss");
    }

    private void IncrementEventCount()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => { EventCount++; });
    }

    private void IncrementDroppedCount()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => { DroppedCount++; });
    }

    private DateTime _sessionStartUtc;

    private void RefreshCommands()
    {
        if (StartCommand is RelayCommand start)
        {
            start.RaiseCanExecuteChanged();
        }

        if (StopCommand is RelayCommand stop)
        {
            stop.RaiseCanExecuteChanged();
        }

        if (BrowseCommand is RelayCommand browse)
        {
            browse.RaiseCanExecuteChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
