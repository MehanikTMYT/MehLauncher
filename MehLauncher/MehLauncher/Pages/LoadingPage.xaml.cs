using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using MehLauncher.Properties;
using MehLauncher.Services;
using MehLauncher.Models;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using System.Net.Http;

namespace MehLauncher
{
    public partial class LoadingPage : Page
    {

        public ICommand LogsCommand { get; }

        private readonly ApiService _apiService;
        private readonly string _directoryPath;
        private readonly ProgressBar _progressBar;

        public LoadingPage()
        {
            InitializeComponent();
            DataContext = this;
            _progressBar = ProgressBar;
            _apiService = new ApiService(); 
            _directoryPath = Settings.Default.LastSelectedFolderPath;

            LogsCommand = new AsyncRelayCommand(ToLogsAsync);

            _ = VerifyClientAsync(); // Запуск асинхронного метода
        }

        private async Task ToLogsAsync()
        {
            // Переход на страницу логов
            var mainFrame = MainWindow.mainframe;
            await MainWindow.UpdateLogsFileAsync("Переход на страницу логов");
            mainFrame.Navigate(new LogsPage(this));
        }

        private async Task VerifyClientAsync()
        {
            string clientName = Settings.Default.LastSelectedClient;

            SetStatus("Инициализация проверки клиента...\nПожалуйста, подождите.");
            await MainWindow.UpdateLogsFileAsync("Инициализация проверки клиента начата");

            try
            {
                var clientFolderPath = Path.Combine(_directoryPath, clientName);

                bool directoryExists = Directory.Exists(clientFolderPath);
                await ProcessClientFilesAsync(clientName, clientFolderPath, directoryExists);

                SetStatus("Проверка клиента завершена успешно.\nПроверьте логи для дополнительных сведений.");
                await MainWindow.UpdateLogsFileAsync("Проверка клиента успешно завершена.");
                await ClientCheckAsync(Settings.Default.LastSelectedClient, clientFolderPath);
            }
            catch (Exception ex)
            {
                await HandleVerificationErrorAsync(ex);
            }
        }

        

        private void SetErrorStatus(string statusMessage, string logMessage)
        {
            SetStatus(statusMessage);
            MainWindow.UpdateLogsFileAsync(logMessage).Wait();
        }

        private async Task<IEnumerable<FileData>> RetrieveFilesAsync(string clientName, bool withHashes)
        {
            SetStatus("Получение списка файлов от сервера...\nПожалуйста, подождите.");
            return withHashes ? await _apiService.GetFilesWithHashesAsync(clientName) : await _apiService.GetFilesAsync(clientName);
        }



        private async Task ProcessClientFilesAsync(string clientName, string clientFolderPath, bool verifyHashes)
        {
            var files = await RetrieveFilesAsync(clientName, verifyHashes);

            if (files == null || !files.Any())
            {
                SetErrorStatus("Не удалось получить список файлов от сервера.\nПроверьте соединение с сервером.", "Ошибка: Не удалось получить список файлов от сервера.");
                return;
            }

            await ProcessFilesAsync(files, clientFolderPath, verifyHashes);
        }


        private async Task ProcessFilesAsync(IEnumerable<FileData> files, string clientFolderPath, bool verifyHashes)
        {
            var totalFiles = files.Count();
            for (int i = 0; i < totalFiles; i++)
            {
                var fileData = files.ElementAt(i);
                var filePath = fileData.FilePath;

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    await LogErrorAsync("Пустой путь файла обнаружен.");
                    continue;
                }

                var fileFullPath = Path.Combine(clientFolderPath, filePath.Replace('/', Path.DirectorySeparatorChar));
                EnsureDirectoryExists(fileFullPath);

                try
                {
                    SetStatus($"Проверка файла \n{filePath}...\nПожалуйста, подождите.");
                    await _apiService.DownloadFileAsync(filePath, verifyHashes ? fileData.Hash : null);
                    SetStatus($"Файл {filePath} проверен успешно.");
                }
                catch (Exception ex)
                {
                    await LogErrorAsync($"Ошибка при скачивании или проверке файла {filePath}: {ex.Message}");
                    SetStatus($"Ошибка при скачивании или проверке файла {filePath}:\n{ex.Message}\nПродолжаем с другими файлами.");
                }

                UpdateProgressBar((i + 1) * 100 / totalFiles);
            }
          
        }

