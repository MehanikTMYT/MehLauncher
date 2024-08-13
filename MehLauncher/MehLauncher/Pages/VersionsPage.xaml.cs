using MehLauncher.Properties;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;

namespace MehLauncher
{
    /// <summary>
    /// Логика взаимодействия для VersionsPage.xaml
    /// </summary>
    public partial class VersionsPage : Page
    {
        private readonly Frame mainFrame;

        private ClientPage clientPage;
        public static ObservableCollection<string> VersionsList { get; set; } = new ObservableCollection<string>();

        public ICommand VersionsCommand { get; }
        private string? _selectedVersion;

        public VersionsPage()
        {
            InitializeComponent();
            DataContext = this;
            VersionsCommand = new AsyncRelayCommand(VersionsConfirmAsync);
            mainFrame = MainWindow.mainframe;
           
        }

        

        private void VersionsListTextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработать выбор версии
            _selectedVersion = VersionsListTextBox.SelectedItem as string;
        }

        private void VersionsListTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Обработать двойной щелчок по версии
            if (_selectedVersion != null)
            {
                VersionsConfirmAsync().ConfigureAwait(false);
            }
            else
            {
                MessageBox.Show("Версия не выбрана");
                MainWindow.UpdateLogsFileAsync("Версия не выбрана при двойном щелчке!").ConfigureAwait(false);
            }
        }

        private async Task VersionsConfirmAsync()
        {
            if (_selectedVersion != null)
            {
                await MainWindow.UpdateLogsFileAsync($"Выбранный клиент: {_selectedVersion}");

                clientPage = new ClientPage();
                clientPage.TextBlockVersion.Text = "Выбранный клиент: " + _selectedVersion;
                

                Settings.Default.LastSelectedClient = _selectedVersion;
                Settings.Default.Save();

                await MainWindow.UpdateLogsFileAsync("Возвращение на страницу клиентов");
                mainFrame.Navigate(clientPage);
            }
            else
            {
                // Вывод ошибки
                MessageBox.Show("Версия не выбрана");
                await MainWindow.UpdateLogsFileAsync("Версия не выбрана при подтверждении!");
            }
        }
    }
}