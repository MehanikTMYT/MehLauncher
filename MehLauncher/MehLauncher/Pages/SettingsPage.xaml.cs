using MehLauncher.Properties;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Ookii.Dialogs.Wpf;
using System.Runtime.Intrinsics.Arm;

namespace MehLauncher
{
    /// <summary>
    /// Логика взаимодействия для SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly Frame mainFrame;
        public ICommand LogsCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand VersionCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand FolderCommand { get; }
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = this;
            mainFrame = MainWindow.mainframe;
            LogsCommand = new AsyncRelayCommand(ToLogsAsync);
            BackCommand = new AsyncRelayCommand(ToBackAsync);
            VersionCommand = new AsyncRelayCommand(ToVersionAsync);
            ClearCommand = new AsyncRelayCommand(ClearAsync);
            FolderCommand = new AsyncRelayCommand(ChooseFolderAsync);
            InitializeSettingsAsync().ConfigureAwait(false);
            
        }

        private async Task InitializeSettingsAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
                {
                    await MainWindow.UpdateLogsFileAsync("Установка версии..");
                    Version.Text = "Выбрана версия: " + Settings.Default.LastSelectedClient;
                    await MainWindow.UpdateLogsFileAsync("Установлена версия: " + Settings.Default.LastSelectedClient);
                }
                else
                {
                    Version.Text = "Выбрерите версию";

                }

                if (!string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath))
                {
                    btnClear.Visibility = Visibility.Visible;
                    await MainWindow.UpdateLogsFileAsync("Установка пути..");
                    Folder.Text = "Выбран путь: " + Settings.Default.LastSelectedFolderPath;
                    await MainWindow.UpdateLogsFileAsync("Установлен путь: " + Settings.Default.LastSelectedFolderPath);
                } else
                {
                    btnClear.Visibility = Visibility.Hidden;
                    Folder.Text = "Выберите путь";
                }

                await MainWindow.UpdateLogsFileAsync("Установка ОЗУ..");
                if (Settings.Default.LastEnteredRAM != 0)
                {
                    RAM.Text = $"Значение ОЗУ {Settings.Default.LastEnteredRAM} MB ({Settings.Default.LastEnteredRAM / 1024} ГБ)";
                    sliderRAM.Value = Settings.Default.LastEnteredRAM;
                    await MainWindow.UpdateLogsFileAsync($"Установлено значение ОЗУ: {Settings.Default.LastEnteredRAM} MB ({Settings.Default.LastEnteredRAM / 1024} ГБ)");
                } else
                {
                    RAM.Text = $"Значение ОЗУ по умолчанию: {sliderRAM.Value} MB ({sliderRAM.Value /1024}) ГБ";
                    await MainWindow.UpdateLogsFileAsync($"Установлено значение ОЗУ по умолчанию: {sliderRAM.Value} MB ({sliderRAM.Value / 1024}) ГБ");
                }
               
            }
            catch (Exception ex)
            {
                await MainWindow.UpdateLogsFileAsync($"Ошибка инициализации настроек: {ex.Message}");
            }
        }
    

        private async Task ChooseFolderAsync()
        {
            if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath) || Folder.Text == "Выберите папку")
            {
                VistaFolderBrowserDialog dialog = new()
                {
                    Description = "Выберите папку для лаунчера",
                    UseDescriptionForTitle = true
                };

                // Проверяем, поддерживается ли новый стиль диалога выбора папки
                if (!VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
                {
                    MessageBox.Show("Поскольку вы не используете Windows Vista или более позднюю версию, будет использовано стандартное диалоговое окно выбора папки. Пожалуйста, используйте Windows Vista или новее, чтобы увидеть новый диалог.", "Диалог выбора папки");
                }

                // Открываем диалоговое окно и получаем результат выбора
                if (dialog.ShowDialog() == true)
                {
                    // Сохраняем выбранный путь в настройках
                    if (Settings.Default.LastSelectedFolderPath != dialog.SelectedPath)
                    {
                        Settings.Default.LastSelectedFolderPath = dialog.SelectedPath;
                        Folder.Text = "Выбран путь: " + dialog.SelectedPath;
                        await MainWindow.UpdateLogsFileAsync($"Выбрана и сохранена папка c путём: {dialog.SelectedPath}");
                        Settings.Default.Save();
                    }
                }
                else
                {
                    MessageBox.Show("Вы не выбрали папку! Повторная попытка.");
                }
            }
        }

        private async Task ToVersionAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Переход на страницу версий");
            mainFrame.Navigate(new VersionsPage());
        }

        private async Task ToBackAsync()
        {
            if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath) || string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
            {
                if (string.IsNullOrEmpty(Settings.Default.LastSelectedClient))
                {
                    MessageBox.Show("Клиент не выбран, выберите прежде чем выйти");
                }
                if (string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath))
                {
                    MessageBox.Show("Папка не выбрана, будет установлено значение по умолчанию");
                    Settings.Default.LastSelectedFolderPath = Directory.GetCurrentDirectory();
                    Settings.Default.Save();
                }
            }
            await MainWindow.UpdateLogsFileAsync("Возвращение на страницу клиентов");
            mainFrame.Navigate(new ClientPage());
        }

        private async Task ToLogsAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Переход на страницу логов");
            mainFrame.Navigate(new LogsPage(this));
        }
        internal static async Task ClearAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Инициализация очистки");

            if (!string.IsNullOrEmpty(Settings.Default.LastSelectedClient) && !string.IsNullOrEmpty(Settings.Default.LastSelectedFolderPath))
            {
                try
                {
                    string clientFolderPath = Path.Combine(Settings.Default.LastSelectedFolderPath, Settings.Default.LastSelectedClient);

                    string dataPath = Path.Combine(clientFolderPath, "data");
                    if (Directory.Exists(dataPath))
                    {
                        Directory.Delete(dataPath, true);
                        await MainWindow.UpdateLogsFileAsync($"Папка {dataPath} успешно удалена.");
                    }

                    string gamesPath = Path.Combine(clientFolderPath, "games");
                    if (Directory.Exists(gamesPath))
                    {
                        Directory.Delete(gamesPath, true);
                        await MainWindow.UpdateLogsFileAsync($"Папка {gamesPath} успешно удалена.");
                    }

                    // Обновление параметра типа для повторной загрузки файлов
                    Settings.Default.UpdateType = true;
                    Settings.Default.Save();

                    await MainWindow.UpdateLogsFileAsync("Очистка успешно завершена");
                }
                catch (Exception ex)
                {
                    await MainWindow.UpdateLogsFileAsync($"Ошибка при очистке: {ex.Message}");
                }
            }
            else
            {
                await MainWindow.UpdateLogsFileAsync("Клиент или путь не выбран. Очистка не выполнена.");
            }
        }


        private async void sliderRAM_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // Проверяем, изменилось ли значение слайдера
                if (Settings.Default.LastEnteredRAM != Convert.ToInt32(sliderRAM.Value))
                {
                    DispatcherTimer timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5) // Интервал ожидания изменений
                    };

                    RAM.Text = $"Значение ОЗУ {sliderRAM.Value} MB ({sliderRAM.Value / 1024} ГБ)";
                    timer.Tick += async (s, args) =>
                    {
                        // Останавливаем таймер, чтобы избежать повторных вызовов
                        timer.Stop();

                        // Записываем значение слайдера в логи и сохраняем в память
                        await MainWindow.UpdateLogsFileAsync($"Значение ОЗУ: {sliderRAM.Value} MB ({sliderRAM.Value / 1024} ГБ)");
                        Settings.Default.LastEnteredRAM = (int)sliderRAM.Value;
                        Settings.Default.Save();
                    };

                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                await MainWindow.UpdateLogsFileAsync($"Ошибка при сохранении значения слайдера: {ex.Message}");
            }
        }


    }
}
