/****************************************************************************
*项目名称：SAEA.FTPTest.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Model
*类 名 称：FileClass
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 16:10:20
*描述：
*=====================================================================
*修改时间：2019/11/18 16:10:20
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.FTPTest.Model
{
    /// <summary>
    /// 文件夹
    /// </summary>
    public class FileClass : BaseInfo
    {
        private string fileExtends;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string FileExtends
        {
            get { return fileExtends; }
            set { fileExtends = value; }
        }


        private string fileExtendsName;

        /// <summary>
        /// 文件扩展的名称叫什么
        /// </summary>
        public string FileExtendsName
        {
            get { return fileExtendsName; }
            set { fileExtendsName = value; }
        }

        private double fileMax = 0;

        /// <summary>
        /// 文件大小
        /// </summary>
        public double FileMax
        {
            get { return fileMax; }
            set { fileMax = value; }
        }

    }
}
