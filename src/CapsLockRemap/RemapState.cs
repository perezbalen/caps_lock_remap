namespace CapsLockRemap;

// Pure transition logic; all methods are called on the input thread.
internal sealed class RemapState(Func<int, bool, bool> send)
{
    internal static readonly int[] Modifiers = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];
    private readonly bool[] physical = new bool[256];
    private readonly bool[] owned = new bool[256];
    private Configuration config = new();
    private bool held, interrupted, waitRelease;
    private long pressedAt;
    public bool Failed { get; private set; }
    public bool Held => held;
    private bool Requested(int key) => held && (key == 0xA0 && config.Shift || key == 0xA2 && config.Control || key == 0xA4 && config.Alt || key == 0x5B && config.Windows);
    private void Emit(int key, bool down)
    {
        if (!send(key, down)) Failed = true;
    }
    public void Reset(Configuration settings, bool capsDown)
    {
        held = false;
        foreach (int key in Modifiers)
        {
            if (owned[key] && !physical[key]) Emit(key, false);
            if (!Failed) owned[key] = false;
        }
        config = settings;
        waitRelease = capsDown;
        interrupted = true;
    }
    public void Seed(int key, bool down) => physical[key] = down;
    public void ClearFailure() => Failed = false;
    public bool Handle(int key, bool down, long now)
    {
        if (key == 0x14 && config.Enabled)
        {
            if (waitRelease) { if (!down) waitRelease = false; return true; }
            if (down && !held)
            {
                held = true; interrupted = false; pressedAt = now;
                foreach (int modifier in Modifiers)
                    if (Requested(modifier) && !physical[modifier]) { owned[modifier] = true; Emit(modifier, true); }
            }
            else if (!down && held)
            {
                bool tap = !interrupted && now - pressedAt <= config.TapMilliseconds;
                Reset(config, false);
                if (tap && !Failed && config.TapKey != 0) { Emit(config.TapKey, true); Emit(config.TapKey, false); }
            }
            return true;
        }
        if (held && down) interrupted = true;
        if (!Modifiers.Contains(key)) return false;
        physical[key] = down;
        // Keep the left-side logical key held when its physical owner releases it.
        if (!down && Requested(key)) { owned[key] = true; return true; }
        if (down) owned[key] = false; // physical owner takes over an injected hold
        return false;
    }
}
