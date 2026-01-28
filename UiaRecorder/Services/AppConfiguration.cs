using Microsoft.Extensions.Configuration;
using UiaRecorder.Models;

namespace UiaRecorder.Services;

public static class AppConfiguration
{
    public static OpenAiSettings OpenAi { get; private set; } = new();

    public static void Load()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        OpenAi = config.GetSection("OpenAI").Get<OpenAiSettings>() ?? new OpenAiSettings();
    }
}
