/****************************************************************************
*项目名称：SAEA.MVC.Tool.CodeGenerte
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC.Tool.CodeGenerte
*类 名 称：CSharpTemple
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/26 11:06:12
*描述：
*=====================================================================
*修改时间：2020/12/26 11:06:12
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
