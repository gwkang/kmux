using KMux.Core.Models;

namespace KMux.Terminal.Shell;

public static class ShellProfileDetector
{
    // cmd.exe PROMPT that emits OSC 7 (current working directory notification).
    // $E = ESC, $P = current path, $E\ = ST (string terminator), $G = >, $S = space.
    // This keeps the visual prompt identical to the default ($P$G) while adding
    // the invisible OSC 7 prefix that lets KMux track the shell's working directory.
    private const string CmdPromptWithOsc7 = "$E]7;file:///$P$E\\$P$G";

    public static IReadOnlyList<ShellProfile> DetectProfiles()
    {
        var profiles = new List<ShellProfile>();

        if (TryFind("pwsh.exe",       out var pwsh))
        {
            var p = new ShellProfile { Name = "PowerShell", Executable = pwsh!, Arguments = "-NoLogo" };
            p.EnvironmentVariables["WT_SESSION"] = Guid.NewGuid().ToString();
            profiles.Add(p);
        }
        if (TryFind("powershell.exe", out var ps1))
        {
            var p = new ShellProfile { Name = "Windows PowerShell", Executable = ps1!, Arguments = "-NoLogo" };
            p.EnvironmentVariables["WT_SESSION"] = Guid.NewGuid().ToString();
            profiles.Add(p);
        }
        if (TryFind("cmd.exe", out var cmd))
        {
            var p = new ShellProfile { Name = "Command Prompt", Executable = cmd! };
            p.EnvironmentVariables["PROMPT"] = CmdPromptWithOsc7;
            profiles.Add(p);
        }
        if (TryFind("wsl.exe", out var wsl))    profiles.Add(new ShellProfile { Name = "WSL",                Executable = wsl! });

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
        {
            var p = new ShellProfile { Name = "Claude Code", Executable = "cmd.exe", Arguments = "/k claude" };
            p.EnvironmentVariables["PROMPT"] = CmdPromptWithOsc7;
            profiles.Add(p);
        }

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
