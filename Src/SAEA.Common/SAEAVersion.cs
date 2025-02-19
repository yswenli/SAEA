/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： SAEAVersion
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 版本类，包含版本信息和相关元数据
    /// </summary>
    public static class SAEAVersion
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public const string version = "djcuMjUuMi4xOQ==";

        /// <summary>
        /// 标题，Base64编码
        /// </summary>
        public const string title = "ICBfX19fICAgICBfICAgICBfX19fXyAgICAgXyAgICAgICAgX19fXyAgICAgICAgICAgICAgICBfICAgICAgICAgIF8gICAgICAgIA0KIC8gX19ffCAgIC8gXCAgIHwgX19fX3wgICAvIFwgICAgICAvIF9fX3wgICBfX18gICAgX19fIHwgfCBfXyBfX18gfCB8XyAgX19fIA0KIFxfX18gXCAgLyBfIFwgIHwgIF98ICAgIC8gXyBcICAgICBcX19fIFwgIC8gXyBcICAvIF9ffHwgfC8gLy8gXyBcfCBfX3wvIF9ffA0KICBfX18pIHwvIF9fXyBcIHwgfF9fXyAgLyBfX18gXCAgXyAgX19fKSB8fCAoXykgfHwgKF9fIHwgICA8fCAgX18vfCB8XyBcX18gXA0KIHxfX19fLy9fLyAgIFxfXHxfX19fX3wvXy8gICBcX1woXyl8X19fXy8gIFxfX18vICBcX19ffHxffFxfXFxfX198IFxfX3x8X19fLw0KICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIA==";


        /// <summary>
        /// 标记，Base64编码
        /// </summary>
        public const string author = "Q29weXJpZ2h0IChjKSB5c3dlbmxpIEFsbCBSaWdodHMgUmVzZXJ2ZWQ=";

        /// <summary>
        /// 控制台标题
        /// </summary>
        public static string MarkText
        {
            get
            {
                return $"{title.FromBase64String()}{Environment.NewLine}{version.FromBase64String().PadLeft(75)}{Environment.NewLine}{author.FromBase64String().PadLeft(75)}{Environment.NewLine}{Environment.NewLine}";
            }
        }

        /// <summary>
        /// 获取版本号
        /// </summary>
        /// <returns>版本号字符串</returns>
        public static new string ToString()
        {
            return version;
        }
    }
}
