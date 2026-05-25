namespace CodexProfileTray;

internal sealed class ProfileManagerForm : Form
{
    private readonly CodexConfigManager _configManager;
    private readonly ListBox _profilesList = new();
    private readonly TextBox _profileNameBox = new();
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

    public ProfileManagerForm(CodexConfigManager configManager)
    {
        _configManager = configManager;
        Text = "Manage Codex Profiles";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        Size = new Size(940, 650);
        Font = new Font("Segoe UI", 9F);

        BuildUi();
        ReloadProfiles();
    }

    private void BuildUi()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 270
        };
        Controls.Add(root);

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 4,
            ColumnCount = 1
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Panel1.Controls.Add(leftPanel);

        leftPanel.Controls.Add(new Label
        {
            Text = "Profiles",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold)
        }, 0, 0);

        _profilesList.Dock = DockStyle.Fill;
        _profilesList.IntegralHeight = false;
        _profilesList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        leftPanel.Controls.Add(_profilesList, 0, 1);

        var newButton = new Button
        {
            Text = "New Profile",
            Dock = DockStyle.Fill
        };
        newButton.Click += (_, _) => NewProfile();
        leftPanel.Controls.Add(newButton, 0, 2);

        var deleteButton = new Button
        {
            Text = "Delete Selected",
            Dock = DockStyle.Fill
        };
        deleteButton.Click += (_, _) => DeleteSelectedProfile();
        leftPanel.Controls.Add(deleteButton, 0, 3);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 14, 18, 14),
            ColumnCount = 3,
            RowCount = 13
        };
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        root.Panel2.Controls.Add(rightPanel);

        for (var i = 0; i < 10; i++)
        {
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }

        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        AddHeader(rightPanel);
        AddLabeledControl(rightPanel, "Profile name", _profileNameBox, 1);
        AddLabeledControl(rightPanel, "Provider name", _providerNameBox, 2);
        AddLabeledControl(rightPanel, "Base URL", _baseUrlBox, 3);
        AddLabeledControl(rightPanel, "API key", _apiKeyBox, 4);
        AddLabeledControl(rightPanel, "Model", _modelBox, 5);

        _profileNameBox.PlaceholderText = "my-deepseek-profile";
        _providerNameBox.PlaceholderText = "DeepSeek, OpenRouter, LocalAI, etc.";
        _baseUrlBox.PlaceholderText = "https://api.example.com/v1";
        _apiKeyBox.PlaceholderText = "Paste key only when saving or fetching models";
        _apiKeyBox.UseSystemPasswordChar = true;

        _modelBox.DropDownStyle = ComboBoxStyle.DropDown;
        _modelBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _modelBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        _modelBox.Items.AddRange(new object[] { "gpt-5.5", "gpt-5.4", "deepseek-v4-flash", "deepseek-v4-pro" });

        var saveKeyButton = new Button
        {
            Text = "Save Key",
            Dock = DockStyle.Fill
        };
        saveKeyButton.Click += (_, _) => SaveKeyOnly();
        rightPanel.Controls.Add(saveKeyButton, 2, 4);

        var fetchButton = new Button
        {
            Text = "Fetch Models",
            Dock = DockStyle.Fill
        };
        fetchButton.Click += async (_, _) => await FetchModelsAsync();
        rightPanel.Controls.Add(fetchButton, 2, 5);

        var contextLabel = new Label
        {
            Text = "Context window",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        rightPanel.Controls.Add(contextLabel, 0, 6);

        var contextPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _contextEnabledBox.Text = "Set";
        _contextEnabledBox.AutoSize = true;
        _contextEnabledBox.CheckedChanged += (_, _) => _contextWindowBox.Enabled = _contextEnabledBox.Checked;
        _contextWindowBox.Width = 160;
        _contextWindowBox.Minimum = 1;
        _contextWindowBox.Maximum = 100_000_000;
        _contextWindowBox.Increment = 1000;
        _contextWindowBox.ThousandsSeparator = true;
        _contextWindowBox.Enabled = false;
        contextPanel.Controls.Add(_contextEnabledBox);
        contextPanel.Controls.Add(_contextWindowBox);
        rightPanel.Controls.Add(contextPanel, 1, 6);

        AddLabeledControl(rightPanel, "Reasoning effort", _reasoningEffortBox, 7);
        _reasoningEffortBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningEffortBox.Items.AddRange(new object[] { "", "none", "minimal", "low", "medium", "high", "xhigh" });

        AddLabeledControl(rightPanel, "Reasoning summaries", _reasoningSummariesBox, 8);
        _reasoningSummariesBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasoningSummariesBox.Items.AddRange(new object[] { "Auto", "Yes", "No" });
        _reasoningSummariesBox.SelectedIndex = 0;

        _secretStatusLabel.Dock = DockStyle.Fill;
        _secretStatusLabel.ForeColor = Color.DimGray;
        rightPanel.Controls.Add(_secretStatusLabel, 1, 9);

        var note = new Label
        {
            Text = "Keys are saved in Windows Credential Manager. The Codex config gets an env var name, never the key itself.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray
        };
        rightPanel.Controls.Add(note, 1, 10);
        rightPanel.SetColumnSpan(note, 2);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.DimGray;
        rightPanel.Controls.Add(_statusLabel, 1, 11);
        rightPanel.SetColumnSpan(_statusLabel, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var closeButton = new Button { Text = "Close", Width = 100, Height = 30 };
        closeButton.Click += (_, _) => Close();
        var saveButton = new Button { Text = "Save Profile", Width = 120, Height = 30 };
        saveButton.Click += (_, _) => SaveProfile();
        footer.Controls.Add(closeButton);
        footer.Controls.Add(saveButton);
        rightPanel.Controls.Add(footer, 0, 12);
        rightPanel.SetColumnSpan(footer, 3);
    }

    private static void AddHeader(TableLayoutPanel panel)
    {
        var title = new Label
        {
            Text = "OpenAI-Compatible Profile",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };
        panel.Controls.Add(title, 0, 0);
        panel.SetColumnSpan(title, 3);
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string labelText, Control control, int row)
    {
        panel.Controls.Add(new Label
        {
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 0, row);

        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, 1);
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
            NewProfile();
            return;
        }

        var selected = _profiles.FirstOrDefault(profile =>
            profile.ProfileName.Equals(selectProfileName, StringComparison.OrdinalIgnoreCase));
        _profilesList.SelectedItem = selected ?? _profiles[0];
    }

    private void NewProfile()
    {
        _profilesList.ClearSelected();
        _loadedProviderId = null;
        _loadedEnvKey = null;
        _profileNameBox.Text = string.Empty;
        _providerNameBox.Text = string.Empty;
        _baseUrlBox.Text = string.Empty;
        _apiKeyBox.Text = string.Empty;
        _modelBox.Text = string.Empty;
        _contextEnabledBox.Checked = false;
        _contextWindowBox.Value = 1_000_000;
        _reasoningEffortBox.SelectedIndex = 0;
        _reasoningSummariesBox.SelectedIndex = 0;
        _secretStatusLabel.Text = "No saved key for this new provider yet.";
        _statusLabel.Text = "Create a profile by entering a profile name, base URL, API key, and model.";
    }

    private void LoadSelectedProfile()
    {
        if (_profilesList.SelectedItem is not CodexProfile profile)
        {
            return;
        }

        _loadedProviderId = profile.ProviderId;
        _loadedEnvKey = profile.EnvKey;
        _profileNameBox.Text = profile.ProfileName;
        _providerNameBox.Text = profile.ProviderName ?? profile.ProviderId;
        _baseUrlBox.Text = profile.BaseUrl ?? string.Empty;
        _apiKeyBox.Text = string.Empty;
        _modelBox.Text = profile.Model ?? string.Empty;
        _contextEnabledBox.Checked = profile.ContextWindow.HasValue;
        _contextWindowBox.Enabled = profile.ContextWindow.HasValue;
        _contextWindowBox.Value = Math.Clamp(profile.ContextWindow ?? 1_000_000, 1, 100_000_000);
        _reasoningEffortBox.SelectedItem = profile.ReasoningEffort ?? string.Empty;

        _reasoningSummariesBox.SelectedIndex = profile.SupportsReasoningSummaries switch
        {
            true => 1,
            false => 2,
            _ => 0
        };

        var hasSecret = !profile.IsBuiltInOpenAI && WindowsCredentialStore.HasSecret(profile.ProviderId);
        _secretStatusLabel.Text = profile.IsBuiltInOpenAI
            ? "This profile uses Codex's built-in OpenAI/ChatGPT authentication."
            : hasSecret
                ? "A key is saved in Windows Credential Manager for this provider."
                : "No key saved yet. Paste one and click Save Key or Save Profile.";
        _statusLabel.Text = string.Empty;
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

            var models = await ModelFetcher.FetchAsync(_baseUrlBox.Text.Trim(), key, CancellationToken.None);
            _modelBox.Items.Clear();
            foreach (var model in models)
            {
                _modelBox.Items.Add(model);
            }

            if (models.Count > 0 && string.IsNullOrWhiteSpace(_modelBox.Text))
            {
                _modelBox.Text = models[0];
            }

            _statusLabel.Text = models.Count == 0
                ? "The provider responded, but no model ids were found."
                : $"Fetched {models.Count} model(s).";
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
            _statusLabel.Text = $"Saved profile '{definition.ProfileName}'.";
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
            WindowsCredentialStore.DeleteSecret(profile.ProviderId);
            ReloadProfiles();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private ProfileDefinition BuildDefinition()
    {
        var profileName = _profileNameBox.Text.Trim();
        var providerName = _providerNameBox.Text.Trim();
        var baseUrl = _baseUrlBox.Text.Trim();
        var model = _modelBox.Text.Trim();
        var providerId = ResolveProviderId();
        var envKey = providerId.Equals(_loadedProviderId, StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(_loadedEnvKey)
            ? _loadedEnvKey
            : CodexConfigManager.MakeEnvKey(providerId);

        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("Profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException("Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Base URL is required for a custom OpenAI-compatible provider.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Base URL must be a valid http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Model is required. You can type any model id, for example gpt-5.5 or gpt-5.4.");
        }

        return new ProfileDefinition
        {
            ProfileName = profileName,
            ProviderId = providerId,
            ProviderName = providerName,
            BaseUrl = baseUrl,
            EnvKey = envKey,
            Model = model,
            ContextWindow = _contextEnabledBox.Checked ? (int)_contextWindowBox.Value : null,
            ReasoningEffort = string.IsNullOrWhiteSpace((string?)_reasoningEffortBox.SelectedItem) ? null : (string)_reasoningEffortBox.SelectedItem,
            SupportsReasoningSummaries = _reasoningSummariesBox.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null
            }
        };
    }

    private string ResolveProviderId()
    {
        if (!string.IsNullOrWhiteSpace(_loadedProviderId) &&
            _profilesList.SelectedItem is CodexProfile selected &&
            selected.ProfileName.Equals(_profileNameBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return _loadedProviderId;
        }

        return CodexConfigManager.MakeProviderId(_profileNameBox.Text);
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
