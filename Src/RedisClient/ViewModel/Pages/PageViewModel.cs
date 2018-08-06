using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace RedisClient
{
    public abstract class PageViewModel:PropertyChangedBase
    {
        private bool _isVisible;

        public bool IsVisible
        {
            get { return this._isVisible; }
            set
            {
                this._isVisible = value;
                this.NotifyOfPropertyChange(() => this.IsVisible);
            }
        }

        private string _title;
        public string Title
        {
            get { return this._title; }
            protected set
            {
                this._title = value;
                this.NotifyOfPropertyChange(() => this.Title);
            }
        }

        private ICommand _closeCommand;
        
        public ICommand CloseCommand
        {
            get
            {
                return this._closeCommand ?? (this._closeCommand = new RelayCommand(() =>
                    {
                        this.IsVisible = false;
                    }));
            }
        }

    }
}
