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
*命名空间：SAEA.P2P.Security
*文件名： CryptoService
*版本号： v26.4.23.1
*唯一标识：a6259127-e4e1-4d6a-8d7f-36be8f6d65c6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:04:05
*描述：CryptoService接口
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:04:05
*修改人： yswenli
*版本号： v26.4.23.1
*描述：CryptoService接口
*
*****************************************************************************/
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