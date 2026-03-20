using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using KMux.Core.Models;
using KMux.Session;
using KMux.Terminal.Shell;
using KMux.UI.Services;
using KMux.UI.ViewModels;
using KMux.UI.Views;

namespace KMux.App;

public partial class App : Application
{
    private readonly WorkspaceStore _workspaceStore = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings and apply theme before first window opens
        await AppSettingsService.LoadAndApplyAsync();

        // Detect best available shell
        var profiles = ShellProfileDetector.DetectProfiles();
        var profile  = profiles.FirstOrDefault(p => p.Name == "PowerShell")
                    ?? profiles.FirstOrDefault()
                    ?? ShellProfile.Cmd;

        // Resolve assets path (xterm.js etc.)
        var assetsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets");

        // Register Claude Code hooks so pane activity is visible in the header
        RegisterClaudeHooks(assetsPath);

        // Restore last workspace if the setting is enabled
        if (AppSettingsService.Current.RestoreOnStartup)
        {
            var workspace = await _workspaceStore.LoadAsync();
            if (workspace?.Windows?.Count > 0)
            {
                RestoreWorkspace(workspace, profile, assetsPath);
                return;
            }
        }

        var vm  = new TerminalWindowViewModel(profile);
        var win = new TerminalWindow
        {
            AssetsPath  = assetsPath,  // must be set before DataContext triggers Rebuild
            DataContext = vm,
        };

        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SaveWorkspace();
        base.OnExit(e);
    }

    private void SaveWorkspace()
    {
        try
        {
            var terminalWindows = Windows.OfType<TerminalWindow>().ToList();
            if (terminalWindows.Count == 0) return;

            var windowLayouts = terminalWindows
                .Select(win => (win.DataContext as TerminalWindowViewModel)?.CaptureLayout(win))
                .Where(l => l is not null)
                .Cast<WindowLayout>()
                .ToList();

            if (windowLayouts.Count == 0) return;

            _workspaceStore.Save(new KMux.Core.Models.Session
            {
                Name    = "Last Workspace",
                Windows = windowLayouts
            });
        }
        catch { /* Don't crash shutdown if save fails */ }
    }

    private void RestoreWorkspace(KMux.Core.Models.Session workspace, ShellProfile defaultProfile, string assetsPath)
    {
        foreach (var winLayout in workspace.Windows)
        {
            var vm  = new TerminalWindowViewModel(profile: defaultProfile, restore: winLayout);
            var win = new TerminalWindow { AssetsPath = assetsPath, DataContext = vm };

            if (winLayout.Width > 100 && winLayout.Height > 100)
            {
                win.Width  = winLayout.Width;
                win.Height = winLayout.Height;
            }
            win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            win.Left = winLayout.Left;
            win.Top  = winLayout.Top;

            win.Show();
        }
    }

    private static void RegisterClaudeHooks(string assetsPath)
    {
        try
        {
            var hookScript = Path.Combine(assetsPath, "hooks", "kmux-activity-hook.sh")
                                 .Replace('\\', '/');
            var command = $"bash \"{hookScript}\"";

            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json");

            JsonNode root;
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                root = JsonNode.Parse(json) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            }

            var hooks   = (root["hooks"] as JsonObject) ?? new JsonObject();
            bool changed = false;

            foreach (var eventName in new[] { "PreToolUse", "PostToolUse", "PostToolUseFailure" })
            {
                if (!IsHookRegistered(hooks, eventName, command))
                {
                    AddHook(hooks, eventName, command);
                    changed = true;
                }
            }

            if (changed)
            {
                root["hooks"] = hooks;
                File.WriteAllText(settingsPath, root.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { /* Don't crash startup if hook registration fails */ }
    }

    private static bool IsHookRegistered(JsonObject hooks, string eventName, string command)
    {
        if (hooks[eventName] is not JsonArray eventHooks) return false;
        foreach (var item in eventHooks)
        {
            if (item?["hooks"] is not JsonArray subHooks) continue;
            foreach (var hook in subHooks)
            {
                if (hook?["command"]?.GetValue<string>() == command) return true;
            }
        }
        return false;
    }

    private static void AddHook(JsonObject hooks, string eventName, string command)
    {
        if (hooks[eventName] is not JsonArray eventHooks)
        {
            eventHooks = new JsonArray();
            hooks[eventName] = eventHooks;
        }
        eventHooks.Add(new JsonObject
        {
            ["matcher"] = "",
            ["hooks"]   = new JsonArray
            {
                new JsonObject
                {
                    ["type"]    = "command",
                    ["command"] = command
                }
            }
        });
    }
}

