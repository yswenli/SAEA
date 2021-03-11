namespace SAEA.MQTT.Client.Options
{
    public interface IMqttClientCredentials
    {
        string Username { get; }
        byte[] Password { get; }
    }
}