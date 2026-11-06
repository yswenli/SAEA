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
*文件名： SpinLock
*版本号： v26.4.23.1
*唯一标识：9e3b9b7e-f8cb-4c07-b130-bec52ba09b5f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/22 17:34:07
*描述：SpinLock接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Diagnostics;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 基于 Interlocked 自旋的轻量锁，同一实例不适用于多线程并发等待
    /// </summary>
    public class SpinLock
    {
        int _signal = 0;

        Stopwatch _stopwatch;

        /// <summary>
        /// 创建自旋锁并启动内部计时器
        /// </summary>
        public SpinLock()
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        /// <summary>
        /// 自旋等待获取锁，超时后释放并直接返回，不抛出异常
        /// </summary>
        /// <param name="timeOut">超时毫秒数</param>
        public void WaitOne(int timeOut = 5000)
        {
            _stopwatch.Restart();
            while (Interlocked.Exchange(ref _signal, 1) != 0)
            {
                if(_stopwatch.ElapsedMilliseconds > timeOut)
                {
                    Set();
                    _stopwatch.Stop();
                    break;
                }
            }
        }

        /// <summary>
        /// 释放锁并停止计时
        /// </summary>
        public void Set()
        {
            Interlocked.Exchange(ref _signal, 0);
            _stopwatch.Stop();
        }
    }
}
