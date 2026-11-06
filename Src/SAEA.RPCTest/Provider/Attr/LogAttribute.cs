/****************************************************************************
*项目名称：SAEA.RPCTest.Provider.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RPCTest.Provider.Model
*类 名 称：LogAttribute
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/6/17 11:05:29
*描述：
*=====================================================================
*修改时间：2019/6/17 11:05:29
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.RPC;
using SAEA.Sockets.Interface;
using System;
using System.Diagnostics;

namespace SAEA.RPCTest.Provider.Attr
{
    /// <summary>
    /// RPC 方法执行日志过滤器，输出服务名、方法名与执行耗时
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LogAttribute : ActionFilterAtrribute
    {
        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// 构造函数，设置执行顺序并启动计时器
        /// </summary>
        /// <param name="argsNames">需记录的参数名称，当前实现未使用</param>
        public LogAttribute(params string[] argsNames)
        {
            Order = 0;
            stopwatch.Start();
        }


        /// <summary>
        /// 方法执行前重置计时器
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="args">调用参数</param>
        /// <returns>固定返回 true，表示继续执行目标方法</returns>
        public override bool OnActionExecuting(IUserToken userToken, string serviceName, string methodName, object[] args)
        {
            stopwatch.Reset();

            return true;
        }

        /// <summary>
        /// 方法执行后输出服务名、方法名与耗时（毫秒）
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="args">调用参数</param>
        /// <param name="result">方法返回值</param>
        public override void OnActionExecuted(IUserToken userToken, string serviceName, string methodName, object[] args, object result)
        {
            Console.WriteLine($"SAEA.RPCTest.LogAttribute.{serviceName}.{methodName} taking {stopwatch.ElapsedMilliseconds} ms");
        }

    }
}
