using System.Diagnostics;
using System.Runtime.InteropServices;

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

        _configManager.SetActiveProfile(profile);
        RestartCodexIfNeeded(profile);

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

        MakeApiKeyAvailable(profile, startInfo);
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

    private static void RestartCodexIfNeeded(CodexProfile profile)
    {
        var runningCodex = Process.GetProcessesByName("Codex")
            .Where(IsDesktopCodexProcess)
            .ToArray();

        if (runningCodex.Length == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Codex is already running. Restart it now so '{profile.ProfileName}' becomes the active profile?",
            "Codex Profile Tray",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        foreach (var process in runningCodex)
        {
            try
            {
                if (!process.CloseMainWindow())
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort: another process may exit while we are closing windows.
            }
        }

        foreach (var process in runningCodex)
        {
            try
            {
                if (!process.WaitForExit(5000) && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static bool IsDesktopCodexProcess(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(path) &&
                   path.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFileName(path).Equals("Codex.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void MakeApiKeyAvailable(CodexProfile profile, ProcessStartInfo startInfo)
    {
        if (string.IsNullOrWhiteSpace(profile.EnvKey) || profile.IsBuiltInOpenAI)
        {
            return;
        }

        var secret = WindowsCredentialStore.ReadSecret(profile.ProviderId)
            ?? GetEnvironmentVariableAnyScope(profile.EnvKey);

        if (!string.IsNullOrWhiteSpace(secret))
        {
            Environment.SetEnvironmentVariable(profile.EnvKey, secret, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(profile.EnvKey, secret, EnvironmentVariableTarget.User);
            startInfo.Environment[profile.EnvKey] = secret;
            BroadcastEnvironmentChange();
        }
    }

    private static void BroadcastEnvironmentChange()
    {
        SendMessageTimeout(
            (IntPtr)0xffff,
            0x001A,
            IntPtr.Zero,
            "Environment",
            0,
            5000,
            out _);
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

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
}
