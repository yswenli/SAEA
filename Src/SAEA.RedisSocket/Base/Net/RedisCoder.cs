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
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RedisCoder
*版本号： v26.4.23.1
*唯一标识：9091f8bb-8eea-4d9b-a41f-f357027cf6e8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：RedisCoder接口
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisCoder接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Sockets.Interface;

namespace SAEA.RedisSocket.Base.Net
{
    /// <summary>
    /// 通信数据接收解析器
    /// </summary>
    public sealed class RedisCoder : ICoder
    {
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 收包处理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OnHeart"></param>
        /// <param name="onFile"></param>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {

        }
    }
}