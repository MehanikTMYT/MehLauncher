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
using System.Diagnostics;
using System.Windows;

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

                var files = await RetrieveFilesAsync(clientName, directoryExists);
                if (files == null || !files.Any())
                {
                    await LogAndSetStatusAsync("Ошибка: Не удалось получить список файлов от сервера.", "Не удалось получить список файлов от сервера.");
                    return;
                }

                await ProcessFilesAsync(files, clientFolderPath);
                SetStatus("Проверка клиента завершена успешно.\nПроверьте логи для дополнительных сведений.");
                await MainWindow.UpdateLogsFileAsync("Проверка клиента успешно завершена.");
                await ClientCheckAsync(clientName, clientFolderPath);
            }
            catch (Exception ex)
            {
                await HandleVerificationErrorAsync(ex);
            }
        }
        private async Task<bool> ShowConfirmationDialogAsync(string message)
        {
            // Реализуйте отображение сообщения пользователю и получение его ответа.
            // Например, используя диалоговое окно в WPF:
            var result = MessageBox.Show(message, "Подтверждение", MessageBoxButton.YesNo);
            return result == MessageBoxResult.Yes;
        }


        private async Task<IEnumerable<FileData>> RetrieveFilesAsync(string clientName, bool withHashes)
        {
            SetStatus("Получение списка файлов от сервера...\nПожалуйста, подождите.");

            // Получите список файлов с хешами
            var files = withHashes
                ? await _apiService.GetFilesWithHashesAsync(clientName)
                : await _apiService.GetFilesAsync(clientName);

            // Определите, нужно ли проверять папку "assets"
            var needsAssetCheck = files.Any(file => file.FilePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase));

            if (needsAssetCheck)
            {
                var userResponse = await ShowConfirmationDialogAsync("Проверка папки 'assets' обнаружена. Хотите продолжить проверку папки 'assets'?");
                if (!userResponse)
                {
                    // Если пользователь не хочет проверять папку "assets", отфильтруйте файлы без "assets"
                    files = files.Where(file => !file.FilePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            return files;
        }

        private async Task ProcessFilesAsync(IEnumerable<FileData> files, string clientFolderPath)
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
                    await _apiService.DownloadFileAsync(filePath, fileData.Hash);
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

        private async Task LogErrorAsync(string message) =>
            await MainWindow.UpdateLogsFileAsync(message);

        private void EnsureDirectoryExists(string fileFullPath)
        {
            var directoryPath = Path.GetDirectoryName(fileFullPath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
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

        internal void SetStatus(string statusMessage) =>
            StatusTextBlock.Text = statusMessage;

        internal async Task ClientCheckAsync(string clientName, string clientFolderPath)
        {
            SetStatus("Проверка клиента");

            string authlibPath = Path.Combine(clientFolderPath, "authlib.jar");
            string authlibUrl = "https://authlib-injector.yushi.moe/artifact/53/authlib-injector-1.2.5.jar";

            if (!File.Exists(authlibPath))
            {
                SetStatus("Скачивание authlib.jar...");
                try
                {
                    using var client = new HttpClient();
                    using var response = await client.GetAsync(authlibUrl);
                    response.EnsureSuccessStatusCode();
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(authlibPath, fileBytes);
                    SetStatus("authlib.jar успешно скачан.");
                }
                catch (Exception ex)
                {
                    SetStatus($"Ошибка при скачивании authlib.jar: {ex.Message}");
                    return;
                }
            }
            SetStatus("Проверка клиента");
            string version = await _apiService.FetchVersionAsync(clientName);
            string authlib = $"-javaagent:{authlibPath}=http://mehhost.ru:1370/";
            var launcher = new MinecraftLauncher(new MinecraftPath(clientFolderPath));

            launcher.FileProgressChanged += (sender, args) =>
            {
                StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                {
                    StatusProgressBarTextBlock.Text = $"Прогресс: {args.ProgressedTasks} из {args.TotalTasks} файлов";
                });

                _progressBar.Dispatcher.Invoke(() =>
                {
                    if (args.TotalTasks > 0)
                    {
                        var progress = (double)args.ProgressedTasks / args.TotalTasks * 100;
                        _progressBar.Value = progress;
                    }
                });
            };

            launcher.ByteProgressChanged += (sender, args) =>
            {
                StatusProgressBarTextBlock.Dispatcher.Invoke(() =>
                {
                    StatusProgressBarTextBlock.Text = $"{args.ProgressedBytes} байт из {args.TotalBytes} байт";
                });
            };

            await launcher.InstallAsync(version);
            string javaPath = Path.Combine(
                Settings.Default.LastSelectedFolderPath,
                Settings.Default.LastSelectedClient,
                "runtime",
                "windows-x64",
                "java-runtime-delta",
                "bin",
                "javaw.exe"
            );
            var minecraftStartOptions = new MLaunchOption
            {
                Session = new MSession(Settings.Default.username, Settings.Default.accessToken, Settings.Default.userUUID),
                MaximumRamMb = Settings.Default.LastEnteredRAM,
                MinimumRamMb = 4096,
                GameLauncherName = $"{clientName}",
                ExtraGameArguments = new MArgument[]
                {
                    new MArgument(authlib),
                    new MArgument("--server")
                }
            };

            var process = await launcher.BuildProcessAsync(version, minecraftStartOptions);
            SetStatus("Проверка клиента завершена, запуск");
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Dispatcher.Invoke(() => SetStatus($"Ошибка: {e.Data}"));
                }
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Dispatcher.Invoke(() => SetStatus(e.Data));
                }
            };

            SetStatus("Запуск клиента");
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        private async Task LogAndSetStatusAsync(string logMessage, string statusMessage)
        {
            await LogErrorAsync(logMessage);
            SetStatus(statusMessage);
        }
    }
}