using CapsLockRemap;

int passed = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
(RemapState State, List<(int Key, bool Down)> Events) Create(Configuration? config = null)
{
    var events = new List<(int, bool)>();
    var state = new RemapState((key, down) => { events.Add((key, down)); return true; });
    state.Reset(config ?? new(), false);
    return (state, events);
}
Test("repeat suppression and balanced chord", () => {
    var (s, e) = Create(); s.Handle(20, true, 0); s.Handle(20, true, 10); s.Handle(83, true, 20); s.Handle(20, false, 30);
    Assert(e.Count == 6 && e.Count(x => x.Down) == 3 && !s.Held);
});
Test("tap emits after modifiers release", () => {
    var (s, e) = Create(new() { TapKey = 27 }); s.Handle(20, true, 0); s.Handle(20, false, 100);
    Assert(e.Count == 8 && e[^2] == (27, true) && e[^1] == (27, false) && !e[5].Down);
});
Test("other key cancels tap", () => {
    var (s, e) = Create(new() { TapKey = 27 }); s.Handle(20, true, 0); s.Handle(65, true, 10); s.Handle(20, false, 20); Assert(e.All(x => x.Key != 27));
});
Test("long hold cancels tap", () => {
    var (s, e) = Create(new() { TapKey = 27 }); s.Handle(20, true, 0); s.Handle(20, false, 201); Assert(e.All(x => x.Key != 27));
});
Test("physical Shift survives release", () => {
    var (s, e) = Create(); s.Handle(0xA0, true, 0); s.Handle(20, true, 10); s.Handle(20, false, 20); Assert(e.All(x => x.Key != 0xA0));
});
Test("physical release during hold is suppressed and later released", () => {
    var (s, e) = Create(); s.Handle(0xA0, true, 0); s.Handle(20, true, 10); Assert(s.Handle(0xA0, false, 20)); s.Handle(20, false, 30);
    Assert(e.Count(x => x.Key == 0xA0 && !x.Down) == 1);
});
Test("physical press takes over injected modifier", () => {
    var (s, e) = Create(); s.Handle(20, true, 0); s.Handle(0xA0, true, 10); s.Handle(20, false, 20); Assert(!e.Contains((0xA0, false)));
});
Test("right Shift is independent", () => {
    var (s, e) = Create(); s.Handle(0xA1, true, 0); s.Handle(20, true, 10); s.Handle(20, false, 20); Assert(!e.Any(x => x.Key == 0xA1) && e.Contains((0xA0, false)));
});
Test("reset cancels tap and waits for release", () => {
    var (s, e) = Create(new() { TapKey = 27 }); s.Handle(20, true, 0); s.Reset(new() { TapKey = 27 }, true); int count = e.Count;
    s.Handle(20, true, 10); s.Handle(20, false, 20); Assert(e.Count == count); s.Handle(20, true, 30); Assert(s.Held);
});
Test("injection failure prevents tap", () => {
    var s = new RemapState((_, _) => false); s.Reset(new() { TapKey = 27 }, false); s.Handle(20, true, 0); Assert(s.Failed); s.Handle(20, false, 10); Assert(s.Failed);
});
Test("startup XML scopes elevation and trigger to user, allows battery and no timeout", () => {
    var xml = System.Xml.Linq.XDocument.Parse(StartupRegistration.BuildXml("S-1-5-21-test", @"C:\Apps & Tools\CapsLockRemap.exe"));
    var root = xml.Root!; var ns = root.Name.Namespace;
    string? Value(string parent, string child) => root.Element(ns + parent)?.Element(ns + child)?.Value;
    Assert(Value("Settings", "DisallowStartIfOnBatteries") == "false");
    Assert(Value("Settings", "StopIfGoingOnBatteries") == "false");
    Assert(Value("Settings", "ExecutionTimeLimit") == "PT0S");
    var principal = root.Element(ns + "Principals")!.Element(ns + "Principal")!;
    Assert(principal.Element(ns + "UserId")!.Value == "S-1-5-21-test");
    Assert(principal.Element(ns + "RunLevel")!.Value == "HighestAvailable");
    Assert(principal.Element(ns + "LogonType")!.Value == "InteractiveToken");
    Assert(root.Element(ns + "Triggers")!.Element(ns + "LogonTrigger")!.Element(ns + "UserId")!.Value == "S-1-5-21-test");
    Assert(root.Element(ns + "Actions")!.Element(ns + "Exec")!.Element(ns + "Command")!.Value == @"C:\Apps & Tools\CapsLockRemap.exe");
});
Console.WriteLine($"{passed} checks passed.");
