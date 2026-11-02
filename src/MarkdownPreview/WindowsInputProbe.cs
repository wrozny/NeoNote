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

    /// <summary>Loads a keyboard layout for a controlled text-input regression test.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadKeyboardLayout(string name, uint flags);

    /// <summary>Reads the current thread keyboard layout so a test can restore it.</summary>
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    /// <summary>Changes only the test thread's active keyboard layout.</summary>
    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);

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

    /// <summary>Drags a captured splitter beyond its owner's left or right edge before releasing the mouse.</summary>
    public static async Task DragOutsideAsync(FrameworkElement divider, bool right)
    {
        var owner = Window.GetWindow(divider);
        owner.Activate();
        var start = divider.PointToScreen(new Point(divider.ActualWidth / 2, divider.ActualHeight / 2));
        if (!SetCursorPos((int)start.X, (int)start.Y)) throw new System.ComponentModel.Win32Exception();
        Send([new Input { Data = new InputData { Mouse = new MouseInput { Flags = 0x0002 } } }]);
        try
        {
            await Task.Delay(100);
            var outside = owner.PointToScreen(new Point(right ? owner.ActualWidth + 40 : -40, divider.TranslatePoint(new Point(0, divider.ActualHeight / 2), owner).Y));
            if (!SetCursorPos((int)outside.X, (int)outside.Y)) throw new System.ComponentModel.Win32Exception();
            await Task.Delay(200);
        }
        finally { Send([new Input { Data = new InputData { Mouse = new MouseInput { Flags = 0x0004 } } }]); }
        await Task.Delay(200);
    }

    /// <summary>Types ś with real AltGr+S on the Polish Programmer layout and restores the prior layout.</summary>
    public static async Task TypePolishSAsync(FrameworkElement target)
    {
        var previous = GetKeyboardLayout(0);
        var polish = LoadKeyboardLayout("00000415", 0);
        if (polish == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        ActivateKeyboardLayout(polish, 0); target.Focus();
        var control = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0xA2 } } };
        var alt = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0xA5, Flags = 1 } } };
        var letter = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0x53 } } };
        try
        {
            Send([control, alt]); await Task.Delay(100);
            Send([letter]); letter.Data.Keyboard.Flags = 2; Send([letter]);
            await Task.Delay(150);
        }
        finally
        {
            alt.Data.Keyboard.Flags = 3; control.Data.Keyboard.Flags = 2; Send([alt, control]);
            ActivateKeyboardLayout(previous, 0);
        }
        await Task.Delay(100);
    }
}
