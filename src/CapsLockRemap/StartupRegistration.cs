using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace CapsLockRemap;

internal sealed record StartupStatus(bool Exists, bool Current, string? Path);

internal static class StartupRegistration
{
    private static string UserId => WindowsIdentity.GetCurrent().User!.Value;
    private static string TaskName => "CapsLockRemap.SignIn." + UserId;
    internal static bool SameUser(string? saved, string sid)
    {
        if (string.IsNullOrWhiteSpace(saved)) return false;
        if (string.Equals(saved, sid, StringComparison.OrdinalIgnoreCase)) return true;
        try { return ((SecurityIdentifier)new NTAccount(saved).Translate(typeof(SecurityIdentifier))).Value == sid; }
        catch (IdentityNotMappedException) { return false; }
    }
    // COM interop can translate ERROR_FILE_NOT_FOUND into FileNotFoundException,
    // not just COMException. Match the HRESULT only at task lookup/deletion.
    internal static T MissingTaskOrDefault<T>(Func<T> operation, T fallback)
    {
        try { return operation(); }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070002)) { return fallback; }
    }
    public static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate application.");
    public static string BuildXml(string user, string executable)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object content) => new(ns + name, content);
        return new XElement(ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "Caps Lock Remap elevated sign-in startup")),
            E("Triggers", E("LogonTrigger", new object[] { E("Enabled", true), E("UserId", user) })),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "User"), E("UserId", user), E("LogonType", "InteractiveToken"), E("RunLevel", "HighestAvailable"))),
            E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", false), E("StopIfGoingOnBatteries", false), E("StartWhenAvailable", true), E("Enabled", true), E("ExecutionTimeLimit", "PT0S") }),
            new XElement(ns + "Actions", new XAttribute("Context", "User"), E("Exec", new object[] { E("Command", executable), E("Arguments", "--startup"), E("WorkingDirectory", System.IO.Path.GetDirectoryName(executable)!) }))).ToString();
    }
    private static T WithFolder<T>(Func<dynamic, T> action)
    {
        object service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        object? folder = null;
        try
        {
            ((dynamic)service).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            return action(folder);
        }
        finally
        {
            if (folder != null) Marshal.FinalReleaseComObject(folder);
            Marshal.FinalReleaseComObject(service);
        }
    }
    public static StartupStatus Read() => WithFolder<StartupStatus>(folder =>
    {
        object? task = MissingTaskOrDefault<object?>(() => folder.GetTask(TaskName), null);
        if (task == null) return new(false, false, null);
        try
        {
            var xml = XDocument.Parse((string)((dynamic)task).Xml);
            XNamespace ns = xml.Root!.Name.Namespace;
            var root = xml.Root;
            string? path = root.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Command")?.Value;
            bool current = string.Equals(path, Executable, StringComparison.OrdinalIgnoreCase)
                && root.Element(ns + "Principals")?.Element(ns + "Principal")?.Element(ns + "RunLevel")?.Value == "HighestAvailable"
                && SameUser(root.Element(ns + "Principals")?.Element(ns + "Principal")?.Element(ns + "UserId")?.Value, UserId)
                && root.Element(ns + "Principals")?.Element(ns + "Principal")?.Element(ns + "LogonType")?.Value == "InteractiveToken"
                && SameUser(root.Element(ns + "Triggers")?.Element(ns + "LogonTrigger")?.Element(ns + "UserId")?.Value, UserId)
                && root.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Arguments")?.Value == "--startup"
                && root.Element(ns + "Settings")?.Element(ns + "DisallowStartIfOnBatteries")?.Value == "false"
                && root.Element(ns + "Settings")?.Element(ns + "StopIfGoingOnBatteries")?.Value == "false"
                && root.Element(ns + "Settings")?.Element(ns + "ExecutionTimeLimit")?.Value == "PT0S"
                && (bool)((dynamic)task).Enabled;
            return new(true, current, path);
        }
        finally { Marshal.FinalReleaseComObject(task); }
    });
    public static void Change(bool enable) => WithFolder<bool>(folder =>
    {
        if (enable)
        {
            object task = folder.RegisterTask(TaskName, BuildXml(UserId, Executable), 6, UserId, null, 3, null);
            Marshal.FinalReleaseComObject(task);
        }
        else
        {
            MissingTaskOrDefault(() => { folder.DeleteTask(TaskName, 0); return true; }, false);
        }
        return true;
    });
    public static void ChangeWithElevation(bool enable)
    {
        if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) { Change(enable); return; }
        using var process = Process.Start(new ProcessStartInfo(Executable)
        {
            UseShellExecute = true, Verb = "runas",
            Arguments = $"--startup-{(enable ? "enable" : "disable")} {UserId}"
        }) ?? throw new InvalidOperationException("Startup setup could not be launched.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Startup setup failed or was cancelled. The checkbox has not been enabled.");
    }
    public static bool HandleCommand(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("--startup-enable" or "--startup-disable")) return false;
        try
        {
            if (args.Length != 2 || args[1] != UserId) throw new InvalidOperationException("Use administrator approval for the same Windows account that runs the app.");
            Change(args[0] == "--startup-enable");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Startup setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.ExitCode = 1; }
        return true;
    }
}
