namespace OpinionHub.Web.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private const string UploadsFolder = "uploads";

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subDirectory)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Файл пуст");

        // 1. Формируем путь к папке (например, wwwroot/uploads/covers)
        var folderPath = Path.Combine(_env.WebRootPath, UploadsFolder, subDirectory);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        // 2. Генерируем уникальное имя файла, чтобы избежать перезаписи
        // Берем расширение оригинала и добавляем к GUID
        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(folderPath, fileName);

        // 3. Сохраняем файл на диск
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 4. Возвращаем относительный путь для хранения в БД
        return $"/{UploadsFolder}/{subDirectory}/{fileName}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;

        // Убираем начальный слэш, чтобы Path.Combine сработал корректно
        var cleanPath = relativePath.TrimStart('/');
        var fullPath = Path.Combine(_env.WebRootPath, cleanPath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}