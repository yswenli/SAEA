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
        #region Win32 declarations


        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;  //大图标
        private const uint SHGFI_SMALLICON = 0x1;  //小图标

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon; //文件的图标句柄  

            public IntPtr iIcon;  //图标的系统索引号  

            public uint dwAttributes;  //文件的属性值  

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;  //文件的显示名  


            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;  //文件的类型名  
        };

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);


        #endregion

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
        /// <param name="FileName">Name of an existing File or Directory</param>
        /// <returns>图片的索引</returns>
        public int GetIconIndex(string FileName)
        {
            SHFILEINFO shinfo = new SHFILEINFO();

            FileInfo info = new FileInfo(FileName);

            string ext = info.Extension; //文件的后缀名 .jpg

            if (string.IsNullOrEmpty(ext)) //文件夹 或者没有相关联的后缀名
            {
                if ((info.Attributes & FileAttributes.Directory) != 0) //文件夹
                {
                    ext = "5EEB255733234c4dBECF9A128E896A1E"; // for directories
                }
                else
                {
                    ext = "F9EB930C78D2477c80A51945D505E9C4"; // for files without extension
                }
            }
            else  //文件
            {
                //如果是exe文件或者快捷方式
                if (ext.Equals(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                    ext.Equals(".lnk", StringComparison.InvariantCultureIgnoreCase))
                {
                    ext = info.Name;
                }

            }


            //看是否存在于imageList中
            if (_smallImageList.Images.ContainsKey(ext))
            {
                return _smallImageList.Images.IndexOfKey(ext);
            }
            else  //如果不存在就添加
            {
                SHGetFileInfo(FileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);

                Icon smallIcon;

                try
                {
                    smallIcon = Icon.FromHandle(shinfo.hIcon);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException(String.Format("File \"{0}\" doesn not exist!", FileName), ex);
                }

                _smallImageList.Images.Add(ext, smallIcon); //添加小图标

                //大图标
                SHGetFileInfo(FileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

                Icon largeIcon = Icon.FromHandle(shinfo.hIcon);
                _largeImageList.Images.Add(ext, largeIcon);

                return _smallImageList.Images.Count - 1;

            }


        }



        #endregion

        /// <summary>
        /// 通过扩展名得到描述
        /// </summary>
        /// <param name="ext">扩展名，如.jpg</param>
        /// <param name="description">返回描述</param>
        public void GetExtsDescription(string ext, out string description)
        {
            description = "";

            //从注册表中读取扩展名相应的子键  
            RegistryKey extsubkey = Registry.ClassesRoot.OpenSubKey(ext);
            if (extsubkey == null)  //没有找到
            {
                //如果没有找到，那就是这种类型

                description = ext.ToUpper().Substring(1) + "文件";

                return;
            }

            //取出扩展名对应的文件类型名称  
            string extdefaultvalue = extsubkey.GetValue(null) as string;
            if (extdefaultvalue == null)
            {
                return;
            }

            //扩展名类型是可执行文件
            if (extdefaultvalue.Equals("exefile", StringComparison.InvariantCultureIgnoreCase))
            {
                //从注册表中读取文件类型名称的相应子键  
                RegistryKey exefilesubkey = Registry.ClassesRoot.OpenSubKey(extdefaultvalue);
                if (exefilesubkey != null)  //如果不为空
                {
                    string exefiledescription = exefilesubkey.GetValue(null) as string;   //得到exefile描述字符串  
                    if (exefiledescription != null)
                    {
                        description = exefiledescription;
                    }

                }
                return;
            }


            //从注册表中读取文件类型名称的相应子键  
            RegistryKey typesubkey = Registry.ClassesRoot.OpenSubKey(extdefaultvalue);
            if (typesubkey == null)
            {
                return;
            }

            //得到类型描述字符串  
            string typedescription = typesubkey.GetValue(null) as string;
            if (typedescription != null)
            {
                description = typedescription;
            }
        }


    }
}
