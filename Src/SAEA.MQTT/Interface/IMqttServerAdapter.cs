/****************************************************************************
*项目名称：SAEA.MQTT.Interface
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Interface
*类 名 称：IMqttServerAdapter
*版 本 号： v4.3.3.7
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:31:43
*描述：
*=====================================================================
*修改时间：2019/1/15 10:31:43
*修 改 人： yswenli
*版 本 号： v4.3.3.7
*描    述：
*****************************************************************************/
using SAEA.MQTT.Event;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTT.Interface
{
    public interface IMqttServerAdapter : IDisposable
    {
        event EventHandler<MqttServerAdapterClientAcceptedEventArgs> ClientAccepted;

        Task StartAsync(IMqttServerOptions options);
        Task StopAsync();
    }
}
