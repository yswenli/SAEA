/****************************************************************************
*项目名称：SAEA.SocketsTest.Business
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest.Business
*类 名 称：ServerBusiness
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/20 15:04:14
*描述：
*=====================================================================
*修改时间：2019/8/20 15:04:14
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.Enums;
using JT808.Protocol.Extensions;
using JT808.Protocol.MessageBody;

namespace SAEA.SocketsTest
{
    // ServerBusiness
    partial class MyUnpacker
    {
        public byte[] Process(JT808Package package)
        {
            byte[] result = null;

            switch (package.Header.MsgId)
            {
                case (ushort)JT808MsgId.终端鉴权:
                case (ushort)JT808MsgId.位置信息汇报:

                    result = Response(package.Header.TerminalPhoneNo);
                    break;
                default:
                    throw new System.Exception("未知的类型!");
                    break;
            }

            return result;
        }

        /// <summary>
        /// 平台通用应答
        /// </summary>
        /// <returns></returns>
        public byte[] Response(string phoneno)
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = JT808MsgId.平台通用应答.ToUInt16Value(),
                MsgNum = 126,
                TerminalPhoneNo = phoneno
            };

            JT808_0x8001 jT808_0x0200 = new JT808_0x8001
            {
                MsgNum = 126,
                MsgId = JT808MsgId.平台通用应答.ToUInt16Value(),
                JT808PlatformResult = JT808PlatformResult.成功
            };

            jT808Package.Bodies = jT808_0x0200;

            return _JT808Serializer.Serialize(jT808Package);
        }
    }
}
