/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： FileHelper
*版本号： V3.1.1.0
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
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using System.IO;

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

    }
}
