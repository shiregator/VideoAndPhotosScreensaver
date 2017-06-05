using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SettingsViewModel
{
    public class CommandHandler : ICommand
    {
        private Action<object> _action;
        private Func<object, bool> _canExecute;
        public CommandHandler(Action<object> action, Func<object, bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            _action(parameter);
        }

        public void UpdateCanExecute()
        {
            CanExecuteChanged?.Invoke(null, null);
        }
    }
}
