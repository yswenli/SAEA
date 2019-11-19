/****************************************************************************
*项目名称：SAEA.FTPTest.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Common
*类 名 称：SystemIconsImageList
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 15:46:26
*描述：
*=====================================================================
*修改时间：2019/11/18 15:46:26
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using Microsoft.Win32;
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest.Common
{
    public class SystemIconsImageList : IDisposable
    {
        #region  ImageList大/小图像列表字段


        private ImageList _smallImageList = new ImageList();

        /// <summary>
        /// 小图标列表集合
        /// </summary>
        public ImageList SmallImageList
        {
            get { return _smallImageList; }

        }

        private ImageList _largeImageList = new ImageList();

        /// <summary>
        /// 大图标列表集合
        /// </summary>
        public ImageList LargeImageList
        {
            get { return _largeImageList; }

        }

        /// <summary>
        /// 得到图标个数
        /// </summary>
        public int Count
        {
            get { return _smallImageList.Images.Count; }
        }

        private bool _disposed = false;

        #endregion


        #region 构造函数

        public SystemIconsImageList()
            : base()
        {
            //设置小图标的颜色和大小
            _smallImageList.ColorDepth = ColorDepth.Depth32Bit;
            _smallImageList.ImageSize = SystemInformation.SmallIconSize; //设置与系统相同的小图标图片
            _smallImageList.ImageSize = new Size(20, 20);


            //设置大图标的颜色和大小
            _largeImageList.ColorDepth = ColorDepth.Depth32Bit;
            _largeImageList.ImageSize = SystemInformation.IconSize;
            _largeImageList.ImageSize = new Size(32, 32);
        }


        #endregion


        #region 注销控件和窗体

        private void CleanUp(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    _smallImageList.Dispose();
                    _largeImageList.Dispose();
                }
            }

            _disposed = true;
        }
        public void Dispose()
        {
            CleanUp(true);
            GC.SuppressFinalize(this);
        }

        ~SystemIconsImageList()
        {
            CleanUp(false);
        }

        #endregion


        #region  得到与系统相关的图片

        /// <summary>
        /// 根据文件名称得到图片：包括文件和文件夹
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isDir"></param>
        /// <returns></returns>
        public int GetIconIndex(string filePath, bool isDir)
        {
            Icon largeIcon = null;

            Icon smallIcon = null;

            var fileName = Path.GetFileName(filePath);

            var ext = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(ext)) //文件夹 或者没有相关联的后缀名
            {
                if (isDir)
                {
                    ext = "5EEB255733234c4dBECF9A128E896A1E"; // for directories
                }
                else
                {
                    ext = "F9EB930C78D2477c80A51945D505E9C4"; // for files without extension
                }
            }
            else  
            {
                if (ext.Equals(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                    ext.Equals(".lnk", StringComparison.InvariantCultureIgnoreCase))
                {
                    ext = fileName;
                }
            }

            if (_smallImageList.Images.ContainsKey(ext))
            {
                return _smallImageList.Images.IndexOfKey(ext);
            }
            else 
            {
                try
                {
                    if (Path.GetExtension(ext).Equals(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                    Path.GetExtension(ext).Equals(".lnk", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (File.Exists(filePath))
                        {
                            largeIcon = IconHelper.GetIconByFilePath(filePath, true);

                            smallIcon = IconHelper.GetIconByFilePath(filePath, false);
                        }
                        else
                        {
                            IconHelper.GetFileIcon(ext, out largeIcon, out smallIcon);
                        }
                    }
                    else
                    {
                        IconHelper.GetFileIcon(ext, out largeIcon, out smallIcon);
                    }

                    if (largeIcon != null && smallIcon == null)
                    {
                        smallIcon = largeIcon;
                    }

                    if (smallIcon != null && largeIcon == null)
                    {
                        largeIcon = smallIcon;
                    }

                    if (largeIcon == null && smallIcon == null)
                    {
                        IconHelper.GetDefaultIcon(out largeIcon, out smallIcon);
                    }

                    _largeImageList.Images.Add(ext, largeIcon);

                    _smallImageList.Images.Add(ext, smallIcon);

                }
                catch (ArgumentException ex)
                {
                    LogHelper.Error("SystemIconsImageList.GetIconIndex", ex, filePath);
                }

                return _smallImageList.Images.Count - 1;
            }
        }
        #endregion
    }
}
