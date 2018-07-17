/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： ReTryHelper
*版本号： V1.0.0.0
*唯一标识：f32b02a9-2f2b-4b59-9dd6-08892e1c5231
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/29 15:14:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/29 15:14:57
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.Common
{
    /// <summary>
    /// 重试辅助类
    /// </summary>
    public static class ReTryHelper
    {
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
