namespace CodexProfileTray;

internal sealed class ProfileManagerForm : Form
{
    private static readonly Color Page = Color.FromArgb(246, 248, 251);
    private static readonly Color Sidebar = Color.FromArgb(17, 24, 39);
    private static readonly Color SidebarSelected = Color.FromArgb(31, 41, 55);
    private static readonly Color SidebarMuted = Color.FromArgb(156, 163, 175);
    private static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);
    private static readonly Color TextMuted = Color.FromArgb(86, 104, 128);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);
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
        MinimumSize = new Size(1040, 660);
        Size = new Size(1100, 700);
        BackColor = Page;
        Font = new Font("Segoe UI", 10F);
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
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
            Padding = new Padding(24, 24, 22, 24),
            RowCount = 5,
            ColumnCount = 1,
            BackColor = Sidebar
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var title = new Label
        {
            Text = "Profiles",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };
        sidebar.Controls.Add(title, 0, 0);

        var hint = new Label
        {
            Text = "OpenAI-compatible providers",
            Dock = DockStyle.Fill,
            ForeColor = SidebarMuted,
            TextAlign = ContentAlignment.TopLeft
        };
        sidebar.Controls.Add(hint, 0, 1);

        _profilesList.Dock = DockStyle.Fill;
        _profilesList.BorderStyle = BorderStyle.None;
        _profilesList.BackColor = Sidebar;
        _profilesList.ForeColor = Color.White;
        _profilesList.ItemHeight = 60;
        _profilesList.IntegralHeight = false;
        _profilesList.DrawMode = DrawMode.OwnerDrawFixed;
        _profilesList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        _profilesList.DrawItem += DrawProfileItem;
        sidebar.Controls.Add(_profilesList, 0, 2);

        var newButton = CreateButton("New provider", accent: false);
        newButton.Dock = DockStyle.Fill;
        newButton.Click += (_, _) => NewProviderSetup();
        sidebar.Controls.Add(newButton, 0, 3);

        var deleteButton = CreateButton("Delete", accent: false);
        deleteButton.Dock = DockStyle.Fill;
        deleteButton.Click += (_, _) => DeleteSelectedProfile();
        sidebar.Controls.Add(deleteButton, 0, 4);

        return sidebar;
    }

    private Control BuildEditor()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(44, 34, 44, 28),
            RowCount = 13,
            ColumnCount = 3,
            BackColor = Page
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));

        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var header = new Label
        {
            Text = "Provider Setup",
            Dock = DockStyle.Fill,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        editor.Controls.Add(header, 0, 0);
        editor.SetColumnSpan(header, 3);

        var intro = new Label
        {
            Text = "Pick a provider preset first, then save the key and default model you want Codex Desktop to expose.",
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        editor.Controls.Add(intro, 0, 1);
        editor.SetColumnSpan(intro, 3);

        StyleCombo(_presetBox);
        _presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _presetBox.MaxDropDownItems = 10;
        _presetBox.Items.AddRange(ProviderPreset.All.Cast<object>().ToArray());
        _presetBox.SelectedIndexChanged += (_, _) => ApplySelectedPreset();
        AddLabeledControl(editor, "Provider", _presetBox, 2, columnSpan: 2);

        StyleTextBox(_providerNameBox);
        _providerNameBox.PlaceholderText = "Provider display name";
        AddLabeledControl(editor, "Display", _providerNameBox, 3, columnSpan: 2);

        StyleTextBox(_baseUrlBox);
        _baseUrlBox.PlaceholderText = "https://api.example.com/v1";
        AddLabeledControl(editor, "Base URL", _baseUrlBox, 4, columnSpan: 2);

        StyleTextBox(_apiKeyBox);
        _apiKeyBox.PlaceholderText = "Paste API key";
        _apiKeyBox.UseSystemPasswordChar = true;
        AddLabeledControl(editor, "API key", _apiKeyBox, 5);

        var saveKeyButton = CreateButton("Save key", accent: false);
        saveKeyButton.Click += (_, _) => SaveKeyOnly();
        editor.Controls.Add(saveKeyButton, 2, 5);

        StyleCombo(_modelBox);
        _modelBox.DropDownStyle = ComboBoxStyle.DropDown;
        _modelBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _modelBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        AddLabeledControl(editor, "Model", _modelBox, 6);

        var fetchButton = CreateButton("Fetch", accent: false);
        fetchButton.Click += async (_, _) => await FetchModelsAsync();
        editor.Controls.Add(fetchButton, 2, 6);

        var contextPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Page
        };
        _contextEnabledBox.Text = "Set a context window";
        _contextEnabledBox.AutoSize = true;
        _contextEnabledBox.ForeColor = TextPrimary;
        _contextEnabledBox.CheckedChanged += (_, _) => _contextWindowBox.Enabled = _contextEnabledBox.Checked;
        _contextWindowBox.Width = 190;
        _contextWindowBox.Height = 30;
        _contextWindowBox.Minimum = 1;
        _contextWindowBox.Maximum = 100_000_000;
        _contextWindowBox.Increment = 1000;
        _contextWindowBox.ThousandsSeparator = true;
        _contextWindowBox.Enabled = false;
        contextPanel.Controls.Add(_contextEnabledBox);
        contextPanel.Controls.Add(_contextWindowBox);
        AddLabeledControl(editor, "Context", contextPanel, 7, columnSpan: 2);

        StyleCombo(_reasoningEffortBox);
        _reasoningEffortBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningEffortBox.Items.AddRange(new object[] { "Auto", "none", "minimal", "low", "medium", "high", "xhigh" });
        _reasoningEffortBox.SelectedIndex = 0;
        AddLabeledControl(editor, "Reasoning", _reasoningEffortBox, 8, columnSpan: 2);

        StyleCombo(_reasoningSummariesBox);
        _reasoningSummariesBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningSummariesBox.Items.AddRange(new object[] { "Auto", "Yes", "No" });
        _reasoningSummariesBox.SelectedIndex = 0;
        AddLabeledControl(editor, "Summaries", _reasoningSummariesBox, 9, columnSpan: 2);

        _secretStatusLabel.AutoSize = true;
        _secretStatusLabel.Dock = DockStyle.Fill;
        _secretStatusLabel.ForeColor = TextMuted;
        _secretStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _secretStatusLabel.Padding = new Padding(4, 0, 0, 0);
        editor.Controls.Add(_secretStatusLabel, 1, 10);
        editor.SetColumnSpan(_secretStatusLabel, 2);

        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = TextMuted;
        _statusLabel.Padding = new Padding(4, 0, 0, 0);
        editor.Controls.Add(_statusLabel, 1, 11);
        editor.SetColumnSpan(_statusLabel, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Page
        };
        var closeButton = CreateButton("Close", accent: false);
        closeButton.Click += (_, _) => Close();
        var saveButton = CreateButton("Save provider", accent: true);
        saveButton.Width = 150;
        saveButton.Click += (_, _) => SaveProfile();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(saveButton);
        editor.Controls.Add(footer, 0, 12);
        editor.SetColumnSpan(footer, 3);

        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Page
        };
        scrollHost.Controls.Add(editor);
        return scrollHost;
    }

    private void DrawProfileItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = e.Bounds;
        using var background = new SolidBrush(selected ? SidebarSelected : Sidebar);
        e.Graphics.FillRectangle(background, bounds);
        if (selected)
        {
            using var accentBrush = new SolidBrush(Accent);
            e.Graphics.FillRectangle(accentBrush, bounds.Left, bounds.Top + 8, 4, bounds.Height - 16);
        }

        if (_profilesList.Items[e.Index] is not CodexProfile profile)
        {
            return;
        }

        var titleRect = new Rectangle(bounds.Left + 16, bounds.Top + 10, bounds.Width - 28, 23);
        var subtitleRect = new Rectangle(bounds.Left + 16, bounds.Top + 34, bounds.Width - 28, 20);
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
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(0, 0, 18, 0)
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
            Margin = new Padding(5, 6, 5, 6),
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
        textBox.Height = 30;
        textBox.Margin = new Padding(0, 7, 12, 7);
    }

    private static void StyleCombo(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.White;
        comboBox.ForeColor = TextPrimary;
        comboBox.Height = 30;
        comboBox.Margin = new Padding(0, 7, 12, 7);
        comboBox.IntegralHeight = false;
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
        _statusLabel.Text = "Choose a provider, paste an API key, then fetch models. Use Custom for providers not listed.";
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
        _statusLabel.Text = "Choose the default model, then save. The Codex app model picker will handle later changes.";
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
        _statusLabel.Text = "This provider and model will be available inside the Codex app picker.";
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
            _configManager.SetActiveProfile(definition);
            _statusLabel.Text = $"Saved {definition.ProviderName}. Open Codex and choose models in the app picker.";
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
            UseProxy = ProviderProxyServer.ShouldProxyProvider(providerId),
            KnownModels = GetModelChoices().Append(model).ToArray(),
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

    private IEnumerable<string> GetModelChoices()
    {
        foreach (var item in _modelBox.Items)
        {
            if (!string.IsNullOrWhiteSpace(item?.ToString()))
            {
                yield return item.ToString()!;
            }
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
