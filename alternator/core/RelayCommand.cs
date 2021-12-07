using System;
using System.Windows.Input;

namespace guildwars2.tools.alternator
{
    class RelayCommand : ICommand
    {

        private Action<object?> execute;
        private Func<object?, bool>? canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }


        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
    }
}
