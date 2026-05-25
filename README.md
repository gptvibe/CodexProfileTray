# Codex Profile Tray

A small Windows tray app for people who want to switch Codex providers without touching the command line.

It helps you use the normal Codex Windows app with OpenAI-compatible APIs such as DeepSeek, OpenRouter, local model gateways, or your own proxy.

## What This App Does

- Sits in the Windows system tray
- Opens the Codex Windows app with a saved profile
- Creates Codex profiles for you
- Saves API keys in Windows Credential Manager
- Keeps API keys out of GitHub and out of `config.toml`
- Supports DeepSeek with one-click preset setup
- Supports any OpenAI-compatible base URL
- Can fetch model names from `/models` when the provider supports it

## Easiest Setup

1. Download `CodexProfileTray.exe` from the latest release.
2. Double-click it.
3. Look for the tray icon near the Windows clock.
4. Right-click the icon.
5. Click **Manage Providers...**.
6. Pick **DeepSeek**.
7. Paste your DeepSeek API key.
8. Click **Save provider**.

That creates these Codex profiles automatically:

- `deepseek-v4-pro`
- `deepseek-v4-flash`

You do not need to type those model names yourself.

## Opening Codex

1. Right-click the tray icon.
2. Click **Choose Project Folder...** once.
3. Right-click the tray icon again.
4. Pick a profile under **Open Codex With Profile**.

The tray app only handles setup and launching. Your actual coding session still happens inside the normal Codex Windows app.

## Custom Provider Setup

Use **Custom OpenAI-compatible API** when your provider is not DeepSeek.

You need:

- A display name, like `OpenRouter`
- A base URL, like `https://openrouter.ai/api/v1`
- An API key, if your provider needs one
- One or more model IDs

You can type model IDs manually, one per line, or click **Fetch models**. Fetching uses:

```text
GET <base_url>/models
```

Some providers do not expose model lists, or they require a different API prefix. If fetching fails, just type the model name manually.

## Reasoning Effort

Reasoning effort cannot be reliably fetched from the standard OpenAI-compatible `/models` response. Different providers expose different metadata, and many expose none.

So the app keeps reasoning effort as an optional setting.

For DeepSeek, **Auto** currently writes:

- `high` for `deepseek-v4-pro`
- `low` for `deepseek-v4-flash`

For custom providers, leave it on **Auto** unless you know the provider supports a specific value.

## Context Window

The context window field is optional.

Set it only if you know the provider and model support a specific context size. Otherwise leave it unchecked.

## Where Keys Are Stored

API keys are stored in Windows Credential Manager under names like:

```text
CodexProfileTray/deepseek
```

The Codex config only receives an environment variable name, not the secret itself.

## Build From Source

Requires Windows and the .NET 8 SDK or newer.

```powershell
dotnet build .\CodexProfileTray.csproj --configuration Release
```

## Publish A Single EXE

```powershell
.\scripts\publish.ps1
```

The published app appears under:

```text
artifacts\publish\CodexProfileTray.exe
```

## Safety Notes

- Never commit API keys.
- Never paste keys into GitHub issues.
- If you accidentally shared a key, rotate it at the provider dashboard.
- This repository ignores build output, local settings, Codex auth files, and secret-looking files.
