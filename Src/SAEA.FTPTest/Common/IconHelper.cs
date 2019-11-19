/****************************************************************************
*项目名称：SAEA.FTPTest.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Common
*类 名 称：IconHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/19 13:47:41
*描述：
*=====================================================================
*修改时间：2019/11/19 13:47:41
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.FTPTest.Common
{
    public class IconHelper
    {
        [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        public static extern uint ExtractIconExW([System.Runtime.InteropServices.InAttribute()] [System.Runtime.InteropServices.MarshalAsAttribute(UnmanagedType.LPWStr)] string lpszFile, int nIconIndex, ref IntPtr phiconLarge, ref IntPtr phiconSmall, uint nIcons);

        /// <summary> 
        /// 通过扩展名得到图标和描述 
        /// </summary> 
        /// <param name="ext">扩展名</param> 
        /// <param name="LargeIcon">得到大图标</param> 
        /// <param name="smallIcon">得到小图标</param> 
        public static void GetExtsIconAndDescription(string ext, out Icon largeIcon, out Icon smallIcon, out string description)
        {
            largeIcon = smallIcon = null;
            description = "";
            var extsubkey = Registry.ClassesRoot.OpenSubKey(ext); //从注册表中读取扩展名相应的子键 
            if (extsubkey != null)
            {
                var extdefaultvalue = (string)extsubkey.GetValue(null); //取出扩展名对应的文件类型名称 

                //未取到值，返回预设图标 
                if (extdefaultvalue == null)
                {
                    GetDefaultIcon(out largeIcon, out smallIcon);
                    return;
                }

                var typesubkey = Registry.ClassesRoot.OpenSubKey(extdefaultvalue); //从注册表中读取文件类型名称的相应子键 
                if (typesubkey != null)
                {
                    description = (string)typesubkey.GetValue(null); //得到类型描述字符串 
                    var defaulticonsubkey = typesubkey.OpenSubKey("DefaultIcon"); //取默认图标子键 
                    if (defaulticonsubkey != null)
                    {
                        //得到图标来源字符串 
                        var defaulticon = (string)defaulticonsubkey.GetValue(null); //取出默认图标来源字符串 
                        var iconstringArray = defaulticon.Split(',');
                        int nIconIndex = 0;
                        if (iconstringArray.Length > 1) int.TryParse(iconstringArray[1], out nIconIndex);
                        //得到图标 
                        System.IntPtr phiconLarge = new IntPtr();
                        System.IntPtr phiconSmall = new IntPtr();
                        ExtractIconExW(iconstringArray[0].Trim('"'), nIconIndex, ref phiconLarge, ref phiconSmall, 1);
                        if (phiconLarge.ToInt32() > 0) largeIcon = Icon.FromHandle(phiconLarge);
                        if (phiconSmall.ToInt32() > 0) smallIcon = Icon.FromHandle(phiconSmall);
                    }
                }
            }
        }
        /// <summary> 
        /// 获取缺省图标 
        /// </summary> 
        /// <param name="largeIcon"></param> 
        /// <param name="smallIcon"></param> 
        public static void GetDefaultIcon(out Icon largeIcon, out Icon smallIcon)
        {
            largeIcon = smallIcon = null;
            System.IntPtr phiconLarge = new IntPtr();
            System.IntPtr phiconSmall = new IntPtr();
            ExtractIconExW(Path.Combine(Environment.SystemDirectory, "shell32.dll"), 0, ref phiconLarge, ref phiconSmall, 1);
            if (phiconLarge.ToInt32() > 0) largeIcon = Icon.FromHandle(phiconLarge);
            if (phiconSmall.ToInt32() > 0) smallIcon = Icon.FromHandle(phiconSmall);
        }

        /// <summary>
        /// 获取文件图标
        /// </summary>
        /// <param name="fileType"></param>
        /// <param name="large"></param>
        /// <param name="small"></param>
        public static void GetFileIcon(string fileType, out Icon large, out Icon small)
        {
            string des;

            if (fileType.Trim() == "") //预设图标
            {
                GetDefaultIcon(out large, out small);
            }
            else if (fileType.ToUpper() == ".EXE") //应用程序图标单独获取
            {
                IntPtr l = IntPtr.Zero;
                IntPtr s = IntPtr.Zero;

                ExtractIconExW(Path.Combine(Environment.SystemDirectory, "shell32.dll"), 2, ref l, ref s, 1);

                large = Icon.FromHandle(l);
                small = Icon.FromHandle(s);
            }
            else //其它类型的图标
            {
                GetExtsIconAndDescription(fileType, out large, out small, out des);
            }

            if ((large == null) || (small == null)) //无法获取图标,预设图标
                GetDefaultIcon(out large, out small);
        }


        /// <summary>
        /// 通过扩展名得到描述
        /// </summary>
        /// <param name="ext">扩展名，如.jpg</param>
        /// <param name="description">返回描述</param>
        public static void GetExtsDescription(string ext, out string description)
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
