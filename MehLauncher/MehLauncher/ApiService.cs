using System;
using System.Collections.Generic;
using System.IO;
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
        private const string BaseApiUrl = "http://mehhost.ru:5024/api/";
        private const string BaseApiForDownloadUrl = "http://mehhost.ru:5100/";
        private const string AuthUrl = "http://mc-api.mehhost.ru/aurora/auth";

        public ApiService()
        {
            _httpClient = new HttpClient();
            ServicePointManager.DefaultConnectionLimit = 256;
        }

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<AuthResponseData> AuthenticateAsync(string? login, string? password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                string errorMessage = string.IsNullOrWhiteSpace(login) ? "Логин не может быть пустым!" : "Пароль не может быть пустым!";
                await LogAndThrowAsync(errorMessage);
                return default; 
            }

            var requestData = new { login, password };
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(AuthUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await LogAndThrowAsync("Некорректный запрос: " + responseContent);
                return default;
            }

            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseData>>(responseContent, JsonSerializerOptions);
                if (apiResponse == null || !apiResponse.Success)
                {
                    string errorMessage = apiResponse?.Error switch
                    {
                        "User not found" => "Пользователь не найден",
                        "Invalid password" => "Неверный пароль",
                        _ => $"Ошибка: {apiResponse?.Error}"
                    };
                    await LogAndThrowAsync(errorMessage);
                    return default;
                }

                UpdateSettings(apiResponse.Result);
                await MainWindow.UpdateLogsFileAsync("Авторизация успешна");
                return apiResponse.Result;
            }
            catch (JsonException ex)
            {
                await LogAndThrowAsync("Ошибка при десериализации ответа: " + ex.Message, ex);
                return default; 
            }
        }

        public async Task DownloadFileAsync(string filePath, string? expectedHash)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));

            var fileUrl = BuildFileUrl(filePath);
            var fileFullPath = BuildFileFullPath(filePath);

            if (File.Exists(fileFullPath))
            {
                try
                {
                    var existingFileHash = await GetFileHashAsync(fileFullPath);
                    if (expectedHash != null && existingFileHash == expectedHash)
                    {
                        await MainWindow.UpdateLogsFileAsync($"Файл {fileFullPath} уже актуален. Хеш совпадает.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await MainWindow.UpdateLogsFileAsync($"Ошибка при проверке хеша существующего файла {fileFullPath}: {ex.Message}");
                }
            }

            await DownloadAndVerifyFileAsync(fileUrl, fileFullPath, expectedHash);
        }

        private string BuildFileUrl(string filePath) =>
            $"{BaseApiForDownloadUrl}{Settings.Default.LastSelectedClient}/{filePath}";

        private string BuildFileFullPath(string filePath) =>
            Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient, filePath.Replace('/', Path.DirectorySeparatorChar));

        private async Task<string> GetFileHashAsync(string fileFullPath)
        {
            try
            {
                var fileBytes = await File.ReadAllBytesAsync(fileFullPath);
                return CalculateHash(fileBytes);
            }
            catch (Exception ex)
            {
                await LogAndThrowAsync($"Ошибка при чтении файла для проверки хеша {fileFullPath}: {ex.Message}", ex);
                return null;
            }
        }

        private async Task DownloadAndVerifyFileAsync(string fileUrl, string fileFullPath, string? expectedHash)
        {
            EnsureDirectoryExists(fileFullPath);

            try
            {
                using var response = await _httpClient.GetAsync(fileUrl);
                response.EnsureSuccessStatusCode();
                var fileBytes = await response.Content.ReadAsByteArrayAsync();

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
            catch (Exception ex)
            {
                await LogAndThrowAsync($"Ошибка при скачивании файла с URL {fileUrl}: {ex.Message}", ex);
            }
        }

        private void EnsureDirectoryExists(string fileFullPath)
        {
            var directoryPath = Path.GetDirectoryName(fileFullPath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        private string CalculateHash(byte[] fileBytes)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(fileBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public async Task<List<FileData>> GetFilesAsync(string clientName, bool withHash = false)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Имя клиента не может быть пустым", nameof(clientName));

            var apiUrl = $"{BaseApiUrl}listFiles{(withHash ? "WithHashForClient" : "ForClient")}?clientName={clientName}";
            return await SendGetRequestAsync<List<FileData>>(apiUrl);
        }

        private async Task<T> SendGetRequestAsync<T>(string apiUrl)
        {
            try
            {
                using var response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                await LogAndThrowAsync($"Ошибка при выполнении GET-запроса по адресу {apiUrl}: {ex.Message}", ex);
                return default;
            }
        }
        public async Task<List<FileData>> GetFilesWithHashesAsync(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Имя клиента не может быть пустым", nameof(clientName));

            var apiUrl = $"{BaseApiUrl}listFilesWithHashForClient?clientName={clientName}";
            return await SendGetRequestAsync<List<FileData>>(apiUrl);
        }

        public async Task<string> FetchVersionAsync(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                throw new ArgumentException("Имя клиента не может быть пустым", nameof(clientName));

            var requestUri = $"{BaseApiUrl}version?clientName={clientName}";
            try
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                await LogAndThrowAsync($"Ошибка при выполнении запроса: {ex.Message}", ex);
                return null;
            }
        }

        private async Task LogAndThrowAsync(string message, Exception? ex = null)
        {
            await MainWindow.UpdateLogsFileAsync(message);
            if (ex != null)
                throw new Exception(message, ex);
            else
                throw new Exception(message);
        }

        private void UpdateSettings(AuthResponseData result)
        {
            Settings.Default.authResponse = result.AccessToken;
            Settings.Default.username = result.Username;
            Settings.Default.userUUID = result.UserUUID;
            Settings.Default.accessToken = result.AccessToken;
            Settings.Default.Save();
        }
    }
}