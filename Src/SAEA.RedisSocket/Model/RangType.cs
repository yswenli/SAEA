/****************************************************************************
*项目名称：SAEA.RedisSocket.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Model
*类 名 称：RangType
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/16 10:59:37
*描述：
*=====================================================================
*修改时间：2019/8/16 10:59:37
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 取值范围
    /// </summary>
    public enum RangType
    {
        None = 0,
        IncludeLeft = 1,
        InCludeRight = 2,
        Both = 3
    }
}
