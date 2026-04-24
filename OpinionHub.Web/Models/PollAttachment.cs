namespace OpinionHub.Web.Models;

public class PollAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Связь с опросом
    public Guid PollId { get; set; }
    public Poll? Poll { get; set; }

    // Информация о файле
    public string FilePath { get; set; } = string.Empty; // Путь на сервере (/uploads/...)
    public string OriginalFileName { get; set; } = string.Empty; // Название файла для скачивания
    public string ContentType { get; set; } = string.Empty; // Тип (image/png, application/pdf и т.д.)
    public long FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}