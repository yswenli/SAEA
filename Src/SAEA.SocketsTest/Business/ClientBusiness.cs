/****************************************************************************
*项目名称：SAEA.SocketsTest.Business
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest.Business
*类 名 称：ClientBusiness
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/20 15:01:40
*描述：
*=====================================================================
*修改时间：2019/8/20 15:01:40
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.Extensions;
using JT808.Protocol.MessageBody;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.SocketsTest
{
    //ClientBusiness
    partial class MyUnpacker
    {
        public byte[] Sign(string code)
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = JT808.Protocol.Enums.JT808MsgId.终端鉴权.ToUInt16Value(),
                MsgNum = 126,
                TerminalPhoneNo = "123456789012"
            };

            JT808_0x0102 jT808_0x0200 = new JT808_0x0102
            {
                Code = code
            };

            jT808Package.Bodies = jT808_0x0200;

            return _JT808Serializer.Serialize(jT808Package);
        }


        public byte[] ReportPosition()
        {
            JT808Package jT808Package = new JT808Package();

            jT808Package.Header = new JT808Header
            {
                MsgId = JT808.Protocol.Enums.JT808MsgId.位置信息汇报.ToUInt16Value(),
                MsgNum = 126,
                TerminalPhoneNo = "123456789012"
            };

            JT808_0x0200 jT808_0x0200 = new JT808_0x0200
            {
                AlarmFlag = 1,
                Altitude = 40,
                GPSTime = DateTime.Parse("2018-10-15 10:10:10"),
                Lat = 12222222,
                Lng = 132444444,
                Speed = 60,
                Direction = 0,
                StatusFlag = 2,
                JT808LocationAttachData = new Dictionary<byte, JT808_0x0200_BodyBase>()
            };

            jT808_0x0200.JT808LocationAttachData.Add(JT808Constants.JT808_0x0200_0x01, new JT808_0x0200_0x01
            {
                Mileage = 100
            });

            jT808_0x0200.JT808LocationAttachData.Add(JT808Constants.JT808_0x0200_0x02, new JT808_0x0200_0x02
            {
                Oil = 125
            });

            jT808Package.Bodies = jT808_0x0200;

            return _JT808Serializer.Serialize(jT808Package);
        }
    }
}
