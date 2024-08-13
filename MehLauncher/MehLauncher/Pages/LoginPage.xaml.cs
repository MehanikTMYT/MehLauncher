using MehLauncher.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MehLauncher
{
    /// <summary>
    /// Логика взаимодействия для LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page, INotifyPropertyChanged
    {
        private readonly ApiService _authService;
        private string? _username;
        private string? _password;
        private string? _statusMessage;
        private LogsPage logs;
        private readonly Frame? mainFrame = MainWindow.mainframe;
        

        public LoginPage()
        {
            InitializeComponent();
            logs = new LogsPage(this);
            DataContext = this;
            //Properties.Settings.Default.Reset();
            _authService = new ApiService();
            
            string? token = Properties.Settings.Default.authResponse;

            CheckBoxToken.IsChecked = Properties.Settings.Default.UseToken;
            if (CheckBoxToken.IsChecked == true)
            {
                LoginCommand = new RelayCommand(async () => await LoginAsync(token));
            }
            else
            {
                LoginCommand = new RelayCommand(async () => await LoginAsync(null));
            }
        }
        public string? Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged(nameof(Username));
                }
            }
        }

        public string? Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged(nameof(Password));
                }
            }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public ICommand? LoginCommand { get; }

        private async Task LoginAsync(string? token)
        {
            try
            {

                if (Properties.Settings.Default.UseToken && !string.IsNullOrEmpty(token))
                {
                    StatusMessage = $"Вход в систему успешный!";
                    mainFrame.Navigate(new ClientPage());

                }
                else if (Properties.Settings.Default.UseToken == false || string.IsNullOrEmpty(token))
                {
                    await MainWindow.UpdateLogsFileAsync("Авторизация");
                    Models.AuthResponseData? authResponse = await _authService.AuthenticateAsync(Username, Password);
                    StatusMessage = $"Вход в систему успешный!";
                    mainFrame.Navigate(new ClientPage());
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Вход в систему не удался: {ex.Message}";
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Password = (sender as PasswordBox)?.Password;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void CheckBoxToken_Checked(object? sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UseToken = true;
            Properties.Settings.Default.Save();
        }

        private void CheckBoxToken_Unchecked(object? sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UseToken = false;
            Properties.Settings.Default.Save();
        }

    }

    
}

