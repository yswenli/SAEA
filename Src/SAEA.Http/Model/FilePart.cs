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
*命名空间：SAEA.Http.Model
*文件名： FilePart
*版本号： v26.4.23.1
*唯一标识：dc64aea0-e500-45df-8822-3c99e1575191
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common.IO;
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
            FileHelper.Write(filePath, this.Data);
        }

    }
}