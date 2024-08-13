using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MehLauncher.Models;
using MehLauncher.Properties;

namespace MehLauncher.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        public readonly string _baseApiUrl = "http://mehhost.ru:5024/api/";
        public readonly string _baseApiForDownloadUrl = "http://mehhost.ru:5100/";
        private readonly string _authUrl = "http://mc-api.mehhost.ru/aurora/auth";
        private readonly LoadingPage _loadingPage;


        public ApiService()
        {
            _httpClient = new HttpClient();
            ServicePointManager.DefaultConnectionLimit = 256;
        }

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<AuthResponseData> AuthenticateAsync(string? login, string? password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                var errorMessage = string.IsNullOrWhiteSpace(login)
                    ? "Логин не может быть пустым!"
                    : "Пароль не может быть пустым!";

                await MainWindow.UpdateLogsFileAsync(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            var requestData = new { login, password };
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_authUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await MainWindow.UpdateLogsFileAsync("Некорректный запрос: " + responseContent);
                throw new Exception("Некорректный запрос: " + responseContent);
            }

            try
            {
                ApiResponse<AuthResponseData>? apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseData>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null)
                {
                    await MainWindow.UpdateLogsFileAsync("Неизвестная ошибка в ответе API");
                    throw new Exception("Неизвестная ошибка в ответе API");
                }

                if (!apiResponse.Success)
                {
                    var errorMessage = apiResponse.Error switch
                    {
                        "User not found" => "Пользователь не найден",
                        "Invalid password" => "Неверный пароль",
                        _ => $"Ошибка: {apiResponse.Error}"
                    };

                    await MainWindow.UpdateLogsFileAsync(errorMessage);
                    throw new Exception(errorMessage);
                }

                Settings.Default.authResponse = apiResponse.Result.AccessToken;
                Settings.Default.username = apiResponse.Result.Username;
                Settings.Default.userUUID = apiResponse.Result.UserUUID;
                Settings.Default.accessToken = apiResponse.Result.AccessToken;
                Settings.Default.Save();
                await MainWindow.UpdateLogsFileAsync("Авторизация успешна");
                return apiResponse.Result;
            }
            catch (JsonException ex)
            {
                await MainWindow.UpdateLogsFileAsync("Ошибка при десериализации ответа: " + ex.Message);
                throw new Exception("Ошибка при десериализации ответа: " + ex.Message, ex);
            }
        }

        public async Task DownloadFileAsync(string filePath, string? expectedHash)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));
            }

            var fileUrl = BuildFileUrl(filePath);
            var fileFullPath = BuildFileFullPath(filePath);

            bool fileExists = File.Exists(fileFullPath);

            // Если файл существует, проверяем его хеш
            if (fileExists)
            {
                try
                {
                    var existingFileHash = await GetFileHashAsync(fileFullPath);

                    // Если ожидаемый хеш не равен null и хеши совпадают, файл уже актуален
                    if (expectedHash != null && existingFileHash == expectedHash)
                    {
                        await MainWindow.UpdateLogsFileAsync($"Файл {fileFullPath} уже актуален. Хеш совпадает.");
                        return; // Файл уже актуален, не скачиваем его заново
                    }
                }
                catch (Exception ex)
                {
                    await MainWindow.UpdateLogsFileAsync($"Ошибка при проверке хеша существующего файла {fileFullPath}: {ex.Message}");
                    // В случае ошибки проверки, переходим к загрузке файла
                }
            }

            // Если файл не существует или хеши не совпадают, скачиваем файл и проверяем его
            _loadingPage.SetStatus($"Скачивание файла \n{filePath}...\nПожалуйста, подождите");
            await DownloadAndVerifyFileAsync(fileUrl, fileFullPath, expectedHash);
        }

        private string BuildFileUrl(string filePath)
        {
            return new Uri(new Uri(_baseApiForDownloadUrl), $"{Settings.Default.LastSelectedClient}/{filePath}").ToString();
        }

        private string BuildFileFullPath(string filePath)
        {
            filePath = filePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, filePath);
        }

        private async Task<string> GetFileHashAsync(string fileFullPath)
        {
            try
            {
                var fileBytes = await File.ReadAllBytesAsync(fileFullPath);
                return CalculateHash(fileBytes);
            }
            catch (Exception ex)
            {
                await MainWindow.UpdateLogsFileAsync($"Ошибка при чтении файла для проверки хеша {fileFullPath}: {ex.Message}");
                throw;
            }
        }


        private async Task DownloadAndVerifyFileAsync(string fileUrl, string fileFullPath, string? expectedHash)
        {
            EnsureDirectoryExists(fileFullPath);
            
            try
            {
                using (var response = await _httpClient.GetAsync(fileUrl))
                {
                    response.EnsureSuccessStatusCode();
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();

                    // Если ожидаемый хеш не равен null, проверяем хеш скачанного файла
                    if (expectedHash != null)
                    {
                        var downloadedHash = CalculateHash(fileBytes);
                        if (downloadedHash != expectedHash)
                        {
                            throw new InvalidOperationException($"Хеш файла {fileFullPath} не совпадает с ожидаемым. Ожидалось: {expectedHash}, получено: {downloadedHash}");
                        }
                    }

                    await File.WriteAllBytesAsync(fileFullPath, fileBytes);
                }
            }
            catch (Exception ex)
            {
                await MainWindow.UpdateLogsFileAsync($"Ошибка при скачивании файла с URL {fileUrl}: {ex.Message}");
                throw;
            }
        }

        private void EnsureDirectoryExists(string fileFullPath)
        {
            var directoryPath = Path.GetDirectoryName(fileFullPath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private string CalculateHash(byte[] fileBytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(fileBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public async Task<List<FileData>> GetFilesWithHashesAsync(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Имя клиента не может быть пустым", nameof(clientName));

            var apiUrl = $"{_baseApiUrl}listFilesWithHashForClient?clientName={clientName}";

            return await SendGetRequestAsync<List<FileData>>(apiUrl);
        }

        public async Task<List<FileData>> GetFilesAsync(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Имя клиента не может быть пустым", nameof(clientName));

            var apiUrl = $"{_baseApiUrl}listFilesForClient?clientName={clientName}";

            var filePaths = await SendGetRequestAsync<List<string>>(apiUrl);
            return filePaths.Select(filePath => new FileData { FilePath = filePath }).ToList();
        }

        private async Task<T> SendGetRequestAsync<T>(string apiUrl)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(apiUrl))
                {
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<T>(content, _jsonSerializerOptions);
                }
            }
            catch (Exception ex)
            {
                await MainWindow.UpdateLogsFileAsync($"Ошибка при выполнении GET-запроса по адресу {apiUrl}: {ex.Message}");
                throw;
            }
        }
        public async Task<string> FetchVersionAsync(string clientName)
        {
            // Создать URL запроса
            var requestUri = $"{_baseApiUrl}version?clientName={clientName}";

            try
            {
                // Отправить GET-запрос
                var response = await _httpClient.GetAsync(requestUri);

                // Проверить статус-код ответа
                response.EnsureSuccessStatusCode();

                // Прочитать содержимое ответа
                var version = await response.Content.ReadAsStringAsync();

                return version;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Ошибка при выполнении запроса: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Неожиданная ошибка: {ex.Message}");
                throw;
            }
        }

    }
}