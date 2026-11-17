namespace CapsLockRemap;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (StartupRegistration.HandleCommand(Environment.GetCommandLineArgs().Skip(1).ToArray())) return;
        using var instance = new Mutex(false, @"Local\CapsLockRemap.Application");
        bool acquired;
        try { acquired = instance.WaitOne(Environment.GetCommandLineArgs().Contains("--elevated") ? 5000 : 0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            if (!Environment.GetCommandLineArgs().Contains("--startup"))
                MessageBox.Show("Caps Lock Remap is already running. Open Settings from its tray icon.", "Caps Lock Remap");
            return;
        }
        try
        {
        Application.Run(new TrayHost());
        }
        finally { instance.ReleaseMutex(); }
    }    
}
