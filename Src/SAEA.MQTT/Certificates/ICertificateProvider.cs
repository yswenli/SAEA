using System.Security.Cryptography.X509Certificates;

namespace SAEA.MQTT.Certificates
{
    public interface ICertificateProvider
    {
        X509Certificate2 GetCertificate();
    }
}
