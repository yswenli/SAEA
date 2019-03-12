/****************************************************************************
*项目名称：SAEA.MQTT.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common
*类 名 称：ExceptionHelper
*版 本 号： v4.2.3.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:25:19
*描述：
*=====================================================================
*修改时间：2019/1/15 15:25:19
*修 改 人： yswenli
*版 本 号： v4.2.3.1
*描    述：
*****************************************************************************/
using SAEA.MQTT.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common
{
    public static class ExceptionHelper
    {
        public static void ThrowGracefulSocketClose()
        {
            throw new MqttCommunicationClosedGracefullyException();
        }

        public static void ThrowIfGracefulSocketClose(int readBytesCount)
        {
            if (readBytesCount <= 0)
            {
                throw new MqttCommunicationClosedGracefullyException();
            }
        }
    }
}
