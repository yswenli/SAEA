using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisClient 
{
    public class RedisClientDetailEventArgs:EventArgs
    {
        public RedisClientDetailEventArgs(SAEA.RedisSocket.RedisClient client)
        {
            this.Client = client;
        }

       public SAEA.RedisSocket.RedisClient Client { get; private set; }
    }
}
