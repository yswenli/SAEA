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
*命名空间：SAEA.FileSocket.Model
*文件名： FileMessage
*版本号： v26.4.23.1
*唯一标识：22704fb3-4f34-4588-8c86-c8c65a39319c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using ProtoBuf;
using System;

namespace SAEA.FileSocket.Model
{
    /// <summary>
    /// 系统默认文件消息体
    /// </summary>
    [ProtoContract]
    public sealed class FileMessage
    {
        [ProtoMember(1)]
        public string FileName
        {
            get; set;
        }

        [ProtoMember(2)]
        public long Length
        {
            get; set;
        }

        [ProtoMember(3)]
        public long Offset
        {
            get; set;
        }
    }
}