/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Net
*文件名： QCoder
*版本号： V1.0.0.0
*唯一标识：88f5a779-8294-47bc-897b-8357a09f2fdb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 18:01:56
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 18:01:56
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Commom;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.QueueSocket.Net
{
    public sealed class QCoder : ICoder
    {
        public const int P_LEN = 4;

        public const int P_Type = 1;

        public const int P_Head = 5;

        private List<byte> _buffer = new List<byte>();

        private object _locker = new object();

        const string ENTER = "\r\n";

        long _position = 0;

        public void Pack(byte[] data, Action<DateTime> onHeart, Action<ISocketProtocal> onUnPackage, Action<byte[]> onFile)
        {

        }

        /// <summary>
        /// 队列编解码器
        /// </summary>
        public QueueCoder QueueCoder { get; set; } = new QueueCoder();


        private bool _isBegin = false;


        /// <summary>
        /// 包解析
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OnQueueResult"></param>
        public void GetQueueResult(byte[] data, Action<QueueResult> OnQueueResult)
        {
            lock (_locker)
            {
                _buffer.AddRange(data);

                if (_buffer.Count > (1 + 4 + 4 + 0 + 4 + 0 + 0))
                {
                    var buffer = _buffer.ToArray();
                    var offset = 0;
                    var list = QueueSocketMsg.Parse(buffer, out offset);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            OnQueueResult?.Invoke(new QueueResult()
                            {
                                Type = (QueueSocketMsgType)item.Type,
                                Name = item.Name,
                                Topic = item.Topic,
                                Data = item.Data
                            });
                        }
                        _buffer.RemoveRange(0, offset);
                    }
                }
            }
        }



        public void Dispose()
        {
            _isBegin = false;
            _buffer.Clear();
            _buffer = null;
        }



    }
}
