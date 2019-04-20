/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： QueueResult
*版本号： v4.3.3.7
*唯一标识：bfbbfbe7-acd9-4f83-9f6a-e03890205a5b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/8 16:08:47
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/8 16:08:47
*修改人： yswenli
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Model
{
    /// <summary>
    /// 队列编辑消息实体
    /// </summary>
    public class QueueResult
    {
        public QueueSocketMsgType Type { get; set; }

        public string Name { get; set; }

        public string Topic { get; set; }

        public string Data { get; set; }
    }
}
