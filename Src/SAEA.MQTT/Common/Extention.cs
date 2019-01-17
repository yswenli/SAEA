/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：Extention
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:20:01
*描述：
*=====================================================================
*修改时间：2019/1/14 19:20:01
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Model;
using System;

namespace SAEA.MQTT.Common
{
    public static class Extention
    {
        public static int GetPort(this MqttClientTcpOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.Port.HasValue)
            {
                return options.Port.Value;
            }

            return !options.TlsOptions.UseTls ? 1883 : 8883;
        }


        public static byte[] ToArray(this ArraySegment<byte> source)
        {
            if (source.Array == null)
            {
                return null;
            }

            var buffer = new byte[source.Count];
            if (buffer.Length > 0)
            {
                Array.Copy(source.Array, source.Offset, buffer, 0, buffer.Length);
            }

            return buffer;
        }
    }
}
