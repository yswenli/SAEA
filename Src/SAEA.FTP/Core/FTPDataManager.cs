/****************************************************************************
*项目名称：SAEA.FTP.Core
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Core
*类 名 称：FTPDataManager
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 16:41:09
*描述：
*=====================================================================
*修改时间：2019/11/7 16:41:09
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.FTP.Core
{
    public class FTPDataManager
    {
        static string _filePath = string.Empty;

        public static string New(string filePath = "")
        {
            if (string.IsNullOrEmpty(filePath))
                return _filePath = PathHelper.GetFilePath(PathHelper.GetCurrentPath("Data"), Guid.NewGuid().ToString("N") + ".temp");
            else
               return _filePath = filePath;
        }

        public static void Receive(byte[] data)
        {
            if (data != null && data.Any())
            {
                using (var fs = File.Open(_filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                }
            }
        }

        public static string ReadText(int timeOut = 3 * 1000)
        {
            int current = 0;

            while (current < timeOut)
            {
                current += 1000;

                if (!File.Exists(_filePath))
                {
                    ThreadHelper.Sleep(1000);
                }
                else
                {
                    try
                    {
                        return File.ReadAllText(_filePath);
                    }
                    catch
                    {
                        ThreadHelper.Sleep(1000);
                    }
                }
            }
            return string.Empty;
        }

        public static bool Checked(int timeOut = 3 * 1000)
        {
            int current = 0;

            while (current < timeOut)
            {
                current += 1000;

                if (!File.Exists(_filePath))
                {
                    ThreadHelper.Sleep(1000);
                }
                else
                {
                    try
                    {
                        using (var fs = File.OpenRead(_filePath))
                        {
                            fs.ReadByte();
                        }
                        return true;
                    }
                    catch
                    {
                        ThreadHelper.Sleep(1000);
                    }
                }
            }
            return false;
        }
    }
}
