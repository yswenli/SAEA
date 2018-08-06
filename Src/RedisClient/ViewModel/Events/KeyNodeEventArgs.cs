using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisClient 
{
    public class KeyNodeEventArgs:EventArgs
    {
        public KeyNodeEventArgs(KeyViewModel keyValue,KeyNodeEventType type)
        {
            this.Data = keyValue;
            this.Type = type;
        }

       public KeyViewModel Data { get; private set; }

        public KeyNodeEventType Type { get; private set; }
    }

    public enum KeyNodeEventType
    {
        Select=0,

        Delete=1,
    }
}
