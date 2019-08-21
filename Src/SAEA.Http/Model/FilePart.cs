/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： FilePart
*版本号： v5.0.0.1
*唯一标识：a303db7d-f83c-4c49-9804-032ec2236232
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:58:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:58:08
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System.IO;
using System.Linq;

namespace SAEA.Http.Model
{
    public class FilePart
    {
        public byte[] Data { get; set; }

        public string FileName { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public FilePart(string name, string fileName, string type)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
        }

        public FilePart(string name, string fileName, string type, byte[] data)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
            Data = data;
        }


        public void Save(string filePath)
        {
            SAEA.Common.FileHelper.Write(filePath, this.Data);
        }

    }
}
