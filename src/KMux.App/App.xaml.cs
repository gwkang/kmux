using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using KMux.Core.Models;
using KMux.Terminal.Shell;
using KMux.UI.Services;
using KMux.UI.ViewModels;
using KMux.UI.Views;

namespace KMux.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings and apply theme before first window opens
        AppSettingsService.LoadAndApplyAsync().GetAwaiter().GetResult();

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

        var vm  = new TerminalWindowViewModel(profile);
        var win = new TerminalWindow
        {
            AssetsPath  = assetsPath,  // must be set before DataContext triggers Rebuild
            DataContext = vm,
        };

        win.Show();
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

