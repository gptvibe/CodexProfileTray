# Codex Profile Tray

A small Windows tray app for switching Codex to any provider that exposes an OpenAI-compatible API URL.

The goal is simple: no command line, no hand-editing TOML, and no API keys committed anywhere.

## What It Does

- Runs in the Windows system tray
- Opens the normal Codex Windows app and lets Codex handle model changes
- Creates Codex provider/profile config for you
- Adds saved provider models to the Codex app model picker
- Works with OpenAI-compatible API URLs
- Bridges Codex's Responses API calls to provider `/chat/completions` APIs when needed
- Lets you type model IDs manually
- Can fetch model IDs from `GET <base_url>/models` when the provider supports it
- Stores API keys in Windows Credential Manager
- Keeps secrets out of GitHub and out of Codex config files

## Install

1. Download `CodexProfileTray.exe` from the latest release.
2. Double-click it.
3. Look near the Windows clock for the tray icon.

Windows may show a SmartScreen warning because this is an unsigned open-source app. Choose **More info** and **Run anyway** if you trust the download.

## Add A Provider

1. Right-click the tray icon.
2. Click **Manage Providers...**.
3. Pick a provider from the dropdown.
4. Paste your API key.
5. Click **Fetch** to load model IDs from that provider.
6. Choose the default model.
7. Choose a reasoning effort, or leave it on **Auto**.
8. Click **Save**.

For the built-in provider presets, the base URL is filled in automatically. You only need to paste the API key and fetch models.

Some presets include common starter models before you fetch. For example, choosing DeepSeek shows `deepseek-v4-flash` and `deepseek-v4-pro` immediately.

Use **Custom OpenAI-compatible API** only when the provider is not in the dropdown. In that case, enter the provider's base URL, for example:

```text
https://api.example.com/v1
```

The app saves the provider, makes it the active Codex provider, and publishes the provider's models to Codex's own model picker. After that, choose the model and reasoning effort in the Codex Windows app.

## Included Presets

- OpenAI API
- OpenRouter
- DeepSeek
- Groq
- Together AI
- xAI
- Perplexity
- Cerebras
- Fireworks AI
- Custom OpenAI-compatible API

## Open Codex

1. Right-click the tray icon.
2. Click **Choose Project Folder...**.
3. Pick your project folder.
4. Right-click the tray icon again.
5. Click **Open Codex**.

When you save a provider, the tray app makes that provider the active Codex config before opening Codex. If Codex is already running, the tray app asks to restart it so the desktop app reloads provider and model picker changes.

Your coding session still happens in the normal Codex Windows app. This tray app only handles setup, provider registration, and launching.

## Why The Tray Must Stay Running

Codex talks to providers through the Responses API. Many OpenAI-compatible providers use the older chat completions route instead.

For those providers, Codex Profile Tray starts a tiny local compatibility server on your PC:

```text
http://127.0.0.1:17345
```

Codex sends `/responses` requests to that local address. The tray app then sends the real request to the provider's `/chat/completions` endpoint using the API key saved in Windows Credential Manager.

This is why the tray app should stay open while you use a non-OpenAI provider. If you see an error ending in `/responses`, install the latest release and launch Codex from the tray again so your profile is migrated automatically.

## Model Fetching

The **Fetch** button calls:

```text
GET <base_url>/models
```

The app asks for an API key before fetching. If you already saved a key for that provider, the saved key can be used.

If fetching fails, it does not always mean the provider cannot work. Some providers disable model listing, require a different URL prefix, or need account permissions. In that case, type the model ID manually.

## Reasoning Effort

Reasoning effort is not reliably available from the standard OpenAI-compatible `/models` response.

Leave it on **Auto** unless your provider documents a specific value such as `low`, `medium`, `high`, or `xhigh`.

## Context Window

The context window field is optional.

Set it only if your provider documents a specific context size for the model. Otherwise leave it unchecked.

## Where API Keys Go

API keys are stored in Windows Credential Manager under names like:

```text
CodexProfileTray/my-provider
```

Codex config receives only an environment variable name, not the secret value.

When opening Codex, the tray app also makes the key available through that user environment variable so the Codex desktop app can read it.

## Build From Source

Requires Windows and the .NET 8 SDK or newer.

```powershell
dotnet build .\CodexProfileTray.csproj --configuration Release
```

## Publish A Single EXE

```powershell
.\scripts\publish.ps1
```

The published app appears here:

```text
artifacts\publish\CodexProfileTray.exe
```

## Safety Notes

- Never commit API keys.
- Never paste keys into GitHub issues.
- If a key leaks, rotate it at the provider dashboard.
- This repository ignores build output, local settings, Codex auth files, and secret-looking files.
