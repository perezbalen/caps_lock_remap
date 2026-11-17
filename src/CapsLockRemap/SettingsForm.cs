namespace CapsLockRemap;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox ctrl = new() { Text = "&Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "&Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "&Shift", AutoSize = true };
    private readonly CheckBox win = new() { Text = "&Windows", AutoSize = true };
    private readonly ComboBox tap = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly NumericUpDown timing = new() { Minimum = 50, Maximum = 1000, Increment = 25, Width = 100 };
    public Configuration Result { get; private set; }
    public SettingsForm(Configuration config)
    {
        Result = config;
        Text = "Caps Lock Remap — Settings"; Icon = AppIcon.Load();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 330); MinimumSize = new Size(460, 330);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18), AutoScroll = true };
        var modifiers = new FlowLayoutPanel { AutoSize = true };
        modifiers.Controls.AddRange([ctrl, alt, shift, win]);
        ctrl.Checked = config.Control; alt.Checked = config.Alt; shift.Checked = config.Shift; win.Checked = config.Windows;
        // An unbound list keeps selection stable when the form's binding context is created.
        tap.DisplayMember = "Value";
        foreach (var choice in Configuration.TapChoices) tap.Items.Add(choice);
        tap.SelectedItem = new KeyValuePair<int, string>(config.TapKey, Configuration.TapChoices[config.TapKey]);
        timing.Value = config.TapMilliseconds;
        layout.Controls.Add(new Label { Text = "Hold Caps Lock to press:", AutoSize = true });
        layout.Controls.Add(modifiers);
        layout.Controls.Add(new Label { Text = "Tap action (cancelled by another key):", AutoSize = true }); layout.Controls.Add(tap);
        layout.Controls.Add(new Label { Text = "Maximum tap duration (milliseconds):", AutoSize = true }); layout.Controls.Add(timing);
        layout.Controls.Add(new Label { Text = "Closing this window leaves the app in the tray.", AutoSize = true, Margin = new Padding(0, 14, 0, 14) });
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        save.Click += (_, _) =>
        {
            Result = config with { Control = ctrl.Checked, Alt = alt.Checked, Shift = shift.Checked, Windows = win.Checked, TapKey = ((KeyValuePair<int, string>)tap.SelectedItem!).Key, TapMilliseconds = (int)timing.Value };
            try { Result.Save(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot save settings", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        buttons.Controls.AddRange([save, cancel]); layout.Controls.Add(buttons); Controls.Add(layout);
        AcceptButton = save; CancelButton = cancel;
    }
}
