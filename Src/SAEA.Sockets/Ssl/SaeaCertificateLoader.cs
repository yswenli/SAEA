/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Ssl
 *文件名： SaeaCertificateLoader
 *描述：证书加载器
 *
 *****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 证书加载器：支持 PFX/P12、证书库、PEM 以及按目录自动匹配。
    /// </summary>
    public static class SaeaCertificateLoader
    {
        /// <summary>
        /// 临时密钥集标志。 <c>netstandard2.0</c> 的 <see cref="X509KeyStorageFlags"/> 未定义该成员，
        /// 这里补充其运行时值（0x20）。在 .NET Core / .NET 5+ 上可用。
        /// </summary>
        public const X509KeyStorageFlags EphemeralKeySet = (X509KeyStorageFlags)0x20;

        /// <summary>
        /// 默认加载标志：临时密钥集 + 允许导出私钥。
        /// </summary>
        /// <remarks>
        /// BouncyCastle 的默认签名实现需要导出私钥参数；虽然内置签名器已改为直接使用私钥句柄，
        /// 但保留 <see cref="X509KeyStorageFlags.Exportable"/> 可让用户代码继续按预期导出密钥。
        /// </remarks>
        public const X509KeyStorageFlags DefaultKeyStorageFlags =
            EphemeralKeySet | X509KeyStorageFlags.Exportable;

        private static readonly ConcurrentDictionary<string, X509Certificate2> _pemCache =
            new ConcurrentDictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 从 PFX/P12 加载。
        /// </summary>
        /// <param name="path">证书文件路径，不能为空，文件不存在时抛 FileNotFoundException</param>
        /// <param name="password">证书密码，无密码时为 null</param>
        /// <param name="flags">密钥存储标志，为 null 时使用 DefaultKeyStorageFlags</param>
        /// <returns>加载得到的证书对象，PFX 内含私钥时可直接用于 TLS</returns>
        public static X509Certificate2 FromPfx(string path, string password = null,
            X509KeyStorageFlags? flags = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到证书文件。", path);

            return LoadPkcs12(path, password, flags ?? DefaultKeyStorageFlags);
        }

        /// <summary>
        /// 从当前用户/本地计算机证书库按主题或指纹加载。
        /// </summary>
        /// <param name="subjectOrThumbprint">证书主题或指纹，内部会去掉空格后按指纹查找</param>
        /// <param name="location">证书库位置，默认 CurrentUser</param>
        /// <param name="validOnly">是否只返回有效证书</param>
        /// <returns>首个带私钥的匹配证书；无匹配时返回 null</returns>
        public static X509Certificate2 FromStore(string subjectOrThumbprint,
            StoreLocation location = StoreLocation.CurrentUser, bool validOnly = true)
        {
            if (string.IsNullOrWhiteSpace(subjectOrThumbprint))
                throw new ArgumentNullException(nameof(subjectOrThumbprint));

            var key = subjectOrThumbprint.Replace(" ", string.Empty);
            using (var store = new X509Store(StoreName.My, location))
            {
                store.Open(OpenFlags.ReadOnly);

                var candidates = store.Certificates.Find(X509FindType.FindByThumbprint, key, validOnly);
                if (candidates.Count == 0)
                    candidates = store.Certificates.Find(X509FindType.FindBySubjectName, subjectOrThumbprint, validOnly);

                foreach (var certificate in candidates)
                {
                    if (certificate.HasPrivateKey)
                        return certificate;
                }

                return null;
            }
        }

        /// <summary>
        /// 从证书文件 + 私钥文件加载（PEM）。
        /// </summary>
        /// <remarks>
        /// <c>netstandard2.0</c> 未提供 <c>X509Certificate2.CreateFromPemFile</c>，因此使用
        /// BouncyCastle 解析私钥并重新打包为 PKCS#12，再交给运行时加载。
        /// </remarks>
        /// <param name="certificatePath">证书文件路径（PEM），不能为空，文件不存在时抛 FileNotFoundException</param>
        /// <param name="privateKeyPath">私钥文件路径（PEM），不能为空，文件不存在时抛 FileNotFoundException</param>
        /// <param name="password">私钥解密密码，未加密时为 null</param>
        /// <returns>包含私钥的证书对象；相同证书/私钥/密码组合会命中进程内缓存返回同一实例</returns>
        public static X509Certificate2 FromPem(string certificatePath, string privateKeyPath,
            string password = null)
        {
            if (string.IsNullOrWhiteSpace(certificatePath))
                throw new ArgumentNullException(nameof(certificatePath));
            if (string.IsNullOrWhiteSpace(privateKeyPath))
                throw new ArgumentNullException(nameof(privateKeyPath));
            if (!File.Exists(certificatePath))
                throw new FileNotFoundException("找不到证书文件。", certificatePath);
            if (!File.Exists(privateKeyPath))
                throw new FileNotFoundException("找不到私钥文件。", privateKeyPath);

            var cacheKey = certificatePath + "|" + privateKeyPath + "|" + (password ?? string.Empty);
            return _pemCache.GetOrAdd(cacheKey, _ => LoadPemCore(certificatePath, privateKeyPath, password));
        }

        /// <summary>
        /// 按目录自动加载：文件名匹配主机名（如 api.example.com.pfx / *.example.com.pfx）。
        /// </summary>
        /// <param name="directory">证书目录，不能为空，目录不存在时委托返回 null</param>
        /// <param name="password">证书密码，无密码时为 null</param>
        /// <param name="searchPattern">搜索模式，默认 *.pfx；为空时回退为 *.pfx</param>
        /// <returns>主机名到证书的委托；按精确 host、通配符最长后缀、default 的顺序匹配，均未命中返回 null</returns>
        public static Func<string, X509Certificate2> FromDirectory(string directory,
            string password = null, string searchPattern = "*.pfx")
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));

            var search = string.IsNullOrWhiteSpace(searchPattern) ? "*.pfx" : searchPattern;
            var extension = Path.GetExtension(search.Replace("*", string.Empty));
            if (string.IsNullOrEmpty(extension))
                extension = ".pfx";

            Func<string, X509Certificate2> selector = hostName =>
            {
                if (string.IsNullOrWhiteSpace(hostName) || !Directory.Exists(directory))
                    return null;

                var files = Directory.GetFiles(directory, search);
                if (files.Length == 0)
                    return null;

                string matched = null;

                // 1. 精确匹配 host.ext
                var exact = Path.Combine(directory, hostName + extension);
                if (files.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(exact),
                        StringComparison.OrdinalIgnoreCase)))
                {
                    matched = exact;
                }

                // 2. 通配符 *.suffix，取最长后缀
                if (matched == null)
                {
                    var bestSuffixLength = -1;
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrEmpty(name) || !name.StartsWith("*.", StringComparison.Ordinal))
                            continue;

                        var suffix = name.Substring(1); // ".example.com"
                        if (!hostName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 通配符只能匹配一个最左标签
                        var prefix = hostName.Substring(0, hostName.Length - suffix.Length);
                        if (prefix.Length == 0 || prefix.IndexOf('.') >= 0)
                            continue;

                        if (suffix.Length > bestSuffixLength)
                        {
                            bestSuffixLength = suffix.Length;
                            matched = file;
                        }
                    }
                }

                // 3. default.ext
                if (matched == null)
                {
                    var defaultFile = Path.Combine(directory, "default" + extension);
                    if (files.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(defaultFile),
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        matched = defaultFile;
                    }
                }

                if (matched == null)
                    return null;

                return FromPfx(matched, password);
            };

            return selector;
        }

        private static X509Certificate2 LoadPemCore(string certificatePath, string privateKeyPath, string password)
        {
            var certificateBytes = File.ReadAllBytes(certificatePath);
            var bcCertificate = new X509CertificateParser().ReadCertificate(certificateBytes);

            AsymmetricKeyParameter privateKey;
            using (var reader = File.OpenText(privateKeyPath))
            {
                var pemReader = string.IsNullOrEmpty(password)
                    ? new PemReader(reader)
                    : new PemReader(reader, new FixedPasswordFinder(password));

                privateKey = ReadPrivateKey(pemReader);
            }

            if (privateKey == null)
                throw new SaeaTlsException("未能从 PEM 文件解析出私钥。");

            var store = new Pkcs12StoreBuilder().Build();
            var alias = "saea";
            store.SetKeyEntry(alias,
                new AsymmetricKeyEntry(privateKey),
                new[] { new X509CertificateEntry(bcCertificate) });

            using (var output = new MemoryStream())
            {
                var pfxPassword = password ?? string.Empty;
                store.Save(output, pfxPassword.ToCharArray(), new SecureRandom());
                return LoadPkcs12(output.ToArray(), pfxPassword, DefaultKeyStorageFlags);
            }
        }

        private static X509Certificate2 LoadPkcs12(string path, string password, X509KeyStorageFlags flags)
        {
            try
            {
                return new X509Certificate2(path, password, flags);
            }
            catch (ArgumentException)
            {
                // 部分旧运行时（如 .NET Framework）不支持 EphemeralKeySet。
                return new X509Certificate2(path, password);
            }
            catch (PlatformNotSupportedException)
            {
                return new X509Certificate2(path, password);
            }
        }

        private static X509Certificate2 LoadPkcs12(byte[] data, string password, X509KeyStorageFlags flags)
        {
            try
            {
                return new X509Certificate2(data, password, flags);
            }
            catch (ArgumentException)
            {
                return new X509Certificate2(data, password);
            }
            catch (PlatformNotSupportedException)
            {
                return new X509Certificate2(data, password);
            }
        }

        private static AsymmetricKeyParameter ReadPrivateKey(PemReader reader)
        {
            object value;
            while ((value = reader.ReadObject()) != null)
            {
                if (value is AsymmetricCipherKeyPair keyPair)
                    return keyPair.Private;

                if (value is AsymmetricKeyParameter keyParameter)
                    return keyParameter;
            }

            return null;
        }

        private sealed class FixedPasswordFinder : IPasswordFinder
        {
            private readonly char[] _password;

            public FixedPasswordFinder(string password)
            {
                _password = password.ToCharArray();
            }

            public char[] GetPassword()
            {
                return _password;
            }
        }
    }
}