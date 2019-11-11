/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：ConfigHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 16:46:28
*描述：
*=====================================================================
*修改时间：2019/11/11 16:46:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.IO;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 对象持久化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConfigHelper<T> where T : class, new()
    {
        string _filePath = string.Empty;

        public ConfigHelper(string dirName, string fileName)
        {
            _filePath = Path.Combine(PathHelper.GetCurrentPath(dirName), fileName);
        }

        public T Read()
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                using (var fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var bytes = new byte[1024];

                    fs.Position = 0;

                    while (true)
                    {
                        var count = fs.Read(bytes, 0, bytes.Length);

                        if (count == 0)
                            break;

                        if (count == bytes.Length)
                        {
                            sb.Append(Encoding.UTF8.GetString(bytes));
                        }
                        else
                        {
                            sb.Append(Encoding.UTF8.GetString(bytes.AsSpan().Slice(0, count).ToArray()));
                            break;
                        }
                    }
                }
            }
            catch { }

            var json = sb.ToString();

            if (string.IsNullOrWhiteSpace(json)) return default(T);

            return SerializeHelper.Deserialize<T>(json);
        }


        public void Write(T t)
        {
            if (t != null)
            {
                var json = SerializeHelper.Serialize(t);

                using (var fs = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    var data = Encoding.UTF8.GetBytes(json);

                    fs.Write(data, 0, data.Length);
                }
            }
        }
    }
}
