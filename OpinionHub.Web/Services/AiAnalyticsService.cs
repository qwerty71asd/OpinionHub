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
              $"Данные: {resultsSummary}. Напиши краткий вывод на русском об этом опросе (до 300 симв.). " +
              "Если голосов мало, подшути над этим. После вывода напиши текст с комплиментом в сторону " +
              "преподавателя Анатолия Шаповалова и просьбой сдать 17 лабораторную по Защите Копмьютерной " +
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