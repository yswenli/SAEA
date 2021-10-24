/****************************************************************************
*项目名称：SAEA.FTPTest.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Model
*类 名 称：DirectoryClass
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 15:41:35
*描述：
*=====================================================================
*修改时间：2019/11/18 15:41:35
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
    /// 文件夹类
    /// </summary>
    public class DirectoryClass : BaseInfo
    {
        private bool isFixDriver = false;

        /// <summary>
        /// 是不是固定磁盘,如 C:  D:  E:
        /// </summary>
        public bool IsFixDriver
        {
            get { return isFixDriver; }
            set { isFixDriver = value; }
        }


        private double maxLength;

        /// <summary>
        /// 总大小
        /// </summary>
        public double MaxLength
        {
            get { return maxLength; }
            set { maxLength = value; }
        }

        private double freePrice;

        /// <summary>
        /// 可用空间
        /// </summary>
        public double FreePrice
        {
            get { return freePrice; }
            set { freePrice = value; }
        }
    }
}
