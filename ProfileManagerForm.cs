namespace CodexProfileTray;

internal sealed class ProfileManagerForm : Form
{
    private static readonly Color Page = Color.FromArgb(248, 250, 252);
    private static readonly Color Sidebar = Color.FromArgb(15, 23, 42);
    private static readonly Color SidebarMuted = Color.FromArgb(148, 163, 184);
    private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
    private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentSoft = Color.FromArgb(219, 234, 254);
    private static readonly Color Border = Color.FromArgb(203, 213, 225);

    private readonly CodexConfigManager _configManager;
    private readonly ListBox _profilesList = new();
    private readonly ComboBox _presetBox = new();
    private readonly TextBox _providerNameBox = new();
    private readonly TextBox _baseUrlBox = new();
    private readonly TextBox _apiKeyBox = new();
    private readonly ComboBox _modelBox = new();
    private readonly CheckBox _contextEnabledBox = new();
    private readonly NumericUpDown _contextWindowBox = new();
    private readonly ComboBox _reasoningEffortBox = new();
    private readonly ComboBox _reasoningSummariesBox = new();
    private readonly Label _secretStatusLabel = new();
    private readonly Label _statusLabel = new();

    private List<CodexProfile> _profiles = new();
    private string? _loadedProviderId;
    private string? _loadedEnvKey;
    private bool _isLoading;

    public ProfileManagerForm(CodexConfigManager configManager)
    {
        _configManager = configManager;
        Text = "Codex Profile Tray";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1160, 760);
        BackColor = Page;
        Font = new Font("Segoe UI", 9.5F);
        Icon = AppIcons.GetAppIcon();

