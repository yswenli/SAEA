/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：MemoryPoolStatistics
*版本号： v26.4.23.1
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/18
*描述：
*=====================================================================
*修改时间：2026/4/18
*修 改 人： yswenli
*版本号： v26.4.23.1
*描    述：
*****************************************************************************/

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 内存池统计信息
    /// </summary>
    public struct MemoryPoolStatistics
    {
        /// <summary>
        /// 小型池租用计数
        /// </summary>
        public long SmallPoolRented { get; set; }

        /// <summary>
        /// 小型池归还计数
        /// </summary>
        public long SmallPoolReturned { get; set; }

        /// <summary>
        /// 中型池租用计数
        /// </summary>
        public long MediumPoolRented { get; set; }

        /// <summary>
        /// 中型池归还计数
        /// </summary>
        public long MediumPoolReturned { get; set; }

        /// <summary>
        /// 大型池租用计数
        /// </summary>
        public long LargePoolRented { get; set; }

        /// <summary>
        /// 大型池归还计数
        /// </summary>
        public long LargePoolReturned { get; set; }

        /// <summary>
        /// 总租用计数
        /// </summary>
        public long TotalRented => SmallPoolRented + MediumPoolRented + LargePoolRented;

        /// <summary>
        /// 总归还计数
        /// </summary>
        public long TotalReturned => SmallPoolReturned + MediumPoolReturned + LargePoolReturned;

        /// <summary>
        /// 当前活动缓冲区数量
        /// </summary>
        public long ActiveBuffers => TotalRented - TotalReturned;
    }
}
