# Codex Profile Tray

Codex Profile Tray is a small native Windows tray app for switching OpenAI Codex profiles without touching the command line.

It is designed for people who use the Codex Windows app with multiple model providers, including OpenAI-compatible APIs such as DeepSeek, OpenRouter, local gateways, or other compatible endpoints.

## Features

- Runs from the Windows system tray
- Opens the Codex Windows app with a selected profile
- Creates and edits Codex profiles in `%USERPROFILE%\.codex\config.toml`
- Supports any OpenAI-compatible API base URL
- Lets users type any model id, such as `gpt-5.5`, `gpt-5.4`, `deepseek-v4-pro`, or a local model name
- Fetches model ids from `GET <base_url>/models` when the provider supports it
- Optional context window field
- Optional reasoning effort and reasoning summaries fields
- Stores API keys in Windows Credential Manager, not in the repo and not in Codex config

## How It Works

Codex Profile Tray writes provider/profile sections like this:

```toml
[model_providers.my-provider]
name = "My Provider"
base_url = "https://api.example.com/v1"
env_key = "CODEX_PROFILE_TRAY_MY_PROVIDER_API_KEY"

[profiles.my-profile]
model_provider = "my-provider"
model = "gpt-5.5"
```

The API key itself is saved in Windows Credential Manager. When you launch Codex from the tray app, the app injects the right environment variable into the Codex process.

## Usage

1. Start `CodexProfileTray.exe`.
2. Right-click the tray icon.
3. Choose **Manage Profiles...**.
4. Enter:
   - Profile name
   - Provider name
   - Base URL
   - API key
   - Model id
   - Optional context window
5. Click **Fetch Models** if the provider supports the OpenAI-compatible `/models` endpoint.
6. Click **Save Profile**.
7. Right-click the tray icon again and choose the profile under **Open Codex With Profile**.

## Base URL Examples

- OpenAI-compatible API: `https://api.example.com/v1`
- DeepSeek: `https://api.deepseek.com`
- Local gateway: `http://localhost:1234/v1`

If model fetching fails, the profile can still work. Some providers disable `/models`, require a different API prefix, or need account permissions.

## Build From Source

Requires Windows and the .NET 8 SDK or newer.

```powershell
dotnet build .\CodexProfileTray.csproj --configuration Release
```

## Publish A Single EXE

```powershell
.\scripts\publish.ps1
```

The published app appears under `artifacts\publish`.

## Security

- Do not paste API keys into issues, screenshots, or commits.
- Keys are stored under Windows Credential Manager target names starting with `CodexProfileTray/`.
- `config.toml` backups are written next to your Codex config before profile edits.
- This repository intentionally ignores Codex config, auth files, settings files, and secret-looking files.
