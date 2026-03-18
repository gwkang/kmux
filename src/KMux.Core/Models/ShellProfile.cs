namespace KMux.Core.Models;

public class ShellProfile
{
    public string Name { get; set; } = "Default";
    public string Executable { get; set; } = "cmd.exe";
    public string Arguments { get; set; } = "";
    public string WorkingDir { get; set; } = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile);
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    public static ShellProfile Cmd => new()
    {
        Name = "Command Prompt",
        Executable = "cmd.exe",
        Arguments = ""
    };

    public static ShellProfile PowerShell => new()
    {
        Name = "PowerShell",
        Executable = "pwsh.exe",
        Arguments = "-NoLogo"
    };

    public static ShellProfile PowerShellLegacy => new()
    {
        Name = "Windows PowerShell",
        Executable = "powershell.exe",
        Arguments = "-NoLogo"
    };

    public static ShellProfile GitBash => new()
    {
        Name = "Git Bash",
        Executable = @"C:\Program Files\Git\bin\bash.exe",
        Arguments = "--login -i"
    };

    public static ShellProfile Wsl => new()
    {
        Name = "WSL",
        Executable = "wsl.exe",
        Arguments = ""
    };

    public static ShellProfile ClaudeCode => new()
    {
        Name = "Claude Code",
        Executable = "cmd.exe",
        Arguments = "/k claude"
    };

    public ShellProfile WithWorkingDir(string dir) => new()
    {
        Name       = Name,
        Executable = Executable,
        Arguments  = Arguments,
        WorkingDir = dir,
        EnvironmentVariables = new Dictionary<string, string>(EnvironmentVariables)
    };

    public ShellProfile Clone() => WithWorkingDir(WorkingDir);
}
