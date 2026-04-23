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
*命名空间：SAEA.MVC.Tool.CodeGenerte
*文件名： CSharpTemple
*版本号： v26.4.23.1
*唯一标识：199d65ba-35c7-4665-8495-ef13096a80d4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/26 16:34:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/12/26 16:34:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVC.Tool.CodeGenerte
{
    public static class CSharpTemple
    {
        public const string TEMPLE = @"//
//此代码为SAEA.MVC.APISdkCodeGenerator于[[DateTime]]生成，请尽量不要修改
//
using SAEA.Common;
using System;

namespace SAEA.MVC.Tool.CodeGenerte
{
    /// <summary>
    /// SaeaApiSdk
    /// </summary>
    public class SaeaApiSdk
    {
        string _url = """";

        /// <summary>
        /// SaeaApiSdk
        /// </summary>
        /// <param name=""url""></param>
        public SaeaApiSdk(string url)
        {
            _url = url;
        }
[[Method]]
    }
}
";
        public const string MethodTemple1 = @"        /// <summary>
        /// [[Controller]]/[[Action]]
        /// </summary>
        /// <param name=""sucess""></param>
        /// <param name=""error""></param>
        public void [[Controller]][[Action]][[Type]](Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($""{_url}api/[[Controller]]/[[Action]]"","""",""[[Type]]"");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }";

        public const string MethodTemple2 = @"        /// <summary>
        /// [[Controller]]/[[Action]]
        /// </summary>
        /// <param name=""sucess""></param>
        /// <param name=""error""></param>
        public void [[Controller]][[Action]][[Type]]([[Inputs1]]Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($""{_url}api/[[Controller]]/[[Action]]"",[[Inputs2]],""[[Type]]"");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }";


        public const string MethodTemple3 = @"        /// <summary>
        /// [[Controller]]/[[Action]]
        /// </summary>
        /// <param name=""sucess""></param>
        /// <param name=""error""></param>
        public void [[Controller]][[Action]][[Type]]([[Inputs1]],WebHeaderCollection headers,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($""{_url}api/[[Controller]]/[[Action]]"",[[Inputs2]],""[[Type]]"",headers);
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }";

    }
}
