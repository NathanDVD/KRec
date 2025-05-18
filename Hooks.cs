using System.Diagnostics;
using System.Runtime.InteropServices;

class Hooks
{
    #region DLL imports
    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint threadId, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    #endregion

    public static Native.HookProc mouseProc = MouseHookCallback;
    public static IntPtr mouseHookId = IntPtr.Zero;

    public static Native.HookProc hookProc = KeyboardHookCallback;
    public static IntPtr hookId = IntPtr.Zero;


    #region Variables
    private static readonly object _sync = new();
    private static List<InputEvent> _events = new();

    private static Stopwatch stopwatch = Stopwatch.StartNew();

    private static bool running = false;

    private static HashSet<int> pressedKeys = new HashSet<int>();
    private static long lastMouseMoveTime = 0;
    #endregion

    ///<summary>
    /// Process keyboard inputs for the recording hotkeys
    ///</summary>
    public static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) 
            return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
            
        int msg = wParam.ToInt32();
        if (msg != Native.WM_KEYDOWN && msg != Native.WM_KEYUP)
            return Native.CallNextHookEx(hookId, nCode, wParam, lParam);

        var kb = Marshal.PtrToStructure<Native.KEYBOARDHOOK>(lParam);
        int vk = kb.vkCode;
        string type = msg == Native.WM_KEYDOWN ? "KeyDown" : "KeyUp";


        if (msg == Native.WM_KEYDOWN)
        {
            if (vk == 112)//F1 start, continue
            {
                if (!running)
                {
                    running = true;
                    stopwatch.Restart();//Reset stopwatch
                    Console.WriteLine("Recording started");
                }
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);//Don't log F1
            }
            else if (vk == 113)//F2, pause/stop
            {
                if (running)
                {
                    running = false;
                    Console.WriteLine("Recording stopped");
                    Console.WriteLine("Saving...");

                    List<InputEvent> snapshot;
                    lock (_sync)//Locks the object to avoid conflict
                    {
                        snapshot = new List<InputEvent>(_events);
                    }
                    Recorder.SaveEvents(snapshot, Program.filePath);
                    
                }
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);//Don't log F2
            }
            else if (vk == 114)//F3, exit
            {
                Console.WriteLine($"Saving data...");

                List<InputEvent> snapshot;
                lock (_sync)
                {
                    snapshot = new List<InputEvent>(_events);
                }
                Recorder.SaveEvents(snapshot, Program.filePath);

                Native.UnhookWindowsHookEx(hookId);
                Native.UnhookWindowsHookEx(mouseHookId);

                Thread.Sleep(500);//Allow write to finish

                PostQuitMessage(0);
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);//Don't log F3
            }
        }
        

        if (running)
        {
            if (type == "KeyDown" && !pressedKeys.Contains(vk))
            {
                pressedKeys.Add(vk);
                var (ts, et, formatted) = Recorder.FormatData((int)stopwatch.ElapsedMilliseconds, type, vk);
                var ie = new InputEvent { TimeStamp = ts, EventType = et, Data = formatted };

                lock (_sync) { _events.Add(ie); }
                Console.WriteLine($"[{ts}] {formatted}");
            }
            else if (type == "KeyUp" && pressedKeys.Contains(vk))
            {
                pressedKeys.Remove(vk);
                var (ts, et, formatted) = Recorder.FormatData((int)stopwatch.ElapsedMilliseconds, type, vk);
                var ie = new InputEvent { TimeStamp = ts, EventType = et, Data = formatted };

                lock (_sync) { _events.Add(ie); }
                Console.WriteLine($"[{ts}] {formatted}");
            }
        }

        return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
    }


    ///<summary>
    /// Keyboard hook handling for playback
    ///</summary>
    public static IntPtr PlaybackKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == Native.WM_KEYDOWN)
            {
                var kb = Marshal.PtrToStructure<Native.KEYBOARDHOOK>(lParam);
                int vk = kb.vkCode;
                if (vk == 112) // F1
                {
                    Console.WriteLine("F1 Pressed - Starting playback");

                    PostThreadMessage(Program.mainThreadId, Message.WM_USER_PLAY, UIntPtr.Zero, IntPtr.Zero);//Send message for message loop
                }
            }
        }

        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }


    ///<summary>
    /// Process mouse inputs for recording
    ///</summary>
    public static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && running)
        {
            var ms = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);//Marshalling lets you convert native code to c# format
            string? eventType = wParam.ToInt32() switch
            {
                Native.WM_MOUSEMOVE => "MouseMove",
                Native.WM_LBUTTONDOWN => "LDown",
                Native.WM_LBUTTONUP => "LUp",
                Native.WM_RBUTTONDOWN => "RDown",
                Native.WM_RBUTTONUP => "RUp",
                _ => null
            };

            if (eventType != null)
            {
                var coords = (ms.pt.X, ms.pt.Y);
                
                int currentTime = (int)stopwatch.ElapsedMilliseconds;

                if (eventType == "MouseMove")
                {
                    const int mouseMoveInterval = 20;//Time in ms

                    if (currentTime - lastMouseMoveTime < mouseMoveInterval)
                        return Native.CallNextHookEx(mouseHookId, nCode, wParam, lParam);

                    lastMouseMoveTime = currentTime;
                }

                var (ts, type, formatted) = Recorder.FormatData((int)stopwatch.ElapsedMilliseconds, eventType, coords);

                var inputEvent = new InputEvent {TimeStamp = ts, EventType = type, Data = formatted};

                lock(_sync) {
                _events.Add(inputEvent);
                }

                Console.WriteLine($"[{ts}] {formatted}");
            }
        }

        return Native.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
    }
}