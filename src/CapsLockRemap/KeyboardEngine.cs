using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapsLockRemap;

internal sealed class KeyboardEngine : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly RemapState state = new(Native.Send);
    private readonly Native.HookProc callback;
    private Control? dispatcher;
    private ApplicationContext? context;
    private nint hook;
    private Configuration config;
    public string? Error { get; private set; }
    public KeyboardEngine(Configuration config)
    {
        this.config = config;
        callback = Callback;
        thread = new Thread(Run) { IsBackground = true, Name = "Caps Lock input" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Input thread did not initialize.");
    }
    private void Run()
    {
        using var control = new Control();
        dispatcher = control;
        _ = control.Handle;
        context = new ApplicationContext();
        try { Repair(); } catch (Exception ex) { Error = ex.Message; }
        ready.Set();
        Application.Run(context);
        state.Reset(config with { Enabled = false }, false);
        if (hook != 0) Native.UnhookWindowsHookEx(hook);
        context.Dispose();
    }
    private void Repair()
    {
        state.ClearFailure();
        state.Reset(config, (Native.GetAsyncKeyState(0x14) & 0x8000) != 0);
        if (state.Failed) throw new InvalidOperationException("Unable to release injected modifiers.");
        foreach (int key in RemapState.Modifiers) state.Seed(key, (Native.GetAsyncKeyState(key) & 0x8000) != 0);
        if (hook != 0) { Native.UnhookWindowsHookEx(hook); hook = 0; }
        if (config.Enabled)
        {
            hook = Native.SetWindowsHookExW(13, callback, Native.GetModuleHandleW(null), 0);
            if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((Native.GetKeyState(0x14) & 1) != 0 && (!Native.Send(0x14, true) || !Native.Send(0x14, false)))
                throw new InvalidOperationException("Unable to turn Caps Lock off.");
        }
        Error = null;
    }
    private nint Callback(int code, nuint message, nint data)
    {
        if (code < 0) return Native.CallNextHookEx(hook, code, message, data);
        try
        {
            var input = Marshal.PtrToStructure<Native.HookData>(data);
            if ((input.Flags & 0x10) == 0)
            {
                bool down = message is 0x100 or 0x104;
                bool suppress = state.Handle((int)input.Key, down, Environment.TickCount64);
                if (state.Failed) Error = "Input injection failed. Use Retry input handling; elevated apps require administrator privileges.";
                if (suppress) return 1;
            }
        }
        catch { Error = "Input handler failed. Use Retry input handling."; }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void Apply(Configuration settings) => dispatcher!.Invoke(() =>
    {
        config = settings;
        try { Repair(); } catch (Exception ex) { Error = ex.Message; }
    });
    public void Dispose()
    {
        if (dispatcher is { IsDisposed: false }) dispatcher.BeginInvoke(() => context?.ExitThread());
        if (!thread.Join(TimeSpan.FromSeconds(3))) throw new TimeoutException("Input thread did not stop cleanly.");
        ready.Dispose();
    }
}
