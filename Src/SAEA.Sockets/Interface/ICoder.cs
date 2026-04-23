/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 

  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets
*文件名： IUnpacker
*版本号： v26.4.23.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 通信数编码器
    /// </summary>
    public interface ICoder
    {
        /// <summary>
        /// 编码方法，将ISocketProtocal对象编码为字节数组
        /// </summary>
        /// <param name="protocal">ISocketProtocal对象</param>
        /// <returns>编码后的字节数组</returns>
        byte[] Encode(ISocketProtocal protocal);

        /// <summary>
        /// 解码方法，将字节数组解码为ISocketProtocal对象列表
        /// </summary>
        /// <param name="data">待解码的字节数组</param>
        /// <param name="onHeart">心跳包处理回调</param>
        /// <param name="onFile">文件包处理回调</param>
        /// <returns>解码后的ISocketProtocal对象列表</returns>
        List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null);

        /// <summary>
        /// 清除方法，清除编码器内部状态
        /// </summary>
        void Clear();
    }
}