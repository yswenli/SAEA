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
*命名空间：SAEA.Common
*文件名： TargetFrameworkInfoProvider
*版本号： v26.4.23.1
*唯一标识：6b2411b2-94c2-4a17-b5b8-aac44bd9979c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/16 15:31:19
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/01/16 15:31:19
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
#elif NET462
                return "net462";
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
#elif NETSTANDARD3_1
                return "netstandard3.1";
#elif WINDOWS_UWP
                return "uap10.0";
#endif
            }
        }
    }
}
