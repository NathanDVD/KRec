using System.Text.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;


public class InputEvent
{
    public int TimeStamp {get; set;}
    public string? EventType {get; set;}
    public string? Data{get; set;}
}

public class Recorder
{
    //Format the mouse data
    public static string FormatMouseData(object mouseData)
    {
        if (mouseData is (int x, int y))
            return $"{x},{y}";

        return "Bad mouse data";
    }


    //Format the keyboard keys data
    private static string FormatKeyData(object rawData)
    {
        if (rawData is int key)
            return key.ToString();

        return "Bad keyboard data";
    }


    ///<summary>
    ///Function to record mouse and keyboard events
    ///</summary>
    /// <returns> A time stamp, a type of event and the event data(key press, mouse movements ect...)</returns>
    public static (int, string, string) FormatData(int timeStamp, string eventType, object rawData)
    {
        string data = eventType switch
        {
            "MouseMove" => FormatMouseData(rawData),
            "MouseClick" => FormatMouseData(rawData),
            "LDown" => FormatMouseData(rawData),
            "LUp" => FormatMouseData(rawData),
            "RDown" => FormatMouseData(rawData),
            "RUp" => FormatMouseData(rawData),
            "KeyUp" or "KeyDown" => FormatKeyData(rawData),
            _  => throw new ArgumentException($"Unknown eventType: {eventType}")
        };


        return (timeStamp, eventType, data);
    }


    ///<summary>
    ///Function to batch save the given infos to a json file
    ///</summary>
    public static void SaveEvents(List<InputEvent> events, string filePath)
    {

        string jsonData = JsonSerializer.Serialize(events);//Prepare data
        File.WriteAllText(filePath, jsonData);
    }
}