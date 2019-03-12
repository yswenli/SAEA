/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common
*类 名 称：TargetFrameworkInfoProvider
*版 本 号： v4.2.3.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 16:47:29
*描述：
*=====================================================================
*修改时间：2019/1/15 16:47:29
*修 改 人： yswenli
*版 本 号： v4.2.3.1
*描    述：
*****************************************************************************/

namespace SAEA.Common
{
    public static class TargetFrameworkInfoProvider
    {
        public static string TargetFramework
        {
            get
            {
#if NET452
                return "net452";
#elif NET461
                return "net461";
#elif NET472
                return "net472";
#elif NETSTANDARD1_3
                return "netstandard1.3";
#elif NETSTANDARD2_0
                return "netstandard2.0";
#elif NETSTANDARD2_1
                return "netstandard2.1";
#elif NETSTANDARD3_0
                return "netstandard3.0";
#elif WINDOWS_UWP
                return "uap10.0";
#endif
            }
        }
    }
}
