using KMux.Core.Models;

namespace KMux.Terminal.Shell;

public static class ShellProfileDetector
{
    public static IReadOnlyList<ShellProfile> DetectProfiles()
    {
        var profiles = new List<ShellProfile>();

        if (TryFind("pwsh.exe",        out var pwsh))   profiles.Add(new ShellProfile { Name = "PowerShell",         Executable = pwsh!, Arguments = "-NoLogo" });
        if (TryFind("powershell.exe",  out var ps1))    profiles.Add(new ShellProfile { Name = "Windows PowerShell", Executable = ps1!,  Arguments = "-NoLogo" });
        if (TryFind("cmd.exe",         out var cmd))    profiles.Add(new ShellProfile { Name = "Command Prompt",     Executable = cmd! });
        if (TryFind("wsl.exe",         out var wsl))    profiles.Add(new ShellProfile { Name = "WSL",                Executable = wsl! });

        // Git Bash
        string[] gitBashPaths =
        [
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
        ];
        foreach (var p in gitBashPaths)
            if (File.Exists(p)) { profiles.Add(new ShellProfile { Name = "Git Bash", Executable = p, Arguments = "--login -i" }); break; }

        // Claude Code (if `claude` is on PATH)
        if (TryFind("claude.cmd", out _) || TryFind("claude.exe", out _))
            profiles.Add(new ShellProfile { Name = "Claude Code", Executable = "cmd.exe", Arguments = "/k claude" });

        return profiles.Count > 0 ? profiles : [ShellProfile.Cmd];
    }

    private static bool TryFind(string exe, out string? fullPath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';'))
        {
            var candidate = Path.Combine(dir.Trim(), exe);
            if (File.Exists(candidate)) { fullPath = candidate; return true; }
        }
        // Also try system32
        var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
        if (File.Exists(sys)) { fullPath = sys; return true; }

        fullPath = null;
        return false;
    }
}
