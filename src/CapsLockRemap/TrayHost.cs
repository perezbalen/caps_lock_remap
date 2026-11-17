using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace CapsLockRemap;

internal sealed class TrayHost : ApplicationContext
{
    private Configuration config;
    private readonly KeyboardEngine engine;
    private readonly NotifyIcon tray;
    private readonly Icon icon = AppIcon.Load();
    private readonly ToolStripMenuItem enabled = new("Enabled");
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private SettingsForm? settings;
    private readonly bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public TrayHost()
    {
        bool first = !File.Exists(Configuration.FilePath);
        try { config = Configuration.Load(); }
        catch (Exception ex) { MessageBox.Show("Settings could not be loaded; using defaults.\n" + ex.Message, "Caps Lock Remap"); config = new(); }
        engine = new KeyboardEngine(config);
        var menu = new ContextMenuStrip();
        menu.Items.Add(enabled);
        enabled.Click += (_, _) =>
        {
            var next = config with { Enabled = !config.Enabled };
            try { next.Save(); config = next; engine.Apply(config); RefreshStatus(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot change settings"); }
        };
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Retry input handling", null, (_, _) => engine.Apply(config));
        menu.Items.Add("Restart as administrator…", null, (_, _) => Elevate());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = icon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();
        timer.Tick += (_, _) => RefreshStatus(); timer.Start();
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.SessionSwitch += SessionChanged;
        RefreshStatus();
        if (first) ShowSettings();
    }
    private void RefreshStatus()
    {
        enabled.Checked = config.Enabled;
        tray.Icon = engine.Error != null ? SystemIcons.Error : config.Enabled ? icon : SystemIcons.Information;
        tray.Text = engine.Error != null ? "Caps Lock Remap — Error: retry input handling" : !config.Enabled ? "Caps Lock Remap — Disabled" : elevated ? "Caps Lock Remap — Enabled" : "Caps Lock Remap — Enabled (not elevated)";
        enabled.ToolTipText = engine.Error ?? "Toggle Caps Lock remapping";
    }
    private void ShowSettings()
    {
        if (settings != null) { settings.Activate(); return; }
        settings = new SettingsForm(config);
        if (settings.ShowDialog() == DialogResult.OK) { config = settings.Result; engine.Apply(config); }
        settings.Dispose(); settings = null;
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) engine.Apply(config with { Enabled = false });
        else engine.Apply(config);
    }
    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) engine.Apply(config with { Enabled = false });
        else if (e.Reason == SessionSwitchReason.SessionUnlock) engine.Apply(config);
    }
    private void Elevate()
    {
        if (elevated) return;
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", Arguments = "--elevated" });
            ExitThread();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to restart as administrator"); }
    }
    protected override void ExitThreadCore()
    {
        timer.Stop(); timer.Dispose();
        SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.SessionSwitch -= SessionChanged;
        engine.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); icon.Dispose();
        base.ExitThreadCore();
    }
}
