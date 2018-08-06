using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;


namespace RedisClient
{
    
    public class RelayCommand:ICommand
    {
        #region Fields

        readonly Action _execute;
        readonly Func<bool> _canExecute;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// RelayCommand的构造函数
        /// </summary>
        /// <param name="execute">执行方法的委托</param>
        public RelayCommand(Action execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// RelayCommand的构造函数
        /// </summary>
        /// <param name="execute">执行方法的委托</param>
        /// <param name="canExecute">执行状态的委托</param>
        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion // Constructors

        #region ICommand Members


        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute.Invoke();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        #endregion 
    }
}
