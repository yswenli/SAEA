using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace RedisClient
{
    public class ZSetKeyItemViewModel:PropertyChangedBase
    {
        private double _score ;

        public ZSetKeyItemViewModel(double score,string value)
        {
            this._score = score;
            this._value = value;
        }

        public ZSetKeyItemViewModel()
        {

        }


        public double Score
        {
            get { return this._score; }
            set
            {
                this._score = value;
                this.NotifyOfPropertyChange(() => this.Score);
            }
        }

        private string _value;

        public  string Value
        {
            get { return this._value; }
            set
            {
                this._value = value;
                this.NotifyOfPropertyChange(() => this.Value);
            }
        }

    }
}
