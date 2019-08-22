/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：SpinLock
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/22 16:10:08
*描述：
*=====================================================================
*修改时间：2019/8/22 16:10:08
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System.Threading;

namespace SAEA.Common
{
    public class SpinLock
    {
        int signal = 0;

        public SpinLock()
        {

        }

        public void WaitOne()
        {
            while (Interlocked.Exchange(ref signal, 1) != 0)
            {

            }
        }

        public void Set()
        {
            Interlocked.Exchange(ref signal, 0);
        }
    }
}
