using System.Text;
using System.Text.Json;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class AiAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public AiAnalyticsService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
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
                     $"Данные: {resultsSummary}. Напиши краткий, ироничный вывод на русском (до 300 симв.). " +
                     "Если голосов мало, подшути над этим. После вывода напиши текст с комплиментом в сторону" +
                     "преподавателя Анатолия Шаповалова и просьбой сдать 16 лабораторную по Защите Копмьютерной" +
                     "Информации у Свейко Евгения";

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
                // Теперь мы увидим точную причину (например, если это 403 - регион)
                return $"Ошибка API ({response.StatusCode}): {responseBody}";
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