        BuildUi();
        ReloadProfiles();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Page
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 18, 20),
            RowCount = 5,
            ColumnCount = 1,
            BackColor = Sidebar
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var title = new Label
        {
            Text = "Profiles",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };
        sidebar.Controls.Add(title, 0, 0);

        var hint = new Label
        {
            Text = "OpenAI-compatible configs",
            Dock = DockStyle.Fill,
            ForeColor = SidebarMuted,
            TextAlign = ContentAlignment.TopLeft
        };
        sidebar.Controls.Add(hint, 0, 1);

        _profilesList.Dock = DockStyle.Fill;
        _profilesList.BorderStyle = BorderStyle.None;
        _profilesList.BackColor = Sidebar;
        _profilesList.ForeColor = Color.White;
        _profilesList.ItemHeight = 64;
        _profilesList.IntegralHeight = false;
        _profilesList.DrawMode = DrawMode.OwnerDrawFixed;
        _profilesList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        _profilesList.DrawItem += DrawProfileItem;
        sidebar.Controls.Add(_profilesList, 0, 2);

        var newButton = CreateButton("New setup", accent: false);
        newButton.Click += (_, _) => NewProviderSetup();
        sidebar.Controls.Add(newButton, 0, 3);

        var deleteButton = CreateButton("Delete selected profile", accent: false);
        deleteButton.Click += (_, _) => DeleteSelectedProfile();
        sidebar.Controls.Add(deleteButton, 0, 4);

        return sidebar;
    }

    private Control BuildEditor()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 28, 34, 24),
            RowCount = 12,
            ColumnCount = 3,
            BackColor = Page
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var header = new Label
        {
            Text = "Set Up Provider",
            Dock = DockStyle.Fill,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        editor.Controls.Add(header, 0, 0);
        editor.SetColumnSpan(header, 3);

        StyleCombo(_presetBox);
        _presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _presetBox.Items.AddRange(ProviderPreset.All.Cast<object>().ToArray());
        _presetBox.SelectedIndexChanged += (_, _) => ApplySelectedPreset();
        AddLabeledControl(editor, "Provider type", _presetBox, 1, columnSpan: 2);

        StyleTextBox(_providerNameBox);
        _providerNameBox.PlaceholderText = "Provider display name";
        AddLabeledControl(editor, "Display name", _providerNameBox, 2, columnSpan: 2);

        StyleTextBox(_baseUrlBox);
        _baseUrlBox.PlaceholderText = "https://api.example.com/v1";
        AddLabeledControl(editor, "Base URL", _baseUrlBox, 3, columnSpan: 2);

        StyleTextBox(_apiKeyBox);
        _apiKeyBox.PlaceholderText = "Paste API key";
        _apiKeyBox.UseSystemPasswordChar = true;
        AddLabeledControl(editor, "API key", _apiKeyBox, 4);

        var saveKeyButton = CreateButton("Save key", accent: false);
        saveKeyButton.Click += (_, _) => SaveKeyOnly();
        editor.Controls.Add(saveKeyButton, 2, 4);

        StyleCombo(_modelBox);
        _modelBox.DropDownStyle = ComboBoxStyle.DropDown;
        _modelBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _modelBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        AddLabeledControl(editor, "Model", _modelBox, 5);

        var fetchButton = CreateButton("Fetch", accent: false);
        fetchButton.Click += async (_, _) => await FetchModelsAsync();
        editor.Controls.Add(fetchButton, 2, 5);

        var contextPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Page
        };
        _contextEnabledBox.Text = "Set a context window";
        _contextEnabledBox.AutoSize = true;
        _contextEnabledBox.ForeColor = TextPrimary;
        _contextEnabledBox.CheckedChanged += (_, _) => _contextWindowBox.Enabled = _contextEnabledBox.Checked;
        _contextWindowBox.Width = 190;
        _contextWindowBox.Minimum = 1;
        _contextWindowBox.Maximum = 100_000_000;
        _contextWindowBox.Increment = 1000;
        _contextWindowBox.ThousandsSeparator = true;
        _contextWindowBox.Enabled = false;
        contextPanel.Controls.Add(_contextEnabledBox);
        contextPanel.Controls.Add(_contextWindowBox);
        AddLabeledControl(editor, "Context", contextPanel, 6, columnSpan: 2);

        StyleCombo(_reasoningEffortBox);
        _reasoningEffortBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningEffortBox.Items.AddRange(new object[] { "Auto", "none", "minimal", "low", "medium", "high", "xhigh" });
        _reasoningEffortBox.SelectedIndex = 0;
        AddLabeledControl(editor, "Reasoning effort", _reasoningEffortBox, 7, columnSpan: 2);

        StyleCombo(_reasoningSummariesBox);
        _reasoningSummariesBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningSummariesBox.Items.AddRange(new object[] { "Auto", "Yes", "No" });
        _reasoningSummariesBox.SelectedIndex = 0;
        AddLabeledControl(editor, "Summaries", _reasoningSummariesBox, 8, columnSpan: 2);

        _secretStatusLabel.Dock = DockStyle.Fill;
        _secretStatusLabel.ForeColor = TextMuted;
        _secretStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        editor.Controls.Add(_secretStatusLabel, 1, 9);
        editor.SetColumnSpan(_secretStatusLabel, 2);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = TextMuted;
        editor.Controls.Add(_statusLabel, 1, 10);
        editor.SetColumnSpan(_statusLabel, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Page
        };
        var closeButton = CreateButton("Close", accent: false);
        closeButton.Click += (_, _) => Close();
        var saveButton = CreateButton("Save profile", accent: true);
        saveButton.Width = 150;
        saveButton.Click += (_, _) => SaveProfile();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(saveButton);
        editor.Controls.Add(footer, 0, 11);
        editor.SetColumnSpan(footer, 3);

        return editor;
    }

    private void DrawProfileItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = e.Bounds;
        using var background = new SolidBrush(selected ? Color.FromArgb(30, 41, 59) : Sidebar);
        e.Graphics.FillRectangle(background, bounds);

        if (_profilesList.Items[e.Index] is not CodexProfile profile)
        {
            return;
        }

        var titleRect = new Rectangle(bounds.Left + 12, bounds.Top + 11, bounds.Width - 24, 23);
        var subtitleRect = new Rectangle(bounds.Left + 12, bounds.Top + 36, bounds.Width - 24, 20);
        TextRenderer.DrawText(e.Graphics, profile.ProfileName, new Font(Font, FontStyle.Bold), titleRect, Color.White, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, profile.Model ?? profile.ProviderName ?? profile.ProviderId, Font, subtitleRect, SidebarMuted, TextFormatFlags.EndEllipsis);
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string labelText, Control control, int row, int columnSpan = 1)
    {
        var label = new Label
        {
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextMuted,
            Dock = DockStyle.Fill
        };
        panel.Controls.Add(label, 0, row);

        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, columnSpan);
    }

    private static Button CreateButton(string text, bool accent)
    {
        var button = new Button
        {
            Text = text,
            Width = 136,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Accent : Color.White,
            ForeColor = accent ? Color.White : TextPrimary,
            Cursor = Cursors.Hand,
            Margin = new Padding(5),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = accent ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.White;
        textBox.ForeColor = TextPrimary;
        textBox.Margin = new Padding(0, 6, 12, 6);
    }

    private static void StyleCombo(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.White;
        comboBox.ForeColor = TextPrimary;
        comboBox.Margin = new Padding(0, 6, 12, 6);
    }

    private void ReloadProfiles(string? selectProfileName = null)
    {
        _profiles = _configManager.LoadProfiles().ToList();
        _profilesList.Items.Clear();
        foreach (var profile in _profiles)
        {
            _profilesList.Items.Add(profile);
        }

        if (_profiles.Count == 0)
        {
            NewProviderSetup();
            return;
        }

        var selected = _profiles.FirstOrDefault(profile =>
            profile.ProfileName.Equals(selectProfileName, StringComparison.OrdinalIgnoreCase));
        _profilesList.SelectedItem = selected ?? _profiles[0];
    }

    private void NewProviderSetup()
    {
        _isLoading = true;
        _profilesList.ClearSelected();
        _loadedProviderId = null;
        _loadedEnvKey = null;
        _presetBox.SelectedIndex = 0;
        _isLoading = false;
        ApplySelectedPreset();
        _statusLabel.Text = "Choose a preset, paste an API key, then fetch models. Use Custom for providers not listed.";
    }

    private void ApplySelectedPreset()
    {
        if (_isLoading || _presetBox.SelectedItem is not ProviderPreset preset)
        {
            return;
        }

        _loadedProviderId = preset.IsCustom ? null : preset.ProviderId;
        _loadedEnvKey = preset.EnvKey;
        _providerNameBox.Text = preset.ProviderName;
        _baseUrlBox.Text = preset.BaseUrl;
        _apiKeyBox.Text = string.Empty;
        SetModelChoices(preset.Models);
        _baseUrlBox.ReadOnly = !preset.IsCustom;
        _baseUrlBox.BackColor = preset.IsCustom ? Color.White : Color.FromArgb(241, 245, 249);
        _contextEnabledBox.Checked = preset.ContextWindow.HasValue;
        _contextWindowBox.Enabled = preset.ContextWindow.HasValue;
        _contextWindowBox.Value = Math.Clamp(preset.ContextWindow ?? 1_000_000, 1, 100_000_000);
        _reasoningEffortBox.SelectedIndex = 0;
        _reasoningSummariesBox.SelectedIndex = 0;
        _secretStatusLabel.Text = preset.IsCustom
            ? "Paste a key when saving, or leave it blank if this provider does not need one."
            : WindowsCredentialStore.HasSecret(preset.ProviderId)
                ? "A key is already saved for this provider."
                : "Paste your API key once. It will be saved in Windows Credential Manager.";
        _statusLabel.Text = "Choose a model, choose reasoning effort, then save the profile.";
    }

    private void LoadSelectedProfile()
    {
        if (_profilesList.SelectedItem is not CodexProfile profile)
        {
            return;
        }

        _isLoading = true;
        var preset = ProviderPreset.All.FirstOrDefault(item =>
            item.ProviderId.Equals(profile.ProviderId, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(profile.BaseUrl) && item.BaseUrl.Equals(profile.BaseUrl, StringComparison.OrdinalIgnoreCase)))
            ?? ProviderPreset.All.First(item => item.IsCustom);
        _presetBox.SelectedItem = preset;
        _isLoading = false;

        _loadedProviderId = profile.ProviderId;
        _loadedEnvKey = profile.EnvKey;
        _providerNameBox.Text = profile.ProviderName ?? profile.ProviderId;
        _baseUrlBox.Text = profile.BaseUrl ?? string.Empty;
        _baseUrlBox.ReadOnly = !preset.IsCustom;
        _baseUrlBox.BackColor = preset.IsCustom ? Color.White : Color.FromArgb(241, 245, 249);
        _apiKeyBox.Text = string.Empty;

        var providerProfiles = _profiles
            .Where(item => item.ProviderId.Equals(profile.ProviderId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        SetModelChoices(providerProfiles
            .Select(item => item.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)!);
        _modelBox.Text = profile.Model ?? string.Empty;

        var context = profile.ContextWindow;
        _contextEnabledBox.Checked = context.HasValue;
        _contextWindowBox.Enabled = context.HasValue;
        _contextWindowBox.Value = Math.Clamp(context ?? 1_000_000, 1, 100_000_000);

        _reasoningEffortBox.SelectedItem = string.IsNullOrWhiteSpace(profile.ReasoningEffort)
            ? "Auto"
            : profile.ReasoningEffort;

        _reasoningSummariesBox.SelectedIndex = profile.SupportsReasoningSummaries switch
        {
            true => 1,
            false => 2,
            _ => 0
        };

        var hasSecret = !profile.IsBuiltInOpenAI && WindowsCredentialStore.HasSecret(profile.ProviderId);
        _secretStatusLabel.Text = profile.IsBuiltInOpenAI
            ? "This profile uses Codex's built-in OpenAI/ChatGPT login."
            : hasSecret
                ? "A key is saved in Windows Credential Manager for this provider."
                : "No key saved yet. Paste one and click Save key or Save profile.";
        _statusLabel.Text = "Reasoning effort is saved with this model profile.";
    }

    private void SaveKeyOnly()
    {
        try
        {
            var providerId = ResolveProviderId();
            var key = _apiKeyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Paste an API key first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowsCredentialStore.WriteSecret(providerId, key);
            _apiKeyBox.Text = string.Empty;
            _secretStatusLabel.Text = "Key saved in Windows Credential Manager.";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task FetchModelsAsync()
    {
        try
        {
            UseWaitCursor = true;
            var providerId = ResolveProviderId();
            var key = string.IsNullOrWhiteSpace(_apiKeyBox.Text)
                ? WindowsCredentialStore.ReadSecret(providerId)
                : _apiKeyBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Paste an API key first, then click Fetch.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var models = await ModelFetcher.FetchAsync(_baseUrlBox.Text.Trim(), key, CancellationToken.None);
            SetModelChoices(models);
            _statusLabel.Text = models.Count == 0
                ? "The provider responded, but no model ids were found."
                : $"Fetched {models.Count} model id(s). Choose one, then save.";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SaveProfile()
    {
        try
        {
            var definition = BuildDefinition();
            if (!string.IsNullOrWhiteSpace(_apiKeyBox.Text))
            {
                WindowsCredentialStore.WriteSecret(definition.ProviderId, _apiKeyBox.Text.Trim());
                _apiKeyBox.Text = string.Empty;
            }

            _configManager.UpsertProfile(definition);
            _statusLabel.Text = $"Saved {definition.ProfileName}.";
            ReloadProfiles(definition.ProfileName);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void DeleteSelectedProfile()
    {
        if (_profilesList.SelectedItem is not CodexProfile profile)
        {
            return;
        }

        if (profile.IsBuiltInOpenAI)
        {
            MessageBox.Show("The built-in OpenAI profile is managed by Codex and cannot be deleted here.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Delete profile '{profile.ProfileName}' from Codex config?",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _configManager.DeleteProfile(profile);
            if (!_profiles.Any(item =>
                    !item.ProfileName.Equals(profile.ProfileName, StringComparison.OrdinalIgnoreCase) &&
                    item.ProviderId.Equals(profile.ProviderId, StringComparison.OrdinalIgnoreCase)))
            {
                WindowsCredentialStore.DeleteSecret(profile.ProviderId);
            }
            ReloadProfiles();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private ProfileDefinition BuildDefinition()
    {
        var providerName = _providerNameBox.Text.Trim();
        var baseUrl = _baseUrlBox.Text.Trim();
        var providerId = ResolveProviderId();
        var preset = GetActivePreset();
        var envKey = ResolveEnvKey(providerId, preset);
        var model = _modelBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Base URL is required for an OpenAI-compatible provider.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Base URL must be a valid http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Choose or type a model id first.");
        }

        var reasoningEffort = ResolveReasoningEffort(providerId, model, preset);

        return new ProfileDefinition
        {
            ProfileName = MakeProfileName(providerId, model, reasoningEffort),
            ProviderId = providerId,
            ProviderName = providerName,
            BaseUrl = baseUrl,
            EnvKey = envKey,
            Model = model,
            ContextWindow = _contextEnabledBox.Checked ? (int)_contextWindowBox.Value : null,
            ReasoningEffort = reasoningEffort,
            SupportsReasoningSummaries = ResolveReasoningSummaries(preset)
        };
    }

    private static string MakeProfileName(string providerId, string model, string? reasoningEffort)
    {
        var baseName = CodexConfigManager.MakeProfileName(providerId, model);
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return baseName;
        }

        return $"{baseName}-{reasoningEffort}";
    }

    private void SetModelChoices(IEnumerable<string?> models)
    {
        var current = _modelBox.Text.Trim();
        var choices = models
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _modelBox.Items.Clear();
        foreach (var model in choices)
        {
            _modelBox.Items.Add(model);
        }

        if (!string.IsNullOrWhiteSpace(current) && choices.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            _modelBox.Text = current;
        }
        else if (choices.Count > 0)
        {
            _modelBox.Text = choices[0];
        }
        else
        {
            _modelBox.Text = string.Empty;
        }
    }

    private string ResolveProviderId()
    {
        var preset = GetActivePreset();
        if (preset is not null && !preset.IsCustom)
        {
            return preset.ProviderId;
        }

        if (!string.IsNullOrWhiteSpace(_loadedProviderId) && preset?.IsCustom == true)
        {
            return _loadedProviderId;
        }

        return CodexConfigManager.MakeProviderId(_providerNameBox.Text);
    }

    private string ResolveEnvKey(string providerId, ProviderPreset? preset)
    {
        if (!string.IsNullOrWhiteSpace(_loadedEnvKey) &&
            providerId.Equals(_loadedProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return _loadedEnvKey;
        }

        if (preset is not null && !preset.IsCustom)
        {
            return preset.EnvKey;
        }

        return CodexConfigManager.MakeEnvKey(providerId);
    }

    private string? ResolveReasoningEffort(string providerId, string model, ProviderPreset? preset)
    {
        var selected = _reasoningEffortBox.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(selected) && selected != "Auto")
        {
            return selected;
        }

        return preset?.ReasoningEffort;
    }

    private bool? ResolveReasoningSummaries(ProviderPreset? preset)
    {
        return _reasoningSummariesBox.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => preset?.SupportsReasoningSummaries
        };
    }

    private ProviderPreset? GetActivePreset()
    {
        return _presetBox.SelectedItem as ProviderPreset;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
