namespace KMux.Core.Themes;

/// <summary>All color values for a single KMux theme (WPF UI + xterm.js terminal).</summary>
public record ThemeColors
{
    // ── WPF UI palette ────────────────────────────────────────────────────────
    public required string Base     { get; init; }  // main window background
    public required string Mantle   { get; init; }  // tab bar background
    public required string Crust    { get; init; }  // status bar background
    public required string Surface0 { get; init; }  // hover bg / unfocused border
    public required string Surface1 { get; init; }  // splitter hover / secondary bg
    public required string Overlay0 { get; init; }  // close button / minor hints
    public required string Subtext0 { get; init; }  // status bar text
    public required string Subtext1 { get; init; }  // muted tab text / icons
    public required string Text     { get; init; }  // primary text / active tab
    public required string Accent   { get; init; }  // active tab indicator / focus border
    public required string Green    { get; init; }  // activity indicator
    public required string Red      { get; init; }  // recording indicator / delete

    // ── xterm.js terminal theme ───────────────────────────────────────────────
    public required string TermBg           { get; init; }
    public required string TermFg           { get; init; }
    public required string TermCursor       { get; init; }
    public required string TermCursorAccent { get; init; }
    public required string TermBlack        { get; init; }
    public required string TermRed          { get; init; }
    public required string TermGreen        { get; init; }
    public required string TermYellow       { get; init; }
    public required string TermBlue         { get; init; }
    public required string TermMagenta      { get; init; }
    public required string TermCyan         { get; init; }
    public required string TermWhite        { get; init; }
    public required string TermBrightBlack  { get; init; }
    public required string TermBrightRed    { get; init; }
    public required string TermBrightGreen  { get; init; }
    public required string TermBrightYellow { get; init; }
    public required string TermBrightBlue   { get; init; }
    public required string TermBrightMagenta{ get; init; }
    public required string TermBrightCyan   { get; init; }
    public required string TermBrightWhite  { get; init; }
    public required string TermSelection    { get; init; }
}