        private async Task LogErrorAsync(string message)
        {
            await MainWindow.UpdateLogsFileAsync(message);
        }

        private void EnsureDirectoryExists(string fileFullPath)
        {
            var directoryPath = Path.GetDirectoryName(fileFullPath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private async Task HandleVerificationErrorAsync(Exception ex)
        {
            SetStatus($"Ошибка при проверке клиента:\n{ex.Message}\nПроверьте логи для получения дополнительных сведений.");
            await LogErrorAsync($"Ошибка при проверке клиента: {ex.Message}. Проверьте логи для получения дополнительных сведений.");
        }

        private void UpdateProgressBar(int value)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _progressBar.Value = value;
                StatusProgressBarTextBlock.Text = $"Прогресс: {value}%";
            });
        }

        internal void SetStatus(string statusMessage)
        {
            StatusTextBlock.Text = statusMessage;
        }
        internal async Task ClientCheckAsync(string clientName, string clientFolderPath)
        {
            SetStatus("Проверка клиента");

            string authlibPath = Path.Combine(clientFolderPath, "authlib.jar");
            string authlibUrl = "https://authlib-injector.yushi.moe/artifact/53/authlib-injector-1.2.5.jar";

            // Проверка наличия файла authlib.jar
            if (!File.Exists(authlibPath))
            {
                SetStatus("Скачивание authlib.jar...");

                try
                {
                    using (HttpClient client = new HttpClient())
                    using (HttpResponseMessage response = await client.GetAsync(authlibUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(authlibPath, fileBytes);
                        SetStatus("authlib.jar успешно скачан.");
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"Ошибка при скачивании authlib.jar: {ex.Message}");
                    return;
                }
            }

            string version = await _apiService.FetchVersionAsync(clientName);
            string authlib = $"-javaagent:{authlibPath}=http://mehhost.ru:1370/";
            MinecraftLauncher launcher = new(new MinecraftPath(clientFolderPath));

            // Обработчик события для изменения прогресса файлов
            launcher.FileProgressChanged += (sender, args) =>
            {
                // Обновляем текст статуса
                StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                {
                    StatusProgressBarTextBlock.Text = $"Прогресс: {args.ProgressedTasks} из {args.TotalTasks} файлов";
                });

                // Обновляем значение ProgressBar
                _progressBar.Dispatcher.Invoke(() =>
                {
                    if (args.TotalTasks > 0)
                    {
                        // Рассчитываем процент завершения
                        var progress = (double)args.ProgressedTasks / args.TotalTasks * 100;
                        _progressBar.Value = progress;
                    }
                });
            };

            // Обработчик события для изменения прогресса в байтах
            launcher.ByteProgressChanged += (sender, args) =>
            {
                // Обновляем текст статуса
                StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                {
                    StatusProgressBarTextBlock.Text = $"{args.ProgressedBytes} байт из {args.TotalBytes} байт";
                });
            };

            // Устанавливаем клиент
            await launcher.InstallAsync(version);

            MLaunchOption minecraftStartOptions = new()
            {
                Session = new MSession($"{Settings.Default.username}", $"{Settings.Default.accessToken}", $"{Settings.Default.userUUID}"),
                MaximumRamMb = Settings.Default.LastEnteredRAM,
                MinimumRamMb = 4096,
                GameLauncherName = $"{clientName} by MehLauncher",
                JvmArgumentOverrides = new MArgument[]
            {
                new($"{authlib}"),
                new("--add-opens"),
                new("java.base/java.lang.invoke=ALL-UNNAMED")
            },
            };

            var process = await launcher.BuildProcessAsync(version, minecraftStartOptions);

            // Конфигурация запуска процесса
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;

            // Обработчики для чтения вывода и ошибок, с выводом в SetStatus
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                    {
                            SetStatus($"Ошибка: {e.Data}");
                    });
                }
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                    {
                        SetStatus(e.Data);
                    });
                }
            };

            // Запуск процесса
            SetStatus("Запуск клиента");
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

    }
}