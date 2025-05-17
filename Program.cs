using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

class Message
{
    #region dll
    [DllImport("user32.dll")]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref MSG lpMsg);
    #endregion

    public const uint WM_USER_PLAY = 0x0400 + 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }
    public static void MessageLoop()
    {
        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);

            if (msg.message == WM_USER_PLAY)
            {
                Program.playRequested = true;
                break; // Exit the loop to start playback
            }
        }
    }
}

class Program
{
    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint threadId, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    

    static uint mainThreadId;


    private static Native.HookProc mouseProc = MouseHookCallback;
    private static IntPtr mouseHookId = IntPtr.Zero;

    private static Native.HookProc hookProc = KeyboardHookCallback;
    private static IntPtr hookId = IntPtr.Zero;

    private static readonly object _sync = new();
    private static List<InputEvent> _events = new();

    private static Stopwatch stopwatch = Stopwatch.StartNew();

    #region Variables
    private static bool running = false;
    public static bool playRequested = false;
    public static bool replay = true;
    public static bool userChoosed = false;
    private static HashSet<int> pressedKeys = new HashSet<int>();
    private static long lastMouseMoveTime = 0;
    private static string filePath = "";
    #endregion


    public static void Main()
    {
        mainThreadId = GetCurrentThreadId();
        
        Directory.CreateDirectory("./SavedDataFiles");//Create the folder to hold the data files

        while (!userChoosed)
        {
            userChoosed = true;

            Console.Clear();

            Console.WriteLine("Choose 1 or 2 : Record or Read?");
            int recOrRead = int.Parse(Console.ReadLine());

            //_________________________________________
            //-----------------Record------------------
            //_________________________________________
            if (recOrRead == 1)
            {
                Console.WriteLine("Choose a file name : ");
                filePath = $"./SavedDataFiles/{Console.ReadLine()}.json";
                Console.Clear();

                //Keyboard hook
                var moduleHandle = Native.GetModuleHandle(null);
                hookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, hookProc, moduleHandle, 0);

                //Mouse hook
                mouseHookId = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, moduleHandle, 0);


                Console.WriteLine("\nPress F1 to start recording, F2 to stop, and F3 to exit.\n");
                Console.Write("|CLOSING WITH ANYTHING ELSE THAN F3 MAY RESULT IN NOT SAVING EVERYTHING| \n(Use F2 before closing with the button / alt+f4)\n");
                Message.MessageLoop();
            

            //_________________________________________
            //------------------Read-------------------
            //_________________________________________

            }else if (recOrRead == 2)
            {
                while (replay)
                {
                    replay = false;
                    Console.Clear();

                    //Ask for the file name
                    Console.WriteLine("Name of the file to read? (Don't add the extension) : ");
                    filePath = $"./SavedDataFiles/{Console.ReadLine()}.json";
                    while (!File.Exists(filePath))
                    {
                        Console.Clear();
                        Console.WriteLine("File name is wrong, or it doesn't exist. Try again : ");
                        filePath = $"./SavedDataFiles/{Console.ReadLine()}.json";
                        Console.WriteLine("Opening  " + filePath);
                    }

                    // Console.WriteLine("Enter your screen resolution (eg. 1920x1080) : ");
                    // string[] parts = Console.ReadLine().Split('x');
                    // Vector2 screenResolution = new(float.Parse(parts[0]), float.Parse(parts[1]));
                    
                    List<InputEvent> events = Player.LoadEevent(filePath);

                    //Keyboard hook setup
                    IntPtr hInstance = Native.GetModuleHandle(null);
                    IntPtr playbackHookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, PlaybackKeyboardHookCallback, hInstance, 0);

                    Console.WriteLine("Press F1 to start playback...");

                    Message.MessageLoop();//Keep the app running and intercept messages

                    Native.UnhookWindowsHookEx(playbackHookId);//Unhook

                    Player.ReplayEvents(events, 1920, 1080);//Replay

                    Console.WriteLine("File finished playing :D");

                    Console.WriteLine("Replay a file? (y or n) : ");
                    string replayChoice = Console.ReadLine();
                    if ( replayChoice == "y")
                    {
                        replay = true;

                    }else{break;}
                }

            }else
            {
                Console.Clear();
                Console.WriteLine("Wrong input....");
                Console.WriteLine("Now you have to restart. Happy?");

                Thread.Sleep(3000);

                userChoosed = false;
            }
        }
        
    }


    //Process keyboard inputs
    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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
                    Recorder.SaveEvents(snapshot, filePath);
                    
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
                Recorder.SaveEvents(snapshot, filePath);

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


    //Global keyboard hook
    private static IntPtr PlaybackKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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

                    PostThreadMessage(mainThreadId, Message.WM_USER_PLAY, UIntPtr.Zero, IntPtr.Zero);//Send message for message loop
                }
            }
        }

        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }


    //Process mouse inputs
    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
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
