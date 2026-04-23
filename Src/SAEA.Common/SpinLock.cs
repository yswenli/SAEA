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
*唯一标识：e5544a65-0b91-4044-a5fe-28a8eb375c6a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/22 17:34:07
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/08/22 17:34:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System.Diagnostics;
using System.Threading;

namespace SAEA.Common
{
    public class SpinLock
    {
        int _signal = 0;

        Stopwatch _stopwatch;

        public SpinLock()
        {
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

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

        public void Set()
        {
            Interlocked.Exchange(ref _signal, 0);
            _stopwatch.Stop();
        }
    }
}
