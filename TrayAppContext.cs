namespace CodexProfileTray;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly ProviderSettingsStore _providerSettingsStore = new();
    private readonly CodexConfigManager _configManager;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly NotifyIcon _notifyIcon;
    private readonly CodexLauncher _launcher;
    private readonly ProviderProxyServer _proxyServer;
    private readonly Icon _appIcon;
    private string? _proxyStartError;

    public TrayAppContext()
    {
        _configManager = new CodexConfigManager(_providerSettingsStore);
        _launcher = new CodexLauncher(_configManager);
        _proxyServer = new ProviderProxyServer(_providerSettingsStore);
        try
        {
            _proxyServer.Start();
        }
        catch (Exception ex)
        {
            _proxyStartError = ex.Message;
        }

        _appIcon = AppIcons.GetAppIcon();
        _notifyIcon = new NotifyIcon
        {
            Text = "Codex Profile Tray",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => LaunchCodex();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _proxyServer.Dispose();
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openCodex = new ToolStripMenuItem("Open Codex", null, (_, _) => LaunchCodex());
        var manageProfiles = new ToolStripMenuItem("Manage Providers...", null, (_, _) => ShowProfileManager());
        var chooseFolder = new ToolStripMenuItem("Choose Project Folder...", null, (_, _) => ChooseWorkspace());
        var openConfig = new ToolStripMenuItem("Open Codex Config", null, (_, _) => OpenConfig());
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        menu.Items.Add(openCodex);
        menu.Items.Add(manageProfiles);
        menu.Items.Add(chooseFolder);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void ShowProfileManager()
    {
        using var form = new ProfileManagerForm(_configManager);
        form.ShowDialog();
    }

    private void ChooseWorkspace()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a project folder to open in Codex"
        };

        if (!string.IsNullOrWhiteSpace(_settings.LastWorkspace) && Directory.Exists(_settings.LastWorkspace))
        {
            dialog.SelectedPath = _settings.LastWorkspace;
        }
        else
        {
            var github = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GitHub");
            dialog.SelectedPath = Directory.Exists(github)
                ? github
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _settings.LastWorkspace = dialog.SelectedPath;
            _settings.Save();
        }
    }

    private void LaunchCodex()
    {
        try
        {
            EnsureWorkspaceSelected();
            if (string.IsNullOrWhiteSpace(_settings.LastWorkspace))
            {
                return;
            }

            if (_configManager.LoadProfiles().Any(profile => ProviderProxyServer.ShouldProxyProvider(profile.ProviderId)) &&
                !_proxyServer.IsRunning)
            {
                throw new InvalidOperationException($"The local compatibility proxy is not running. {_proxyStartError ?? "Exit and reopen Codex Profile Tray, then try again."}");
            }

            _launcher.Launch(_settings.LastWorkspace);
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Codex Profile Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsureWorkspaceSelected()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastWorkspace) && Directory.Exists(_settings.LastWorkspace))
        {
            return;
        }

        ChooseWorkspace();
    }

    private void OpenConfig()
    {
        try
        {
            if (!File.Exists(_configManager.ConfigPath))
            {
                MessageBox.Show($"Codex config does not exist yet: {_configManager.ConfigPath}", "Codex Profile Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _configManager.ConfigPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Codex Profile Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
