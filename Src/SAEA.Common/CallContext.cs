/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：CallContext
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 自定义上下文容器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class CallContext<T>
    {
        static ConcurrentDictionary<string, AsyncLocal<T>> state = new ConcurrentDictionary<string, AsyncLocal<T>>();

        /// <summary>
        /// 保存上下文对象
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void SetData(string name, T data) =>
            state.GetOrAdd(name, _ => new AsyncLocal<T>()).Value = data;

        /// <summary>
        /// 获取上下文对象
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetData(string name) =>
            state.TryGetValue(name, out AsyncLocal<T> data) ? data.Value : default(T);
    }
}
