using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MehLauncher
{
    public partial class ClientPage : Page
    {
        private readonly Frame mainFrame;
        private LogsPage logs;
        private VersionsPage versionsPage;

        public ClientPage()
        {
            InitializeComponent();
            DataContext = this;
            mainFrame = MainWindow.mainframe;
            logs = new LogsPage(this);
            versionsPage = new VersionsPage();
            // Инициализация команд
            StartCommand = new AsyncRelayCommand(OnStartAsync);
            SettingsCommand = new AsyncRelayCommand(ToSettingsAsync);
            LogsCommand = new AsyncRelayCommand(ToLogsAsync);
            VersionsCommand = new AsyncRelayCommand(ToVersionAsync);
            LoadVersionsAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastSelectedClient))
            {
                TextBlockVersion.Text = "Выбранный клиент: " + Properties.Settings.Default.LastSelectedClient;
            }
        }

        private async Task LoadVersionsAsync()
        {
            using HttpClient client = new();
            try
            {
                string url = "http://mehhost.ru:5024/api/profiles"; // URL вашего API
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseData = await response.Content.ReadAsStringAsync();
                    var versions = JsonConvert.DeserializeObject<ObservableCollection<string>>(responseData);
                    if (versions != null)
                    {
                        // Обновление коллекции на главном UI-потоке
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            VersionsPage.VersionsList.Clear();
                            foreach (var version in versions)
                            {
                                VersionsPage.VersionsList.Add(version);
                            }
                        });

                        await MainWindow.UpdateLogsFileAsync("Версии успешно загружены.");
                    }
                }
                else
                {
                    MessageBox.Show("Ошибка загрузки версий: " + response.StatusCode);
                    await MainWindow.UpdateLogsFileAsync("Ошибка загрузки версий: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Исключение: " + ex.Message);
                await MainWindow.UpdateLogsFileAsync("Исключение: " + ex.Message);
            }
        }

        // Команды
        public ICommand StartCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand LogsCommand { get; }
        public ICommand VersionsCommand { get; }

        // Команды-методы
        private async Task ToVersionAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Переход на страницу выбора версии");
            mainFrame.Navigate(new VersionsPage());
        }
        private async Task OnStartAsync()
        {
            // Логика запуска клиента

            bool isClientSelected = !string.IsNullOrEmpty(Properties.Settings.Default.LastSelectedClient);
            bool isFolderPathSelected = !string.IsNullOrEmpty(Properties.Settings.Default.LastSelectedFolderPath);

            if (isClientSelected && isFolderPathSelected)
            {
                await MainWindow.UpdateLogsFileAsync("Переход на страницу загрузки клиента");
                mainFrame.Navigate(new LoadingPage());
            }
            else
            {
                if (!isClientSelected)
                {
                    MessageBox.Show("Не выбран клиент");
                    await MainWindow.UpdateLogsFileAsync("Не выбран клиент");
                }

                if (!isFolderPathSelected)
                {
                    MessageBox.Show("Не выбран путь");
                    await MainWindow.UpdateLogsFileAsync("Не выбран путь");
                }
            }
        }


        private async Task ToSettingsAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Переход на страницу настроек");
            mainFrame.Navigate(new SettingsPage());
        }

        private async Task ToLogsAsync()
        {
            await MainWindow.UpdateLogsFileAsync("Переход на страницу логов");
            mainFrame.Navigate(new LogsPage(this));
        }
    }
}
