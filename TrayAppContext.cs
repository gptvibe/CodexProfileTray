namespace CodexProfileTray;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly CodexConfigManager _configManager = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openMenu = new("Open Codex With Profile");
    private readonly CodexLauncher _launcher;

    public TrayAppContext()
    {
        _launcher = new CodexLauncher(_configManager);
        _notifyIcon = new NotifyIcon
        {
            Text = "Codex Profile Tray",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowProfileManager();
        RefreshOpenMenu();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => RefreshOpenMenu();

        var manageProfiles = new ToolStripMenuItem("Manage Profiles...", null, (_, _) => ShowProfileManager());
        var chooseFolder = new ToolStripMenuItem("Choose Project Folder...", null, (_, _) => ChooseWorkspace());
        var openConfig = new ToolStripMenuItem("Open Codex Config", null, (_, _) => OpenConfig());
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        menu.Items.Add(_openMenu);
        menu.Items.Add(manageProfiles);
        menu.Items.Add(chooseFolder);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void RefreshOpenMenu()
    {
        _openMenu.DropDownItems.Clear();

        IReadOnlyList<CodexProfile> profiles;
        try
        {
            profiles = _configManager.LoadProfiles();
        }
        catch (Exception ex)
        {
            _openMenu.DropDownItems.Add(new ToolStripMenuItem("Could not read profiles") { Enabled = false });
            _openMenu.DropDownItems.Add(new ToolStripMenuItem(ex.Message) { Enabled = false });
            return;
        }

        if (profiles.Count == 0)
        {
            _openMenu.DropDownItems.Add(new ToolStripMenuItem("No profiles found") { Enabled = false });
            return;
        }

        foreach (var profile in profiles)
        {
            var item = new ToolStripMenuItem(profile.ToString());
            item.Click += (_, _) => Launch(profile);
            _openMenu.DropDownItems.Add(item);
        }
    }

    private void ShowProfileManager()
    {
        using var form = new ProfileManagerForm(_configManager);
        form.ShowDialog();
        RefreshOpenMenu();
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

    private void Launch(CodexProfile profile)
    {
        try
        {
            EnsureWorkspaceSelected();
            if (string.IsNullOrWhiteSpace(_settings.LastWorkspace))
            {
                return;
            }

            _launcher.Launch(profile, _settings.LastWorkspace);
            _settings.LastProfile = profile.ProfileName;
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
