/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common
*文件名： ConfigHelper
*版本号： v26.4.23.1
*唯一标识：ed0c4bcd-31db-4053-91a8-625217e91817
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/11 17:28:07
*描述：ConfigHelper接口
*
*=====================================================================
*修改标记
*修改时间：2019/11/11 17:28:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ConfigHelper接口
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Common.Serialization;
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
                using (var fs = File.Open(_filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
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
            catch(Exception ex) { }

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
