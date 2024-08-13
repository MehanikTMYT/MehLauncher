using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace MehLauncher
{
    public partial class LogsPage : Page
    {
        private readonly Frame mainFrame;
        private readonly Page previousPage;

        public static TextBox Debug { get; private set; }
        public ICommand BackCommand { get; }

        public LogsPage(Page previousPage)
        {
            InitializeComponent();
            DataContext = this;
            mainFrame = MainWindow.mainframe;
            this.previousPage = previousPage;
            BackCommand = new AsyncRelayCommand(OnBackAsync);
            Debug = DebugTextBox;
            LoadLogs();
            DebugTextBox.ScrollToEnd();
        }

        private async Task LoadLogs()
        {
            try
            {
                string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log");

                if (File.Exists(logFilePath))
                {
                    string logs = await File.ReadAllTextAsync(logFilePath);
                    DebugTextBox.Text = logs;                 
                }
                else
                {
                    DebugTextBox.Text = "Лог-файл не найден.";
                }
            }
            catch (Exception ex)
            {
                DebugTextBox.Text = $"Ошибка при загрузке логов: {ex.Message}";
            }
        }

        private async Task OnBackAsync()
        {
            await MainWindow.UpdateLogsFileAsync($"Возвращение на предыдущую страницу {previousPage}");
            mainFrame.Navigate(previousPage);
        }
    }
}
