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
*命名空间：SAEA.DNS.Protocol
*文件名： RecordType
*版本号： v26.4.23.1
*唯一标识：d8090445-122d-4343-b968-86288e68b050
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
