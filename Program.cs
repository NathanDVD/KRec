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
    public static uint mainThreadId;

    #region Variables
    public static bool playRequested = false;
    public static bool replay = true;
    public static bool userChoosed = false;
    public static string filePath = "";
    #endregion


    public static void Main()
    {
        mainThreadId = Hooks.GetCurrentThreadId();
        
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
                filePath = ShowFileDialog(false);

                //Keyboard hook
                var moduleHandle = Native.GetModuleHandle(null);
                Hooks.hookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, Hooks.hookProc, moduleHandle, 0);

                //Mouse hook
                Hooks.mouseHookId = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, Hooks.mouseProc, moduleHandle, 0);


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
                    Console.WriteLine("Make a file (remember you need a .json)");

                    //Ask for the file name
                    filePath = ShowFileDialog(true);

                    while (!File.Exists(filePath))
                    {
                        Console.Clear();
                        Console.WriteLine("File name is wrong, or it doesn't exist. Try again.");

                        filePath = ShowFileDialog(true);
                    }
                    
                    //Screen res is needed for the mouse movements to work properly
                    int screenWidth = Native.GetSystemMetrics(Native.SM_CXSCREEN);
                    int screenHeight = Native.GetSystemMetrics(Native.SM_CYSCREEN);
                    
                    List<InputEvent> events = Player.LoadEevent(filePath);

                    //Keyboard hook setup
                    IntPtr hInstance = Native.GetModuleHandle(null);
                    IntPtr playbackHookId = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, Hooks.PlaybackKeyboardHookCallback, hInstance, 0);

                    Console.WriteLine("Press F1 to start playback...");

                    Message.MessageLoop();//Keep the app running and intercept messages

                    Native.UnhookWindowsHookEx(playbackHookId);//Unhook

                    Player.ReplayEvents(events, screenWidth, screenHeight);//Replay

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
    
    ///<summary>
    /// Helper function that prompts the file dialog to choose files
    /// Uses powershell commands to use some WinForm methods
    ///</summary>
    public static string ShowFileDialog(bool isChoosingFile)
    {
        string args = "-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; $f = New-Object Windows.Forms.OpenFileDialog; $f.Filter = 'JSON files (*.json)|*.json'; $f.ShowDialog() | Out-Null; Write-Output $f.FileName\"";
        if (!isChoosingFile)
        {
            args = "-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; $f = New-Object Windows.Forms.SaveFileDialog; $f.Filter = 'JSON files (*.json)|*.json|All files (*.*)|*.*'; $f.DefaultExt = 'json'; $f.Title = 'Save your file as'; $f.ShowDialog() | Out-Null; Write-Output $f.FileName\"";
        }

        string script = "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.OpenFileDialog]::new().ShowDialog()";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using var process = Process.Start(psi);
        string result = process.StandardOutput.ReadToEnd().Trim();
        if (string.IsNullOrEmpty(result)) throw new Exception("No file choosed.");

        return result;
    }
}