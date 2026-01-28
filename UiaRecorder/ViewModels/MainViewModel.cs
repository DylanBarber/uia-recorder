using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using UiaRecorder.Models;
using UiaRecorder.Services;

namespace UiaRecorder.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private readonly HttpClient _httpClient = new();
    private readonly AnalysisService _analysisService;
    private RecordingSession? _session;
    private bool _isRecording;
    private string _outputDirectory = string.Empty;
    private bool _allowPasswordCapture;
    private bool _autoAnalyze;
    private bool _isAnalyzing;
    private int _eventCount;
    private int _droppedCount;
    private string _elapsed = "00:00:00";
    private string _analysisStatus = string.Empty;
    private RecordingItem? _selectedRecording;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        StartCommand = new RelayCommand(StartRecording, () => !IsRecording);
        StopCommand = new RelayCommand(StopRecording, () => IsRecording);
        BrowseCommand = new RelayCommand(BrowseOutputDirectory, () => !IsRecording);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeLatestAsync, CanAnalyze);
        ConfigCommand = new RelayCommand(ShowConfigHelp);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();

        _analysisService = new AnalysisService(_httpClient);
        Recordings = new ObservableCollection<RecordingItem>();
        OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UiaRecorder");
        RefreshRecordings();
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand ConfigCommand { get; }

    public ObservableCollection<RecordingItem> Recordings { get; }

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
            RefreshRecordings();
            SelectLatestRecording();
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

    public bool AutoAnalyze
    {
        get => _autoAnalyze;
        set
        {
            if (_autoAnalyze == value)
            {
                return;
            }

            _autoAnalyze = value;
            OnPropertyChanged();
        }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (_isAnalyzing == value)
            {
                return;
            }

            _isAnalyzing = value;
            OnPropertyChanged();
            RefreshCommands();
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

    public string AnalysisStatus
    {
        get => _analysisStatus;
        private set
        {
            if (_analysisStatus == value)
            {
                return;
            }

            _analysisStatus = value;
            OnPropertyChanged();
        }
    }

    public RecordingItem? SelectedRecording
    {
        get => _selectedRecording;
        set
        {
            if (_selectedRecording == value)
            {
                return;
            }

            _selectedRecording = value;
            OnPropertyChanged();
            RefreshCommands();
        }
    }

    public void Shutdown()
    {
        if (IsRecording)
        {
            StopRecording();
        }

        _httpClient.Dispose();
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

        RefreshRecordings();
        SelectLatestRecording();
        if (AutoAnalyze)
        {
            _ = AnalyzeLatestAsync();
        }
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
            RefreshRecordings();
            SelectLatestRecording();
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

    private async Task AnalyzeLatestAsync()
    {
        if (!CanAnalyze())
        {
            AnalysisStatus = "Analysis unavailable. Configure OpenAI settings first.";
            return;
        }

        RecordingItem? latest = Recordings.OrderByDescending(item => item.TimestampUtc).FirstOrDefault();
        if (latest == null)
        {
            AnalysisStatus = "No recordings found to analyze.";
            return;
        }

        SelectedRecording = latest;
        if (!File.Exists(latest.FilePath))
        {
            AnalysisStatus = "Selected recording file not found.";
            return;
        }

        try
        {
            IsAnalyzing = true;
            AnalysisStatus = "Analyzing latest session...";
            string sessionJson = await File.ReadAllTextAsync(latest.FilePath);
            AppConfiguration.Load();
            OpenAiSettings settings = AppConfiguration.OpenAi;
            AnalysisResult result = await _analysisService.AnalyzeAsync(settings, sessionJson, CancellationToken.None);

            string baseName = Path.GetFileNameWithoutExtension(latest.FilePath);
            string summaryPath = Path.Combine(OutputDirectory, $"{baseName}-analysis-summary.txt");
            string jsonPath = Path.Combine(OutputDirectory, $"{baseName}-analysis.json");

            await File.WriteAllTextAsync(summaryPath, result.HumanSummaryText);
            await File.WriteAllTextAsync(jsonPath, result.RawJson);

            AnalysisStatus = "Analysis completed.";
        }
        catch (Exception ex)
        {
            AnalysisStatus = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private bool CanAnalyze()
    {
        return !IsRecording && !IsAnalyzing && Recordings.Count > 0 && AppConfiguration.OpenAi.IsValid;
    }

    private void RefreshRecordings()
    {
        if (Recordings == null)
        {
            return;
        }

        Recordings.Clear();
        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            return;
        }

        IEnumerable<string> files = Directory
            .EnumerateFiles(OutputDirectory, "session-*.json")
            .Where(path => !path.EndsWith("-analysis.json", StringComparison.OrdinalIgnoreCase));

        foreach (string file in files.OrderByDescending(File.GetLastWriteTimeUtc))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string sessionId = fileName.Replace("session-", string.Empty, StringComparison.OrdinalIgnoreCase);
            DateTime timestampUtc = ParseSessionId(sessionId);
            string displayName = timestampUtc == DateTime.MinValue
                ? sessionId
                : timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

            Recordings.Add(new RecordingItem
            {
                SessionId = sessionId,
                FilePath = file,
                TimestampUtc = timestampUtc == DateTime.MinValue ? File.GetLastWriteTimeUtc(file) : timestampUtc,
                DisplayName = displayName
            });
        }
    }

    private void SelectLatestRecording()
    {
        SelectedRecording = Recordings.OrderByDescending(item => item.TimestampUtc).FirstOrDefault();
    }

    private static DateTime ParseSessionId(string sessionId)
    {
        if (DateTime.TryParseExact(sessionId, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return DateTime.MinValue;
    }

    private void ShowConfigHelp()
    {
        string basePath = AppContext.BaseDirectory;
        string message = $"Configure OpenAI settings in appsettings.json located at:\n{basePath}\n\nRequired keys:\nOpenAI:ApiKey\nOpenAI:Model";
        System.Windows.MessageBox.Show(message, "OpenAI Configuration", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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

        if (AnalyzeCommand is AsyncRelayCommand analyze)
        {
            analyze.RaiseCanExecuteChanged();
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

internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
