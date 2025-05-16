using System.Text.Json;

public class Player
{
    public static List<InputEvent> LoadEevent(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<InputEvent>>(json) ?? new();//Return the list, or a new empty one
    }

    public static void ReplayEvents(List<InputEvent> events, int screenWidth, int screenHeight)
    {
        if (events.Count == 0) return;

        int previousTime = events[0].TimeStamp;

        foreach (var ev in events)
        {
            int delay = ev.TimeStamp - previousTime;
            if (delay > 0) Thread.Sleep(delay);
            previousTime = ev.TimeStamp;

            switch (ev.EventType)
            {
                case "KeyDown":
                    Native.SendKey(ushort.Parse(ev.Data), false);
                    break;
                case "KeyUp":
                    Native.SendKey(ushort.Parse(ev.Data), true);
                    break;
                case "MouseMove":
                case "LDown":
                case "LUp":
                case "RDown":
                case "RUp":
                    if (TryParseCoordinates(ev.Data, out int x, out int y))
                    {
                        Native.MoveMouse(x, y, screenWidth, screenHeight);

                        switch (ev.EventType)
                        {
                            case "LDown": Native.MouseClick("Left", true); break;
                            case "LUp": Native.MouseClick("Left", false); break;
                            case "RDown": Native.MouseClick("Right", true); break;
                            case "RUp": Native.MouseClick("Right", false); break;
                        }
                    }
                break;
            }
        }
    }

    //Helper function for coordinate parsing
    private static bool TryParseCoordinates(string data, out int x, out int y)
    {
        x = y = 0;
        var parts = data.Split(',');
        return parts.Length == 2 &&
            int.TryParse(parts[0], out x) &&
            int.TryParse(parts[1], out y);
    }
}