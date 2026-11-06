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
*文件名： ResponseCode
*版本号： v26.4.23.1
*唯一标识：362a6ae3-c930-4db3-bf96-1fd7211569d4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：ResponseCode代码枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// DNS 响应码
    /// </summary>
    public enum ResponseCode
    {
        /// <summary>
        /// 无错误
        /// </summary>
        NoError = 0,
        /// <summary>
        /// 报文格式错误
        /// </summary>
        FormatError,
        /// <summary>
        /// 服务器内部错误
        /// </summary>
        ServerFailure,
        /// <summary>
        /// 查询的域名不存在
        /// </summary>
        NameError,
        /// <summary>
        /// 服务器不支持该查询
        /// </summary>
        NotImplemented,
        /// <summary>
        /// 服务器拒绝执行
        /// </summary>
        Refused,
        /// <summary>
        /// 域名已存在
        /// </summary>
        YXDomain,
        /// <summary>
        /// 资源记录集已存在
        /// </summary>
        YXRRSet,
        /// <summary>
        /// 资源记录集不存在
        /// </summary>
        NXRRSet,
        /// <summary>
        /// 服务器对查询区域无权威
        /// </summary>
        NotAuth,
        /// <summary>
        /// 查询不在指定区域内
        /// </summary>
        NotZone,
    }
}
