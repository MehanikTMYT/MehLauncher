using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MehLauncher
{
    // Базовый класс команд
    public abstract class RelayCommandBase : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public abstract bool CanExecute(object? parameter);
        public abstract void Execute(object? parameter);
    }

    // Команда с действиями
    public class RelayCommand : RelayCommandBase
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public override bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

        public override void Execute(object? parameter) => _execute();
    }

    // Асинхронная команда с действиями
    public class AsyncRelayCommand : RelayCommandBase
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public override bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

        public override async void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                await _execute();
            }
        }
    }
}
