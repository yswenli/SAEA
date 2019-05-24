/****************************************************************************
*项目名称：SAEA.Mongo.Shared
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Shared
*类 名 称：MaxTimeHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/24 14:11:41
*描述：
*=====================================================================
*修改时间：2019/5/24 14:11:41
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Threading;

namespace SAEA.Mongo.Shared
{
    internal static class MaxTimeHelper
    {
        public static int ToMaxTimeMS(TimeSpan value)
        {
            if (value == Timeout.InfiniteTimeSpan)
            {
                return 0;
            }
            else if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            else
            {
                return (int)Math.Ceiling(value.TotalMilliseconds);
            }
        }
    }
}
