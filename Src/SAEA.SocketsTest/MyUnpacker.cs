/****************************************************************************
*项目名称：SAEA.SocketsTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest
*类 名 称：MyUnpacker
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/20 10:55:52
*描述：
*=====================================================================
*修改时间：2019/8/20 10:55:52
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using JT808.Protocol.Interfaces;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SAEA.SocketsTest
{

    partial class MyUnpacker : IUnpacker
    {
        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }

        public void Clear()
        {
            _cache.Clear();
        }


        JT808Serializer _JT808Serializer;

        public MyUnpacker()
        {
            IJT808Config jT808Config = new DefaultGlobalConfig();
            jT808Config.Register(Assembly.GetExecutingAssembly());
            _JT808Serializer = new JT808Serializer(jT808Config);
        }

        List<byte> _cache = new List<byte>();

        /// <summary>
        /// 自定义
        /// </summary>
        /// <param name="data"></param>
        /// <param name="unpackCallback"></param>
        public void Unpack(byte[] data, Action<JT808Package> unpackCallback)
        {
            _cache.AddRange(data);

            var span = _cache.ToArray().AsSpan();

            var package = _JT808Serializer.Deserialize(span);

            if (package != null)
            {
                unpackCallback?.Invoke(package);

                _cache.Clear();
            }
        }

        class DefaultGlobalConfig : GlobalConfigBase
        {
            public override string ConfigId => "default";
        }
    }
}
