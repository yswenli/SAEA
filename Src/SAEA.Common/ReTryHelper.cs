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
*文件名： ReTryHelper
*版本号： v26.4.23.1
*唯一标识：6c43c02f-9ace-4c73-9f3a-01d5937b3f82
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：ReTryHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.Common
{
    /// <summary>
    /// 重试辅助类
    /// </summary>
    public static class ReTryHelper
    {
        /// <summary>
        /// 执行操作，失败后重试，最终失败时静默返回不抛出异常
        /// </summary>
        /// <param name="action">待执行的操作</param>
        /// <param name="retry">期望的重试次数，当前实现固定最多执行 5 次</param>
        public static void Do(Action action, int retry = 5)
        {
            int error = 0;
            while (error < 5)
            {
                try
                {
                    action.Invoke();
                    break;
                }
                catch
                {
                    error++;
                }
            }
        }
        /// <summary>
        /// 重试
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <param name="retry"></param>
        /// <returns></returns>
        public static T Do<T>(Func<T> action, int retry = 5)
        {
            var t = default(T);
            int error = 0;
            Exception exception = null;
            while (error < 5)
            {
                try
                {
                    t = action.Invoke();
                    exception = null;
                    break;
                }
                catch (Exception ex)
                {
                    error++;
                    exception = ex;
                }
            }

            if (exception != null) throw exception;

            return t;
        }
    }
}