namespace CodexProfileTray;

internal sealed class CodexConfigReconciler : IDisposable
{
    private readonly CodexConfigManager _configManager;
    private readonly object _gate = new();
    private readonly System.Threading.Timer _timer;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public CodexConfigReconciler(CodexConfigManager configManager)
    {
        _configManager = configManager;
        _timer = new System.Threading.Timer(_ => Reconcile(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        Directory.CreateDirectory(_configManager.CodexHome);
        _watcher = new FileSystemWatcher(_configManager.CodexHome, Path.GetFileName(_configManager.ConfigPath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
        _watcher.Renamed += (_, _) => Schedule();
        Schedule();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _watcher?.Dispose();
        _timer.Dispose();
    }

    private void Schedule()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _timer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
        }
    }

    private void Reconcile()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }
            }

            try
            {
                _configManager.ReconcileActiveModelProvider();
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
