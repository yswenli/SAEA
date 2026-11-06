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
*文件名： IMessageEntry
*版本号： v26.4.23.1
*唯一标识：8210481b-e133-4cae-af7f-4455a34dc806
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：IMessageEntry类
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
    /// DNS 消息条目通用契约，问题与资源记录共用
    /// </summary>
    public interface IMessageEntry
    {
        /// <summary>
        /// 条目域名
        /// </summary>
        Domain Name { get; }

        /// <summary>
        /// 记录类型
        /// </summary>
        RecordType Type { get; }

        /// <summary>
        /// 记录类别
        /// </summary>
        RecordClass Class { get; }

        /// <summary>
        /// 序列化后的字节数
        /// </summary>
        int Size { get; }

        /// <summary>
        /// 序列化为报文字节
        /// </summary>
        /// <returns>条目字节</returns>
        byte[] ToArray();
    }
}
