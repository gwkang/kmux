namespace KMux.Core.Models;

public class AppSettings
{
    public string ThemeName          { get; set; } = "Catppuccin Mocha";
    public string TerminalFontFamily { get; set; } = "Cascadia Code";
    public int    TerminalFontSize   { get; set; } = 14;
    public bool   RestoreOnStartup   { get; set; } = false;
}
