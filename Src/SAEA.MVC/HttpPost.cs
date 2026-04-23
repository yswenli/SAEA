/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： HttpPost
*版本号： v26.4.23.1
*唯一标识：e00bb57f-e3ee-4efe-a7cf-f23db767c1d0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/4/10 16:43:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:43:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.MVC
{
    /// <summary>
    /// 标记方法为POST
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpPost : Attribute, IFilter
    {
        public int Order { get; set; }
    }
}