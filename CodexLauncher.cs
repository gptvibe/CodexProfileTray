using System.Diagnostics;

namespace CodexProfileTray;

internal sealed class CodexLauncher
{
    private readonly CodexConfigManager _configManager;

    public CodexLauncher(CodexConfigManager configManager)
    {
        _configManager = configManager;
    }

    public void Launch(CodexProfile profile, string workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
        {
            throw new InvalidOperationException("Choose an existing project folder first.");
        }

        var target = ResolveCodexExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = target.FileName,
            WorkingDirectory = workspace,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in target.PrefixArguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profile.ProfileName);
        startInfo.ArgumentList.Add("app");
        startInfo.ArgumentList.Add(workspace);

        InjectApiKey(profile, startInfo);
        Process.Start(startInfo);
    }

    private static CodexLaunchTarget ResolveCodexExecutable()
    {
        var candidates = new List<string>();
        AddIfPresent(candidates, Environment.GetEnvironmentVariable("CODEX_CLI_PATH"));

        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(localBin))
        {
            candidates.AddRange(
                Directory.GetFiles(localBin, "codex.exe", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Select(file => file.FullName));
        }

        candidates.AddRange(FindOnPath("codex.exe"));
        candidates.AddRange(FindOnPath("codex.cmd"));
        candidates.AddRange(FindOnPath("codex.ps1"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var extension = Path.GetExtension(candidate).ToLowerInvariant();
            if (extension == ".exe")
            {
                return new CodexLaunchTarget(candidate, Array.Empty<string>());
            }

            if (extension == ".ps1")
            {
                return new CodexLaunchTarget(ResolvePowerShell(), new[] { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", candidate });
            }

            if (extension is ".cmd" or ".bat")
            {
                return new CodexLaunchTarget(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", new[] { "/d", "/c", candidate });
            }
        }

        throw new FileNotFoundException("Could not find the Codex CLI. Install Codex first, then try again.");
    }

    private static string ResolvePowerShell()
    {
        return FindOnPath("pwsh.exe").FirstOrDefault()
            ?? FindOnPath("powershell.exe").FirstOrDefault()
            ?? "powershell.exe";
    }

    private void InjectApiKey(CodexProfile profile, ProcessStartInfo startInfo)
    {
        if (string.IsNullOrWhiteSpace(profile.EnvKey) || profile.IsBuiltInOpenAI)
        {
            return;
        }

        var secret = WindowsCredentialStore.ReadSecret(profile.ProviderId)
            ?? GetEnvironmentVariableAnyScope(profile.EnvKey);

        if (!string.IsNullOrWhiteSpace(secret))
        {
            startInfo.Environment[profile.EnvKey] = secret;
        }
    }

    private static string? GetEnvironmentVariableAnyScope(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
    }

    private static void AddIfPresent(List<string> items, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            items.Add(value);
        }
    }

    private static IEnumerable<string> FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private sealed record CodexLaunchTarget(string FileName, IReadOnlyList<string> PrefixArguments);
}
