namespace KMux.Core.Themes;

public static class BuiltInThemes
{
    public static IReadOnlyDictionary<string, ThemeColors> All { get; } =
        new Dictionary<string, ThemeColors>
        {
            ["Catppuccin Mocha"] = new()
            {
                Base = "#1e1e2e", Mantle = "#181825", Crust = "#11111b",
                Surface0 = "#313244", Surface1 = "#45475a", Overlay0 = "#6c7086",
                Subtext0 = "#585b70", Subtext1 = "#a6adc8",
                Text = "#cdd6f4", Accent = "#89b4fa",
                Green = "#a6e3a1", Red = "#f38ba8",

                TermBg = "#1e1e2e", TermFg = "#cdd6f4",
                TermCursor = "#f5e0dc", TermCursorAccent = "#1e1e2e",
                TermBlack = "#45475a", TermRed = "#f38ba8",
                TermGreen = "#a6e3a1", TermYellow = "#f9e2af",
                TermBlue = "#89b4fa", TermMagenta = "#f5c2e7",
                TermCyan = "#94e2d5", TermWhite = "#bac2de",
                TermBrightBlack = "#585b70", TermBrightRed = "#f38ba8",
                TermBrightGreen = "#a6e3a1", TermBrightYellow = "#f9e2af",
                TermBrightBlue = "#89b4fa", TermBrightMagenta = "#f5c2e7",
                TermBrightCyan = "#94e2d5", TermBrightWhite = "#a6adc8",
                TermSelection = "#45475a",
            },

            ["Catppuccin Latte"] = new()
            {
                Base = "#eff1f5", Mantle = "#e6e9ef", Crust = "#dce0e8",
                Surface0 = "#ccd0da", Surface1 = "#acb0be", Overlay0 = "#9ca0b0",
                Subtext0 = "#8c8fa1", Subtext1 = "#6c6f85",
                Text = "#4c4f69", Accent = "#1e66f5",
                Green = "#40a02b", Red = "#d20f39",

                TermBg = "#eff1f5", TermFg = "#4c4f69",
                TermCursor = "#dc8a78", TermCursorAccent = "#eff1f5",
                TermBlack = "#5c5f77", TermRed = "#d20f39",
                TermGreen = "#40a02b", TermYellow = "#df8e1d",
                TermBlue = "#1e66f5", TermMagenta = "#ea76cb",
                TermCyan = "#179299", TermWhite = "#acb0be",
                TermBrightBlack = "#6c6f85", TermBrightRed = "#d20f39",
                TermBrightGreen = "#40a02b", TermBrightYellow = "#df8e1d",
                TermBrightBlue = "#1e66f5", TermBrightMagenta = "#ea76cb",
                TermBrightCyan = "#179299", TermBrightWhite = "#bcc0cc",
                TermSelection = "#ccd0da",
            },

            ["One Dark Pro"] = new()
            {
                Base = "#282c34", Mantle = "#21252b", Crust = "#181a1f",
                Surface0 = "#3e4451", Surface1 = "#4b5263", Overlay0 = "#545862",
                Subtext0 = "#5c6370", Subtext1 = "#848da0",
                Text = "#abb2bf", Accent = "#61afef",
                Green = "#98c379", Red = "#e06c75",

                TermBg = "#282c34", TermFg = "#abb2bf",
                TermCursor = "#528bff", TermCursorAccent = "#282c34",
                TermBlack = "#3e4451", TermRed = "#e06c75",
                TermGreen = "#98c379", TermYellow = "#e5c07b",
                TermBlue = "#61afef", TermMagenta = "#c678dd",
                TermCyan = "#56b6c2", TermWhite = "#abb2bf",
                TermBrightBlack = "#4b5263", TermBrightRed = "#e06c75",
                TermBrightGreen = "#98c379", TermBrightYellow = "#e5c07b",
                TermBrightBlue = "#61afef", TermBrightMagenta = "#c678dd",
                TermBrightCyan = "#56b6c2", TermBrightWhite = "#ffffff",
                TermSelection = "#3e4451",
            },

            ["Dracula"] = new()
            {
                Base = "#282a36", Mantle = "#21222c", Crust = "#191a21",
                Surface0 = "#44475a", Surface1 = "#565761", Overlay0 = "#6272a4",
                Subtext0 = "#6272a4", Subtext1 = "#8d93b0",
                Text = "#f8f8f2", Accent = "#bd93f9",
                Green = "#50fa7b", Red = "#ff5555",

                TermBg = "#282a36", TermFg = "#f8f8f2",
                TermCursor = "#f8f8f0", TermCursorAccent = "#282a36",
                TermBlack = "#21222c", TermRed = "#ff5555",
                TermGreen = "#50fa7b", TermYellow = "#f1fa8c",
                TermBlue = "#bd93f9", TermMagenta = "#ff79c6",
                TermCyan = "#8be9fd", TermWhite = "#f8f8f2",
                TermBrightBlack = "#6272a4", TermBrightRed = "#ff6e6e",
                TermBrightGreen = "#69ff94", TermBrightYellow = "#ffffa5",
                TermBrightBlue = "#d6acff", TermBrightMagenta = "#ff92df",
                TermBrightCyan = "#a4ffff", TermBrightWhite = "#ffffff",
                TermSelection = "#44475a",
            },

            ["Nord"] = new()
            {
                Base = "#2e3440", Mantle = "#272c36", Crust = "#242831",
                Surface0 = "#3b4252", Surface1 = "#434c5e", Overlay0 = "#4c566a",
                Subtext0 = "#5a6478", Subtext1 = "#7b88a1",
                Text = "#d8dee9", Accent = "#88c0d0",
                Green = "#a3be8c", Red = "#bf616a",

                TermBg = "#2e3440", TermFg = "#d8dee9",
                TermCursor = "#d8dee9", TermCursorAccent = "#2e3440",
                TermBlack = "#3b4252", TermRed = "#bf616a",
                TermGreen = "#a3be8c", TermYellow = "#ebcb8b",
                TermBlue = "#81a1c1", TermMagenta = "#b48ead",
                TermCyan = "#88c0d0", TermWhite = "#e5e9f0",
                TermBrightBlack = "#4c566a", TermBrightRed = "#bf616a",
                TermBrightGreen = "#a3be8c", TermBrightYellow = "#ebcb8b",
                TermBrightBlue = "#81a1c1", TermBrightMagenta = "#b48ead",
                TermBrightCyan = "#8fbcbb", TermBrightWhite = "#eceff4",
                TermSelection = "#434c5e",
            },

            ["Tokyo Night"] = new()
            {
                Base = "#1a1b26", Mantle = "#16161e", Crust = "#13131a",
                Surface0 = "#24283b", Surface1 = "#414868", Overlay0 = "#565f89",
                Subtext0 = "#565f89", Subtext1 = "#9aa5ce",
                Text = "#c0caf5", Accent = "#7aa2f7",
                Green = "#9ece6a", Red = "#f7768e",

                TermBg = "#1a1b26", TermFg = "#c0caf5",
                TermCursor = "#c0caf5", TermCursorAccent = "#1a1b26",
                TermBlack = "#15161e", TermRed = "#f7768e",
                TermGreen = "#9ece6a", TermYellow = "#e0af68",
                TermBlue = "#7aa2f7", TermMagenta = "#bb9af7",
                TermCyan = "#7dcfff", TermWhite = "#a9b1d6",
                TermBrightBlack = "#414868", TermBrightRed = "#f7768e",
                TermBrightGreen = "#9ece6a", TermBrightYellow = "#e0af68",
                TermBrightBlue = "#7aa2f7", TermBrightMagenta = "#bb9af7",
                TermBrightCyan = "#7dcfff", TermBrightWhite = "#c0caf5",
                TermSelection = "#364a82",
            },
        };

    public static ThemeColors GetOrDefault(string name) =>
        All.TryGetValue(name, out var t) ? t : All["Catppuccin Mocha"];
}
