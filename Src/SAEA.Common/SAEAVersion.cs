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
    /// 版本
    /// </summary>
    public static class SAEAVersion
    {
        public const string version = "v7.25.2.10";

        public const string title = "ICBfX19fICAgIF8gICAgX19fX18gICAgXyAgICAgIF9fX18gICAgICAgICAgICAgXyAgICAgICAgXyAgIA0KIC8gX19ffCAgLyBcICB8IF9fX198ICAvIFwgICAgLyBfX198ICBfX18gICBfX198IHwgX19fX198IHxfIA0KIFxfX18gXCAvIF8gXCB8ICBffCAgIC8gXyBcICAgXF9fXyBcIC8gXyBcIC8gX198IHwvIC8gXyBcIF9ffA0KICBfX18pIC8gX19fIFx8IHxfX18gLyBfX18gXCAgIF9fXykgfCAoXykgfCAoX198ICAgPCAgX18vIHxfIA0KIHxfX19fL18vICAgXF9cX19fX18vXy8gICBcX1wgfF9fX18vIFxfX18vIFxfX198X3xcX1xfX198XF9ffA0KIA==";

        public const string author = "eXN3ZW5saQ==";

        public const string mark = "ZGV2ZWxvcCBieSB5c3dlbmxp";

        public static string ConsoleTitle
        {
            get
            {
                return $"{Encoding.UTF8.GetString(Convert.FromBase64String(title))}{Environment.NewLine}{Encoding.UTF8.GetString(Convert.FromBase64String(mark)).PadLeft(60)}{Environment.NewLine}{version.PadLeft(60)}";
            }
        }

        /// <summary>
        /// 获取版本
        /// </summary>
        /// <returns></returns>
        public static new string ToString()
        {
            return version;
        }
    }
}
