/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： V3.3.3.4
*唯一标识：ab912b9a-c7ed-44d9-8e48-eef0b6ff86a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:11:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:11:15
*修改人： yswenli
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http.Base.Net
{
    class HttpSocket : BaseServerSocket
    {
        public event Action<IUserToken, HttpMessage> OnRequested;


        public HttpSocket(int bufferSize = 1024 * 10, int count = 10000) : base(new HContext(), bufferSize, count)
        {
            
        }

        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            HUnpacker unpacker = (HUnpacker)userToken.Unpacker;            

            unpacker.GetRequest(data, (result) =>
            {
                OnRequested?.Invoke(userToken, result);
            });
        }

        /// <summary>
        /// 发送消息并结束会话
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        public void End(IUserToken userToken, byte[] data)
        {
            base.End(userToken, data);
        }
    }
}
