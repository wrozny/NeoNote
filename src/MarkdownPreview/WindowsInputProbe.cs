using System.Runtime.InteropServices;
using System.Windows;

namespace MarkdownPreview;

/// <summary>Sends real wheel input during Windows-only smoke tests rather than invoking zoom commands directly.</summary>
internal static class WindowsInputProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputData Data; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public IntPtr Extra; }

    /// <summary>Places the pointer inside the tested pane.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    /// <summary>Submits keyboard and wheel events to the Windows input queue.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    /// <summary>Sends wheel input to an actual screen location and optionally holds Control for the gesture.</summary>
    public static async Task WheelAsync(FrameworkElement pane, int delta, bool control)
    {
        var point = pane.PointToScreen(new Point(pane.ActualWidth / 2, pane.ActualHeight / 2));
        if (!SetCursorPos((int)point.X, (int)point.Y)) throw new System.ComponentModel.Win32Exception();
        await Task.Delay(100);
        var wheel = new Input { Type = 0, Data = new InputData { Mouse = new MouseInput { Data = unchecked((uint)delta), Flags = 0x0800 } } };
        var down = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0x11 } } };
        var up = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0x11, Flags = 0x0002 } } };
        if (control) { Send([down]); await Task.Delay(100); }
        try { Send([wheel]); await Task.Delay(300); }
        finally { if (control) Send([up]); }
    }

    /// <summary>Submits one verified input batch and reports failures from Windows.</summary>
    private static void Send(Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new System.ComponentModel.Win32Exception();
    }
}
