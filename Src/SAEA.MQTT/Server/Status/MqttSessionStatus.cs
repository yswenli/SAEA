/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MQTT.Server.Status
*文件名： MqttSessionStatus
*版本号： v26.4.23.1
*唯一标识：c1ad1579-71f5-4449-b0ed-31c7e34a1a78
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MQTT.Server.Status
{
    public sealed class MqttSessionStatus : IMqttSessionStatus
    {
        readonly MqttClientSession _session;
        readonly MqttClientSessionsManager _sessionsManager;

        public MqttSessionStatus(MqttClientSession session, MqttClientSessionsManager sessionsManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _sessionsManager = sessionsManager ?? throw new ArgumentNullException(nameof(sessionsManager));
        }

        public string ClientId { get; set; }

        public long PendingApplicationMessagesCount { get; set; }

        public DateTime CreatedTimestamp { get; set; }

        public IDictionary<object, object> Items { get; set; }

        public Task DeleteAsync()
        {
            return _sessionsManager.DeleteSessionAsync(ClientId);
        }

        public Task ClearPendingApplicationMessagesAsync()
        {
            _session.ApplicationMessagesQueue.Clear();
            return Task.FromResult(0);
        }
    }
}
