namespace CodexProfileTray;

internal sealed record CodexLaunchTarget(string FileName, IReadOnlyList<string> PrefixArguments);

internal static class CodexExecutableLocator
{
    public static CodexLaunchTarget Resolve()
    {
        var candidates = new List<string>();
        AddIfPresent(candidates, Environment.GetEnvironmentVariable("CODEX_CLI_PATH"));

        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(localBin))
        {
            candidates.AddRange(
                EnumerateFilesSafe(localBin, "codex.exe")
                    .OrderByDescending(GetLastWriteTimeUtcSafe));
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

    private static DateTime GetLastWriteTimeUtcSafe(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, string searchPattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] files;
            try
            {
                files = Directory.EnumerateFiles(directory, searchPattern).ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            string[] children;
            try
            {
                children = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push(child);
            }
        }
    }
}
