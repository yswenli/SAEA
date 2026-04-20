using System;

namespace SAEA.P2P.Security
{
    public class KeyExchange
    {
        public string SessionKey { get; private set; }
        public DateTime CreatedTime { get; private set; }
        public bool IsActive { get; private set; }

        public KeyExchange()
        {
            SessionKey = GenerateSessionKey();
            CreatedTime = DateTime.UtcNow;
            IsActive = true;
        }

        public static KeyExchange Create()
        {
            return new KeyExchange();
        }

        public static KeyExchange FromSharedKey(string sharedKey)
        {
            var ke = new KeyExchange();
            ke.SessionKey = sharedKey;
            return ke;
        }

        private static string GenerateSessionKey()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[16];
                rng.GetBytes(bytes);
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}