/****************************************************************************
*项目名称：SAEA.RPCTest.Provider.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RPCTest.Provider.Model
*类 名 称：LogAttribute
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/17 11:05:29
*描述：
*=====================================================================
*修改时间：2019/6/17 11:05:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.RPC;
using SAEA.Sockets.Interface;
using System;
using System.Diagnostics;

namespace SAEA.RPCTest.Provider.Model
{
    /// <summary>
    /// SAEA.RPC LogAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LogAttribute : ActionFilterAtrribute
    {
        Stopwatch stopwatch = new Stopwatch();

        public LogAttribute(params string[] argsNames)
        {
            Order = 0;
            stopwatch.Start();
        }


        public override bool OnActionExecuting(IUserToken userToken, string serviceName, string methodName, object[] args)
        {
            stopwatch.Reset();

            return true;
        }

        public override void OnActionExecuted(IUserToken userToken, string serviceName, string methodName, object[] args, object result)
        {
            Console.WriteLine($"SAEA.RPCTest.LogAttribute.{serviceName}.{methodName} taking {stopwatch.ElapsedMilliseconds} ms");
        }

    }
}
