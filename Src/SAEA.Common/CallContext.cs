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
*文件名： CallContext
*版本号： v26.4.23.1
*唯一标识：d0521ef6-7268-432d-8062-dddffda70e99
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/05 16:44:13
*描述：CallContext上下文类
*
*=====================================================================
*修改标记
*修改时间：2019/11/05 16:44:13
*修改人： yswenli
*版本号： v26.4.23.1
*描述：CallContext上下文类
*
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
