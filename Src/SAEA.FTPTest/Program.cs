using SAEA.FTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.FTPTest
{
    class Program
    {
        static void Main(string[] args)
        {

            FTPClient client = new FTPClient("127.0.0.1", 21, "yswenli", "12321");

            client.Connect();

            client.CheckDir("/BaiduNetdiskDownload");

            var dirs = client.Dir("/WORKS");
        }
    }
}
