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

    public void Launch(string workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
        {
            throw new InvalidOperationException("Choose an existing project folder first.");
        }

        _configManager.EnsureProviderCompatibility();
        _configManager.EnsureAppModelCatalog();
        RestartCodexIfNeeded();

        var target = CodexExecutableLocator.Resolve();
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

        startInfo.ArgumentList.Add("app");
        startInfo.ArgumentList.Add(workspace);

        MakeApiKeysAvailable(startInfo);
        Process.Start(startInfo);
    }

    private static void RestartCodexIfNeeded()
    {
        var runningCodex = Process.GetProcessesByName("Codex")
            .Where(IsDesktopCodexProcess)
            .ToArray();

        if (runningCodex.Length == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            "Codex is already running. Restart it now so provider and model changes reload? Choose No to keep the current app open and open the workspace anyway.",
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

    private void MakeApiKeysAvailable(ProcessStartInfo startInfo)
    {
        foreach (var profile in _configManager.LoadProfiles()
                     .Where(profile => !profile.IsBuiltInOpenAI)
                     .GroupBy(profile => profile.ProviderId, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            MakeApiKeyAvailable(profile, startInfo);
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
