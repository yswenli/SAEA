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
*命名空间：SAEA.RedisSocket.Model
*文件名： ResponseType
*版本号： v26.4.23.1
*唯一标识：597d647d-5b02-45dc-94aa-d9114f847061
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/20 10:04:58
*描述：ResponseType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis 应答类型，决定 Data 与 Entity 的解析方式
    /// </summary>
    public enum ResponseType : byte
    {
        /// <summary>
        /// 状态类应答成功，如 OK、PONG
        /// </summary>
        OK = 1,
        /// <summary>
        /// 服务端返回的错误应答
        /// </summary>
        Error = 2,
        /// <summary>
        /// 整数应答
        /// </summary>
        Value = 3,
        /// <summary>
        /// 空应答，如 nil 或空数组
        /// </summary>
        Empty = 4,
        /// <summary>
        /// 单行字符串应答
        /// </summary>
        String = 5,
        /// <summary>
        /// 多行数组应答
        /// </summary>
        Lines = 6,
        /// <summary>
        /// 键值成对出现的数组应答
        /// </summary>
        KeyValues = 7,
        /// <summary>
        /// 订阅频道推送的消息
        /// </summary>
        Sub = 8,
        /// <summary>
        /// 取消订阅后的确认应答
        /// </summary>
        UnSub = 9,
        /// <summary>
        /// 未能识别的应答
        /// </summary>
        Undefined = 10,
        /// <summary>
        /// 集群重定向应答，Data 为新节点的 ip:port
        /// </summary>
        Redirect = 11,
    }
}