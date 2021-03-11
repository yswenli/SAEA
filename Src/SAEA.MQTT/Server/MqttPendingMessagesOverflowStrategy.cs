namespace SAEA.MQTT.Server
{
    public enum MqttPendingMessagesOverflowStrategy
    {
        DropOldestQueuedMessage,
        DropNewMessage
    }
}
