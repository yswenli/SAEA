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
*唯一标识：8b9c0545-a797-409f-813b-6fbaf7b1dd39
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：FilePart接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.IO;
using System.IO;
using System.Linq;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 上传文件的表单分段信息
    /// </summary>
    public class FilePart
    {
        /// <summary>
        /// 文件内容字节
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 客户端提交的文件名，仅保留最后一个合法文件名片段
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 表单字段名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 文件 MIME 类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 创建文件分段，不携带内容
        /// </summary>
        /// <param name="name">表单字段名</param>
        /// <param name="fileName">客户端提交的文件名，会截取最后一个合法片段</param>
        /// <param name="type">文件 MIME 类型</param>
        public FilePart(string name, string fileName, string type)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
        }

        /// <summary>
        /// 创建文件分段并携带文件内容
        /// </summary>
        /// <param name="name">表单字段名</param>
        /// <param name="fileName">客户端提交的文件名，会截取最后一个合法片段</param>
        /// <param name="type">文件 MIME 类型</param>
        /// <param name="data">文件内容字节</param>
        public FilePart(string name, string fileName, string type, byte[] data)
        {
            Name = name;
            FileName = fileName.Split(Path.GetInvalidFileNameChars()).Last();
            Type = type;
            Data = data;
        }


        /// <summary>
        /// 把文件内容写入指定路径
        /// </summary>
        /// <param name="filePath">目标物理路径</param>
        public void Save(string filePath)
        {
            FileHelper.Write(filePath, this.Data);
        }

    }
}