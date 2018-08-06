using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace RedisClient
{
    public class InputBoxViewModel:PropertyChangedBase
    {
        public string Title { get; set; }

        private string _content;

        public string Content
        {
            get { return this._content; }
            set
            {
                this._content = value;
                this.NotifyOfPropertyChange(() => this.Content);
            }
        }
    }
}
