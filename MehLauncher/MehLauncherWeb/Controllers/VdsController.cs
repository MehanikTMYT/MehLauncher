using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text.Json;


namespace MehLauncherWeb.Controllers
{
    [Route("api")]
    [ApiController]
    public class VdsController : ControllerBase
    {
        private readonly ILogger<VdsController> _logger;
        private readonly string _profileFolderPath;
        private readonly string _clientsFolderPath;

        public VdsController(ILogger<VdsController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _profileFolderPath = configuration["Paths:ProfilesFolder"];
            _clientsFolderPath = configuration["Paths:ClientsFolder"];

            if (string.IsNullOrEmpty(_profileFolderPath) || string.IsNullOrEmpty(_clientsFolderPath))
            {
                _logger.LogError("Некорректные пути к профилям или клиентам в конфигурации.");
                throw new ArgumentException("Некорректные пути к профилям или клиентам в конфигурации.");
            }
        }

        [HttpGet("profiles")]
        public IActionResult GetAllProfiles()
        {
            if (!Directory.Exists(_profileFolderPath))
            {
                _logger.LogWarning($"Папка профилей не найдена по пути: {_profileFolderPath}");
                return NotFound("Папка профилей не найдена.");
            }

            var files = Directory.GetFiles(_profileFolderPath, "*.json");
            var profiles = files.Select(file => Path.GetFileNameWithoutExtension(file)).ToList();

            return Ok(profiles);
        }

        [HttpGet("version")]
        public IActionResult GetVersion(string clientName)
        {
            if (!Directory.Exists(_profileFolderPath))
            {
                _logger.LogWarning($"Папка профилей не найдена по пути: {_profileFolderPath}");
                return NotFound("Папка профилей не найдена.");
            }

            // Сформировать путь к файлу
            var filePath = Path.Combine(_profileFolderPath, $"{clientName}.json");

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning($"Файл для клиента {clientName} не найден по пути: {filePath}");
                return NotFound("Файл не найден.");
            }

            try
            {
                // Считать содержимое файла
                var jsonString = System.IO.File.ReadAllText(filePath);

                // Разобрать JSON
                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    // Извлечь версию
                    if (doc.RootElement.TryGetProperty("Version", out JsonElement versionElement))
                    {
                        var version = versionElement.GetString();
                        return Ok(version);
                    }
                    else
                    {
                        _logger.LogWarning($"Свойство 'Version' не найдено в файле: {filePath}");
                        return NotFound("Свойство 'Version' не найдено.");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Ошибка при разборе JSON в файле: {filePath}");
                return StatusCode(500, "Ошибка при разборе JSON.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Неожиданная ошибка при обработке файла: {filePath}");
                return StatusCode(500, "Неожиданная ошибка.");
            }
        }


        [HttpGet("listFilesForClient")]
        public IActionResult GetFilesList(string clientName)
        {
            var clientFolderPath = Path.Combine(_clientsFolderPath, clientName);

            if (!Directory.Exists(clientFolderPath))
            {
                _logger.LogWarning($"Папка клиента {clientName} не найдена по пути: {clientFolderPath}");
                return NotFound($"Папка клиента {clientName} не найдена.");
            }

            try
            {
                var files = GetAllFiles(clientFolderPath);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении списка файлов для клиента: {clientFolderPath}");
                return StatusCode(500, "Ошибка при получении списка файлов.");
            }
        }

        private List<string> GetAllFiles(string rootPath)
        {
            var filesList = new List<string>();

            void AddFiles(string path)
            {
                try
                {
                    // Добавляем все файлы в текущем каталоге
                    filesList.AddRange(Directory.GetFiles(path)
                        .Select(file => Path.GetRelativePath(rootPath, file).Replace('\\', '/')));

                    // Рекурсивно добавляем все подкаталоги
                    foreach (var directory in Directory.GetDirectories(path))
                    {
                        AddFiles(directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при обработке содержимого директории: {path}");
                }
            }

            AddFiles(rootPath);
            return filesList;
        }

        [HttpGet("listFilesWithHashForClient")]
        public IActionResult GetFilesHashList(string clientName)
        {
            var clientFolderPath = Path.Combine(_clientsFolderPath, clientName);

            if (!Directory.Exists(clientFolderPath))
            {
                _logger.LogWarning($"Папка клиента {clientName} не найдена по пути: {clientFolderPath}");
                return NotFound($"Папка клиента {clientName} не найдена.");
            }

            try
            {
                var filesWithHashes = GetAllFilesWithHashes(clientFolderPath);
                return Ok(filesWithHashes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении списка файлов для клиента: {clientFolderPath}");
                return StatusCode(500, "Ошибка при получении списка файлов.");
            }
        }

        private List<FileHashInfo> GetAllFilesWithHashes(string rootPath)
        {
            var filesList = new List<FileHashInfo>();

            void AddFiles(string path)
            {
                try
                {
                    // Добавляем все файлы в текущем каталоге и вычисляем их хеши
                    filesList.AddRange(Directory.GetFiles(path).Select(file => new FileHashInfo
                    {
                        FilePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/'),
                        Hash = CalculateFileHash(file)
                    }));

                    // Рекурсивно добавляем все подкаталоги
                    foreach (var directory in Directory.GetDirectories(path))
                    {
                        AddFiles(directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка при обработке содержимого директории: {path}");
                }
            }

            AddFiles(rootPath);
            return filesList;
        }

        private string CalculateFileHash(string filePath)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    var hash = hashAlgorithm.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }

        public class FileHashInfo
        {
            public string FilePath { get; set; }
            public string Hash { get; set; }
        }

       
    }
}