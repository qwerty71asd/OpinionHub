using System.Text;
using System.Text.Json;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public record GeneratedPollDto(string Title, string? Description, List<string> Options);

public class AiAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public AiAnalyticsService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    private string ResolveApiUrl() =>
        _config["Gemini:ApiUrl"] is { Length: > 0 } u
            ? u
            : "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    public async Task<GeneratedPollDto?> GeneratePollAsync(string topic, int optionCount, CancellationToken ct = default)
    {
        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        topic = (topic ?? string.Empty).Trim();
        if (topic.Length == 0) return null;
        if (topic.Length > 200) topic = topic.Substring(0, 200);
        optionCount = Math.Clamp(optionCount, 2, 6);

        var prompt =
            $"Сформируй опрос для платформы OpinionHub по теме: \"{topic}\". " +
            $"Верни СТРОГО валидный JSON БЕЗ markdown-обёртки, без пояснений, ровно такой формы:\n" +
            "{\n" +
            "  \"title\": \"короткий заголовок до 100 символов\",\n" +
            "  \"description\": \"короткое описание 1-2 предложения, до 300 символов\",\n" +
            $"  \"options\": [\"вариант 1\", \"вариант 2\", ...] // ровно {optionCount} вариантов\n" +
            "}\n" +
            "Требования: на русском языке, без эмодзи, каждый вариант — до 100 символов, варианты различны, " +
            "заголовок без точки в конце. Ответ — только JSON, ничего больше.";

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ResolveApiUrl()}?key={apiKey}", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return null;

            // Gemini иногда оборачивает JSON в ```json ... ``` — срезаем такую обёртку.
            var cleaned = StripMarkdownFence(text.Trim());

            using var poll = JsonDocument.Parse(cleaned);
            var root = poll.RootElement;

            var title = root.GetProperty("title").GetString() ?? "";
            string? description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            var options = root.GetProperty("options")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Length > 100 ? s.Substring(0, 100) : s)
                .ToList();

            if (string.IsNullOrWhiteSpace(title) || options.Count < 2) return null;
            if (title.Length > 200) title = title.Substring(0, 200);
            if (description is { Length: > 1000 }) description = description.Substring(0, 1000);

            return new GeneratedPollDto(title.Trim(), description?.Trim(), options);
        }
        catch
        {
            return null;
        }
    }

    private static string StripMarkdownFence(string s)
    {
        if (!s.StartsWith("```")) return s;
        // Срезаем первую строку (``` или ```json) и последний ``` блок
        var firstNewline = s.IndexOf('\n');
        if (firstNewline < 0) return s;
        var body = s.Substring(firstNewline + 1);
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body.Substring(0, lastFence).Trim() : body.Trim();
    }

    public async Task<string> AnalyzePollResultsAsync(Poll poll)
    {
        var apiKey = _config["Gemini:ApiKey"];
        // Жестко прописываем рабочий v1beta эндпоинт, если в конфиге что-то не так
        var apiUrl = _config["Gemini:ApiUrl"];
        if (string.IsNullOrEmpty(apiUrl))
        {
            apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
        }

        if (string.IsNullOrEmpty(apiKey)) return "Ошибка: API ключ не найден.";

        // Сбор данных из твоих моделей
        var resultsSummary = string.Join(", ", poll.Options.Select(o =>
        {
            var count = poll.Votes.SelectMany(v => v.Selections).Count(s => s.PollOptionId == o.Id);
            return $"{o.Text}: {count} чел.";
        }));

        var prompt = $"Ты — аналитик данных об опросах OpinionHub. Проанализируй результаты опроса '{poll.Title}'. " +
              $"Данные: {resultsSummary}. Напиши краткий вывод на русском об этом опросе (до 300 симв.). " +
              "Если голосов мало, подшути над этим. После вывода напиши текст с комплиментом в сторону " +
              "преподавателя Анатолия Шаповалова и просьбой сдать 19 лабораторную по Защите Копмьютерной " +
              "Информации у Свейко Евгения. " +
              "ОЧЕНЬ ВАЖНО: Верни просто обычный текст без форматирования Markdown. Не используй звездочки (*) и другие спецсимволы.";

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{apiUrl}?key={apiKey}", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return "Нейросеть сейчас перегружена запросами. Пожалуйста, подождите полминуты и попробуйте снова.";
                }

                // Для всех остальных ошибок (400, 403, 500) оставляем базовый лог, 
                // но лучше возвращать короткий текст
                return $"Ошибка связи с ИИ ({response.StatusCode}). Попробуйте позже.";
            }

            using var doc = JsonDocument.Parse(responseBody);

            // В v1beta структура ответа такая же, достаем текст
            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "ИИ промолчал.";
        }
        catch (Exception ex)
        {
            return $"Критический сбой: {ex.Message}";
        }
    }
}