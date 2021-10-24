/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Protocol
*类 名 称：RecordType
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
namespace SAEA.DNS.Protocol 
{
    /// <summary>
    /// 数据类型
    /// </summary>
    public enum RecordType
    {
        /// <summary>
        /// 由域名获得IPv4地址
        /// </summary>
        A = 1,
        /// <summary>
        /// 查询域名服务器
        /// </summary>
        NS = 2,
        /// <summary>
        /// 查询规范名称
        /// </summary>
        CNAME = 5,
        /// <summary>
        /// 开始授权
        /// </summary>
        SOA = 6,
        /// <summary>
        /// 熟知服务
        /// </summary>
        WKS = 11,
        /// <summary>
        /// 把IP地址转换成域名
        /// </summary>
        PTR = 12,
        /// <summary>
        /// 主机信息
        /// </summary>
        MX = 15,
        /// <summary>
        /// 邮件交换
        /// </summary>
        TXT = 16,
        /// <summary>
        /// 由域名获得IPv6地址
        /// </summary>
        AAAA = 28,
        /// <summary>
        /// 服务定位（SRV）资源记录
        /// </summary>
        SRV = 33,

        OPT = 41,
        /// <summary>
        /// 传送整个区的请求
        /// </summary>
        AXFR = 252,
        /// <summary>
        /// 对所有记录的请求
        /// </summary>
        ANY = 255,
    }
}
