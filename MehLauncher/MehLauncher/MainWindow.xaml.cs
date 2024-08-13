using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MehLauncher.Services;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;

namespace MehLauncher
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Frame mainframe;

        private static Stack<Page> navigationHistory = new();
        public MainWindow()
        {
            InitializeComponent();
            mainframe = MainFrame;
            ClearLogFileAsync().Wait();
            if (Properties.Settings.Default.UseToken && !string.IsNullOrEmpty(Properties.Settings.Default.authResponse))
            {
                MainFrame.Navigate(new ClientPage());
            }
            else
            {
                MainFrame.Navigate(new LoginPage());
            }
            
        }
        public static async Task UpdateLogsFileAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                string logMessage = $"{DateTime.Now} - {message}\r\n";
                await File.AppendAllTextAsync(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                // Обработка ошибок при записи в файл (например, запись в консоль)
                Console.WriteLine($"Ошибка при записи в лог: {ex.Message}");
            }
        }
        private static async Task ClearLogFileAsync()
        {
            string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log");

            try
            {
                if (File.Exists(logFilePath))
                {
                    await File.WriteAllTextAsync(logFilePath, string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок при очистке файла (например, запись в консоль)
                Console.WriteLine($"Ошибка при очистке лог-файла: {ex.Message}");
            }
        }
    }
}
