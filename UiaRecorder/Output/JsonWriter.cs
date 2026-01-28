using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UiaRecorder.Models;

namespace UiaRecorder.Output;

public static class JsonWriter
{
    public static void Write(string filePath, SessionMetadata session, IReadOnlyCollection<RecordedEvent> events)
    {
        var payload = new
        {
            session,
            events
        };

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(filePath, json);
    }
}
