using System;
using System.Collections.Generic;
using System.Windows.Input;


namespace RedisClient
{
    
    public class RelayCommand<T> : ICommand
    {
        #region Fields

        readonly Action<T> _execute;
        readonly Func<T, bool> _canExecute;

        #endregion  

        #region Constructors

        /// <summary>
        /// RelayCommand的构造函数
        /// </summary>
        /// <param name="execute">执行方法的委托</param>
        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// RelayCommand的构造函数
        /// </summary>
        /// <param name="execute">执行方法的委托</param>
        /// <param name="canExecute">执行状态的委托</param>
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion  

        #region ICommand Members

     
        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;
            if (parameter == null && typeof(T).IsValueType)
                return _canExecute.Invoke(default(T));
            return _canExecute.Invoke((T)parameter);
        
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        #endregion 
    }
}
