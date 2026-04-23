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
*命名空间：SAEA.Common.Caching
*文件名： MemoryPoolStatistics
*版本号： v26.4.23.1
*唯一标识：d8be140e-0da4-4a19-ab10-55a4b3a34f0d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/18 22:44:32
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/18 22:44:32
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
