namespace SAEA.P2P.Security
{
    public class CryptoService
    {
        private string _key;
        private bool _enabled = false;

        public CryptoService() { }
        public CryptoService(string key) { _key = key; _enabled = true; }

        public void SetKey(string key) { _key = key; _enabled = !string.IsNullOrEmpty(key); }
        public bool IsEnabled => _enabled;

        public byte[] Encrypt(byte[] data)
        {
            if (!_enabled || data == null) return data;
            var base64 = SAEA.Common.Encryption.AESHelper.Encrypt(System.Text.Encoding.UTF8.GetString(data), _key);
            return System.Text.Encoding.UTF8.GetBytes(base64);
        }

        public byte[] Decrypt(byte[] data)
        {
            if (!_enabled || data == null) return data;
            var base64 = System.Text.Encoding.UTF8.GetString(data);
            var decrypted = SAEA.Common.Encryption.AESHelper.Decrypt(base64, _key);
            return System.Text.Encoding.UTF8.GetBytes(decrypted);
        }

        public string EncryptString(string text)
        {
            if (!_enabled || text == null) return text;
            return SAEA.Common.Encryption.AESHelper.Encrypt(text, _key);
        }

        public string DecryptString(string encrypted)
        {
            if (!_enabled || encrypted == null) return encrypted;
            return SAEA.Common.Encryption.AESHelper.Decrypt(encrypted, _key);
        }
    }
}