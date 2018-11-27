/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： FileHelper
*版本号： V3.3.3.4
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:53:26
*修改人： yswenli
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/
using System.IO;
using System.Threading.Tasks;

namespace SAEA.Common
{
    public static class FileHelper
    {
        public static string GetDirecotry(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }


        public static void Write(string filePath, byte[] data)
        {
            GetDirecotry(filePath);
            using (FileStream fs = File.Create(filePath))
            {
                fs.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static byte[] Read(string filePath)
        {
            byte[] data = null;
            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buffer = new byte[fs.Length];
                fs.Position = 0;
                var offset = 0;

                while ((offset = fs.Read(buffer, offset, buffer.Length)) > 0)
                {
                    if (offset == fs.Length) break;

                    if (offset == 0) throw new System.Exception($"坊取{filePath}出现异常！");

                }
                data = buffer;
            }
            return data;
        }

    }
}
