/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Base.Net
*文件名： RMessage
*版本号： V3.3.3.3
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： V3.3.3.3
*描述：
*
*****************************************************************************/

using SAEA.Sockets.Interface;


namespace SAEA.RedisSocket.Base.Net
{
    class RMessage : ISocketProtocal
    {
        public long BodyLength { get; set; }
        public byte[] Content { get; set; }
        public byte Type { get; set; }

        public byte[] ToBytes()
        {
            return this.Content;
        }
    }
}
