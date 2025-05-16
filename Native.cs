using System.Drawing;
using System.Runtime.InteropServices;

internal static class Native
{
    #region Input record related
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    #region Declarations
    //Keyboard
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP   = 0x0101;

    //Mouse
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    #endregion
    #region  dll    
    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public Point pt;
        public int mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    #endregion

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBOARDHOOK
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }
    #endregion Input record related

    #region Play related
    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public const int MAPVK_VK_TO_VSC = 0;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    #region Declarations
    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;

    public const int KEYEVENTF_KEYUP = 0x0002;
    public const int MOUSEEVENTF_MOVE = 0x0001;
    public const int MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const int MOUSEEVENTF_LEFTUP = 0x0004;
    public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const int MOUSEEVENTF_RIGHTUP = 0x0010;

    #endregion
    #region Structs
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion union;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public UIntPtr dwExtraInfo;
    }
    #endregion
    #endregion Play related


    public static void SendRelativeMouseMove(int deltaX, int deltaY)
    {
        mouse_event(MOUSEEVENTF_MOVE, deltaX, deltaY, 0, UIntPtr.Zero);
    }

    public static void SendKey(ushort keyCode, bool keyUp = false)
    {
        Native.INPUT[] inputs = new Native.INPUT[1];

        //Convert virtual key to scan code
        ushort scanCode = (ushort)Native.MapVirtualKey(keyCode, Native.MAPVK_VK_TO_VSC);

        inputs[0].type = Native.INPUT_KEYBOARD;
        inputs[0].union.ki.wVk = 0;//Use scan code
        inputs[0].union.ki.wScan = scanCode;
        inputs[0].union.ki.dwFlags = (int)Native.KEYEVENTF_SCANCODE | (keyUp ? Native.KEYEVENTF_KEYUP : 0);
        inputs[0].union.ki.time = 0;
        inputs[0].union.ki.dwExtraInfo = UIntPtr.Zero;

        Native.SendInput(1, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
    }

    public static void MoveMouse(int targetX, int targetY, int screenWidth, int screenHeight, int steps = 20, int duration = 10)
    {
        Native.POINT startPoint;
        Native.GetCursorPos(out startPoint);

        int deltaX = targetX - startPoint.X;
        int deltaY = targetY - startPoint.Y;

        double previousX = 0;
        double previousY = 0;

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;

            //Ease out
            double ease = t * t * (3 - 2 * t);

            double currentX = deltaX * ease;
            double currentY = deltaY * ease;

            int moveX = (int)Math.Round(currentX - previousX);
            int moveY = (int)Math.Round(currentY - previousY);

            previousX = currentX;
            previousY = currentY;

            SendAbsoluteMouseMove(startPoint.X + (int)Math.Round(currentX), startPoint.Y + (int)Math.Round(currentY), screenWidth, screenHeight);

            Thread.Sleep(duration / steps);
        }
    }

    private static void SendAbsoluteMouseMove(int x, int y, int screenWidth, int screenHeight)
    {
        Native.INPUT[] inputs = new Native.INPUT[1];

        inputs[0].type = Native.INPUT_MOUSE;
        inputs[0].union.mi.dx = x * 65535 / screenWidth;
        inputs[0].union.mi.dy = y * 65535 / screenHeight;
        inputs[0].union.mi.mouseData = 0;
        inputs[0].union.mi.dwFlags = Native.MOUSEEVENTF_MOVE | Native.MOUSEEVENTF_ABSOLUTE;
        inputs[0].union.mi.time = 0;
        inputs[0].union.mi.dwExtraInfo = UIntPtr.Zero;

        Native.SendInput(1, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
    }



    public static void MouseClick(string button, bool isDown)
    {
        int flag = button switch
        {
            "Left" => isDown ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP,
            "Right" => isDown ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP,
            _ => 0
        };

        Native.INPUT[] inputs = new Native.INPUT[1];

        inputs[0].type = Native.INPUT_MOUSE;
        inputs[0].union.mi.dx = 0;
        inputs[0].union.mi.dy = 0;
        inputs[0].union.mi.mouseData = 0;
        inputs[0].union.mi.dwFlags = flag;
        inputs[0].union.mi.time = 0;
        inputs[0].union.mi.dwExtraInfo = UIntPtr.Zero;

        Native.SendInput(1, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
    }

}