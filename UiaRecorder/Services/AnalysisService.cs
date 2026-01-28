using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using UiaRecorder.Models;

namespace UiaRecorder.Services;

public sealed class AnalysisResult
{
    public string HumanSummaryText { get; init; } = string.Empty;
    public string AgentJson { get; init; } = string.Empty;
    public string RawJson { get; init; } = string.Empty;
}

public sealed class AnalysisService
{
    private readonly HttpClient _httpClient;

    public AnalysisService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AnalysisResult> AnalyzeAsync(OpenAiSettings settings, string sessionJson, CancellationToken cancellationToken)
    {
        string prompt = BuildPrompt(sessionJson);

        using HttpRequestMessage request = new(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = new StringContent(BuildRequestBody(settings.Model, prompt), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        string outputText = ExtractOutputText(responseJson);
        string rawJson = outputText.Trim();

        using JsonDocument doc = JsonDocument.Parse(rawJson);
        JsonElement root = doc.RootElement;
        string humanSummaryText = string.Join(Environment.NewLine, root.GetProperty("humanSummary").EnumerateArray().Select(item => item.GetString() ?? string.Empty));
        string agentJson = root.GetProperty("agentInstructions").GetRawText();

        return new AnalysisResult
        {
            HumanSummaryText = humanSummaryText,
            AgentJson = agentJson,
            RawJson = rawJson
        };
    }

    private static string BuildRequestBody(string model, string prompt)
    {
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                humanSummary = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                agentInstructions = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        sessionId = new { type = "string" },
                        startUtc = new { type = "string" },
                        endUtc = new { type = "string" },
                        goal = new { type = "string" },
                        preconditions = new { type = "array", items = new { type = "string" } },
                        steps = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    index = new { type = "integer" },
                                    time = new { type = "string" },
                                    action = new { type = "string" },
                                    target = new
                                    {
                                        type = "object",
                                        additionalProperties = false,
                                        properties = new
                                        {
                                            app = new { type = "string" },
                                            window = new { type = "string" },
                                            element = new
                                            {
                                                type = "object",
                                                additionalProperties = false,
                                                properties = new
                                                {
                                                    name = new { type = "string" },
                                                    controlType = new { type = "string" },
                                                    automationId = new { type = "string" },
                                                    className = new { type = "string" }
                                                }
                                            }
                                        }
                                    },
                                    input = new
                                    {
                                        type = "object",
                                        additionalProperties = false,
                                        properties = new
                                        {
                                            text = new { type = "string" },
                                            keys = new { type = "array", items = new { type = "string" } },
                                            value = new { type = "string" }
                                        }
                                    },
                                    notes = new { type = "string" }
                                }
                            }
                        },
                        verification = new { type = "array", items = new { type = "string" } },
                        fallbacks = new { type = "array", items = new { type = "string" } }
                    },
                    required = new[] { "sessionId", "startUtc", "endUtc", "goal", "steps" }
                }
            },
            required = new[] { "humanSummary", "agentInstructions" }
        };

        var payload = new
        {
            model,
            input = prompt,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "uia_recording_analysis",
                    strict = true,
                    schema
                }
            },
            temperature = 0.2
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildPrompt(string sessionJson)
    {
        return "Summarize what the user did. Output the summary in both a chronological human readable summarization and a structured json format for AI agents to use, outlined here: {humanSummary: string[], agentInstructions: {sessionId, startUtc, endUtc, goal, preconditions, steps, verification, fallbacks}}. The agentInstructions.steps array should be an ordered, replayable set of actions. Use the session data below to infer intent and steps.\n\nSESSION_JSON:\n" + sessionJson;
    }

    private static string ExtractOutputText(string responseJson)
    {
        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("output", out JsonElement outputArray))
        {
            return responseJson;
        }

        StringBuilder builder = new();
        foreach (JsonElement output in outputArray.EnumerateArray())
        {
            if (!output.TryGetProperty("content", out JsonElement contentArray))
            {
                continue;
            }

            foreach (JsonElement content in contentArray.EnumerateArray())
            {
                if (content.TryGetProperty("type", out JsonElement typeElement) && typeElement.GetString() == "output_text")
                {
                    if (content.TryGetProperty("text", out JsonElement textElement))
                    {
                        builder.Append(textElement.GetString());
                    }
                }
            }
        }

        return builder.Length > 0 ? builder.ToString() : responseJson;
    }
}
