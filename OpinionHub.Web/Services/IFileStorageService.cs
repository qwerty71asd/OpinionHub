namespace OpinionHub.Web.Services;

public interface IFileStorageService
{
    // Сохраняет файл и возвращает относительный путь для БД (/uploads/...)
    Task<string> SaveFileAsync(IFormFile file, string subDirectory);

    // Удаляет файл с диска
    void DeleteFile(string? relativePath);
}