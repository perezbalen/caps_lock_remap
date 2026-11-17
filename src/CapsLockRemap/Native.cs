using System.Runtime.InteropServices;

namespace CapsLockRemap;

internal static class Native
{
    public static readonly nuint Tag = 0x434C524D;
    public delegate nint HookProc(int code, nuint message, nint data);
    [StructLayout(LayoutKind.Sequential)] public struct HookData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public Keyboard Keyboard; [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] public struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct Input { public uint Type; public InputUnion Data; }
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetWindowsHookExW(int kind, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nuint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern short GetKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] inputs, int size);
    public static bool Send(int key, bool down)
    {
        var input = new Input { Type = 1, Data = new InputUnion { Keyboard = new Keyboard { Key = (ushort)key, Flags = (down ? 0u : 2u) | (key is 0x5B or 0x5C or 0xA3 or 0xA5 ? 1u : 0u), Extra = Tag } } };
        return SendInput(1, [input], Marshal.SizeOf<Input>()) == 1;
    }
}
