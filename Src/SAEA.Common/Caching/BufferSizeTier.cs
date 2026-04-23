/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common.Caching
*类 名 称：BufferSizeTier
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
    /// 缓冲区大小分级
    /// </summary>
    public enum BufferSizeTier
    {
        /// <summary>
        /// 小型缓冲区（小于 4KB）
        /// </summary>
        Small = 0,

        /// <summary>
        /// 中型缓冲区（4KB - 64KB）
        /// </summary>
        Medium = 1,

        /// <summary>
        /// 大型缓冲区（大于 64KB）
        /// </summary>
        Large = 2
    }
}
