using SAEA.RedisSocket.Core;

namespace SAEA.RedisSocket.Interface
{
    public interface IClient
    {
        bool IsMaster
        {
            get;
        }

        int DBIndex
        {
            get;
        }

        bool IsConnected { get; set; }

        string Auth(string password);

        string Ping();

        bool Select(int dbIndex = 0);

        int DBSize();

        string Type(string key);

        string Info(string section = "all");

        string SlaveOf(string ipPort = "");        

        RedisDataBase GetDataBase(int dbIndex = -1);
    }
}
