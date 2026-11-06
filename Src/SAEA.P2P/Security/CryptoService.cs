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
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.P2P.Security
{
    /// <summary>
    /// 基于 AES 的字节与字符串加解密封装，未启用加密时原样返回输入
    /// </summary>
    public class CryptoService
    {
        private string _key;
        private bool _enabled = false;

        /// <summary>
        /// 创建未启用加密的实例，需再调用 SetKey 提供密钥
        /// </summary>
        public CryptoService() { }
        /// <summary>
        /// 使用指定密钥创建实例并直接启用加密
        /// </summary>
        /// <param name="key">AES 密钥，原样保存；传 null 或空串仍会置为启用状态，加密时才暴露密钥问题</param>
        public CryptoService(string key) { _key = key; _enabled = true; }

        /// <summary>
        /// 设置密钥并按密钥是否为空切换加密开关
        /// </summary>
        /// <param name="key">AES 密钥；为 null 或空串时关闭加密</param>
        public void SetKey(string key) { _key = key; _enabled = !string.IsNullOrEmpty(key); }
        /// <summary>
        /// 当前是否已启用加密
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 先将字节按 UTF-8 解码为文本再 AES 加密，密文以 Base64 文本的 UTF-8 字节返回
        /// </summary>
        /// <param name="data">明文数据；为 null 时原样返回</param>
        /// <returns>Base64 密文字节；未启用加密时返回原输入</returns>
        public byte[] Encrypt(byte[] data)
        {
            if (!_enabled || data == null) return data;
            var base64 = SAEA.Common.Encryption.AESHelper.Encrypt(System.Text.Encoding.UTF8.GetString(data), _key);
            return System.Text.Encoding.UTF8.GetBytes(base64);
        }

        /// <summary>
        /// 按 UTF-8 读取 Base64 密文并 AES 解密，结果按 UTF-8 转回字节
        /// </summary>
        /// <param name="data">Base64 密文的 UTF-8 字节；为 null 时原样返回</param>
        /// <returns>明文字节；未启用加密时返回原输入</returns>
        public byte[] Decrypt(byte[] data)
        {
            if (!_enabled || data == null) return data;
            var base64 = System.Text.Encoding.UTF8.GetString(data);
            var decrypted = SAEA.Common.Encryption.AESHelper.Decrypt(base64, _key);
            return System.Text.Encoding.UTF8.GetBytes(decrypted);
        }

        /// <summary>
        /// AES 加密字符串并以 Base64 文本返回
        /// </summary>
        /// <param name="text">明文字符串；为 null 时原样返回</param>
        /// <returns>Base64 密文；未启用加密时返回原输入</returns>
        public string EncryptString(string text)
        {
            if (!_enabled || text == null) return text;
            return SAEA.Common.Encryption.AESHelper.Encrypt(text, _key);
        }

        /// <summary>
        /// 解密 Base64 密文字符串
        /// </summary>
        /// <param name="encrypted">Base64 密文；为 null 时原样返回</param>
        /// <returns>明文字符串；未启用加密时返回原输入</returns>
        public string DecryptString(string encrypted)
        {
            if (!_enabled || encrypted == null) return encrypted;
            return SAEA.Common.Encryption.AESHelper.Decrypt(encrypted, _key);
        }
    }
}