/****************************************************************************
*项目名称：SAEA.FTPTest.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Model
*类 名 称：BaseInfo
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 15:41:09
*描述：
*=====================================================================
*修改时间：2019/11/18 15:41:09
*修 改 人： yswenli
*版 本 号： V1.0.0.0
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
    /// 公共信息类
    /// </summary>
    public class BaseInfo
    {
        private string name;

        /// <summary>
        /// 文件名
        /// </summary>
        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        private string fullName;

        /// <summary>
        /// 文件路径（文件全名称)
        /// </summary>
        public string FullName
        {
            get { return fullName; }
            set { fullName = value; }
        }




        private string CreateTime;

        /// <summary>
        /// 修改日期
        /// </summary>
        public string CreateTime1
        {
            get { return CreateTime; }
            set { CreateTime = value; }
        }

        private bool isDirectory;

        /// <summary>
        /// 是不是文件夹
        /// </summary>
        public bool IsDirectory
        {
            get { return isDirectory; }
            set { isDirectory = value; }
        }
    }
}
