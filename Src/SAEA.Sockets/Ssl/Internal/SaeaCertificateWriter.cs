/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *机器名称：WALLE
 *公司名称：yswenli
 *命名空间：SAEA.Sockets.Ssl.Internal
 *文件名： SaeaCertificateWriter
 *版本号： v26.11.5.1
 *唯一标识：628a9e7c-087b-4445-8c73-833e68fad329
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：证书生成的内部实现，含参数解析、BC 互转与 PFX/PEM 写出
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/6 13:38:08
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：新增开发用证书生成 API
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

using BcCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// 按用途解析后的生成参数。默认值在解析时确定，不回写调用方传入的 options。
    /// </summary>
    internal sealed class SaeaCertificateSpec
    {
        /// <summary>证书用途。</summary>
        public SaeaCertificatePurpose Purpose { get; set; }

        /// <summary>解析后的主题 DN。</summary>
        public X509Name Subject { get; set; }

        /// <summary>是否写 SAN，仅服务端用途为 true。</summary>
        public bool WriteAlternativeNames { get; set; }

        /// <summary>DNS SAN 列表。</summary>
        public IList<string> DnsNames { get; set; }

        /// <summary>IP SAN 列表。</summary>
        public IList<string> IpAddresses { get; set; }

        /// <summary>RSA 密钥长度。</summary>
        public int KeySize { get; set; }

        /// <summary>生效时间（UTC）。</summary>
        public DateTime NotBefore { get; set; }

        /// <summary>到期时间（UTC）。</summary>
        public DateTime NotAfter { get; set; }

        /// <summary>打包 PFX 时使用的口令。</summary>
        public string PfxPassword { get; set; }
    }

    /// <summary>
    /// 证书、私钥及签发者证书的内部持有对象。
    /// </summary>
    internal sealed class SaeaCertificateMaterial
    {
        /// <summary>供调用方使用的证书。</summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>与 <see cref="Certificate"/> 对应的 BC 证书。</summary>
        public BcCertificate BcCertificate { get; set; }

        /// <summary>生成时的 BC 私钥，不从句柄反推。</summary>
        public AsymmetricKeyParameter PrivateKey { get; set; }

        /// <summary>签发者 CA 的 BC 证书；自签或 CA 自身为 null。</summary>
        public BcCertificate IssuerBcCertificate { get; set; }

        /// <summary>保存 PFX 时的默认口令。</summary>
        public string Password { get; set; }
    }

    /// <summary>
    /// 证书生成的内部帮助类：解析参数与默认值、生成证书、BC 与 .NET 互转、PFX/PEM 写出。
    /// </summary>
    internal static class SaeaCertificateWriter
    {
        private const int DefaultKeySize = 2048;

        private const string DefaultPfxPassword = "saea";

        private const string KeyEntryAlias = "saea";

        private const string SignatureAlgorithm = "SHA256WITHRSA";

        // 复用 CA 时需要通过 BcKeyConverter 导出私钥参数，而临时密钥集在部分平台不允许导出，
        // 因此这条路径单独使用 Exportable 标志；不影响其它加载路径。
        private const X509KeyStorageFlags AuthorityLoadFlags = X509KeyStorageFlags.Exportable;

        /// <summary>
        /// 按用途解析参数与默认值。
        /// </summary>
        /// <param name="options">调用方参数</param>
        /// <param name="purpose">本次实际生成的用途，优先于 options.Purpose</param>
        /// <returns>解析后的参数</returns>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        /// <exception cref="ArgumentException">主题 DN、SAN、KeySize 或有效期非法</exception>
        public static SaeaCertificateSpec Resolve(SaeaCertificateOptions options, SaeaCertificatePurpose purpose)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var now = DateTimeOffset.UtcNow;
            var spec = new SaeaCertificateSpec();
            spec.Purpose = purpose;

            spec.Subject = ParseSubject(options.Subject, GetDefaultSubject(purpose));
            spec.KeySize = ResolveKeySize(options.KeySize);

            var notBefore = options.NotBefore == default(DateTimeOffset)
                ? now.AddDays(-1)
                : options.NotBefore;
            var notAfter = options.NotAfter == default(DateTimeOffset)
                ? now.AddDays(purpose == SaeaCertificatePurpose.CertificateAuthority ? 3650 : 30)
                : options.NotAfter;

            if (notAfter <= notBefore)
                throw new ArgumentException("证书到期时间必须晚于生效时间。", nameof(SaeaCertificateOptions.NotAfter));

            spec.NotBefore = notBefore.UtcDateTime;
            spec.NotAfter = notAfter.UtcDateTime;
            spec.PfxPassword = options.PfxPassword ?? DefaultPfxPassword;

            if (purpose == SaeaCertificatePurpose.Server)
            {
                spec.WriteAlternativeNames = true;
                spec.DnsNames = ResolveDnsNames(options.DnsNames, spec.Subject);
                spec.IpAddresses = ResolveIpAddresses(options.IpAddresses);
            }

            return spec;
        }

        /// <summary>
        /// 创建单张自签证书。
        /// </summary>
        /// <param name="spec">解析后的参数</param>
        /// <returns>证书与私钥</returns>
        public static SaeaCertificateMaterial CreateSelfSigned(SaeaCertificateSpec spec)
        {
            var keyPair = GenerateKeyPair(spec.KeySize);
            var certificate = BuildCertificate(spec, keyPair, false, null, null);
            return Wrap(certificate, keyPair.Private, null, spec.PfxPassword);
        }

        /// <summary>
        /// 创建根 CA 证书。
        /// </summary>
        /// <param name="spec">解析后的参数</param>
        /// <returns>CA 证书与私钥</returns>
        public static SaeaCertificateMaterial CreateAuthority(SaeaCertificateSpec spec)
        {
            var keyPair = GenerateKeyPair(spec.KeySize);
            var certificate = BuildCertificate(spec, keyPair, true, null, null);
            return Wrap(certificate, keyPair.Private, null, spec.PfxPassword);
        }

        /// <summary>
        /// 用 CA 签发叶子证书。
        /// </summary>
        /// <param name="spec">解析后的叶子参数</param>
        /// <param name="issuer">CA 的材料，提供签发者主题、签名私钥与链证书</param>
        /// <returns>叶子证书与私钥</returns>
        public static SaeaCertificateMaterial IssueLeaf(SaeaCertificateSpec spec, SaeaCertificateMaterial issuer)
        {
            if (issuer == null) throw new ArgumentNullException(nameof(issuer));

            var keyPair = GenerateKeyPair(spec.KeySize);
            var certificate = BuildCertificate(spec, keyPair, false, issuer.BcCertificate, issuer.PrivateKey);
            return Wrap(certificate, keyPair.Private, issuer.BcCertificate, spec.PfxPassword);
        }

        /// <summary>
        /// 从 PFX 加载 CA 并校验其可签发能力。
        /// </summary>
        /// <param name="path">PFX 文件路径</param>
        /// <param name="password">读取口令，null 表示无口令</param>
        /// <returns>CA 的材料</returns>
        /// <exception cref="SaeaTlsException">证书没有私钥，或不是 CA 证书</exception>
        public static SaeaCertificateMaterial LoadAuthorityFromPfx(string path, string password)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var certificate = SaeaCertificateLoader.FromPfx(path, password, AuthorityLoadFlags);
            return FromCertificate(certificate, password);
        }

        /// <summary>
        /// 从 PEM 证书与私钥加载 CA 并校验其可签发能力。
        /// </summary>
        /// <param name="certificatePath">证书文件路径</param>
        /// <param name="privateKeyPath">私钥文件路径</param>
        /// <param name="password">私钥口令，null 表示无口令</param>
        /// <returns>CA 的材料</returns>
        /// <exception cref="SaeaTlsException">证书没有私钥，或不是 CA 证书</exception>
        public static SaeaCertificateMaterial LoadAuthorityFromPem(string certificatePath,
            string privateKeyPath, string password)
        {
            if (string.IsNullOrWhiteSpace(certificatePath)) throw new ArgumentNullException(nameof(certificatePath));
            if (string.IsNullOrWhiteSpace(privateKeyPath)) throw new ArgumentNullException(nameof(privateKeyPath));

            var certificate = SaeaCertificateLoader.LoadPemWithFlags(certificatePath, privateKeyPath, password,
                AuthorityLoadFlags);
            return FromCertificate(certificate, password);
        }

        /// <summary>
        /// 保存 PFX。自签或 CA 自身只写一张证书，CA 签发的叶子写叶子 + 签发者。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <param name="material">证书材料</param>
        /// <param name="password">口令；null 表示使用材料记录的默认口令</param>
        public static void SavePfx(string path, SaeaCertificateMaterial material, string password)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            var effectivePassword = password ?? material.Password;
            var data = BuildPfx(material.BcCertificate, material.IssuerBcCertificate, material.PrivateKey,
                effectivePassword);
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// 保存单张证书 PEM。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <param name="material">证书材料</param>
        public static void SaveCertificatePem(string path, SaeaCertificateMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            WritePem(path, material.BcCertificate);
        }

        /// <summary>
        /// 保存不加密的私钥 PEM。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <param name="material">证书材料</param>
        public static void SavePrivateKeyPem(string path, SaeaCertificateMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            WritePem(path, PrivateKeyInfoFactory.CreatePrivateKeyInfo(material.PrivateKey));
        }

        private static SaeaCertificateMaterial FromCertificate(X509Certificate2 certificate, string password)
        {
            if (!certificate.HasPrivateKey)
                throw new SaeaTlsException("签发证书需要 CA 私钥。");
            if (!IsCertificateAuthority(certificate))
                throw new SaeaTlsException("证书不是 CA 证书，不能用于签发。");

            var material = new SaeaCertificateMaterial();
            material.Certificate = certificate;
            material.BcCertificate = new X509CertificateParser().ReadCertificate(certificate.RawData);
            material.PrivateKey = BcKeyConverter.GetPrivateKey(certificate);
            material.Password = password;
            return material;
        }

        private static SaeaCertificateMaterial Wrap(BcCertificate certificate, AsymmetricKeyParameter privateKey,
            BcCertificate issuer, string password)
        {
            var material = new SaeaCertificateMaterial();
            material.BcCertificate = certificate;
            material.PrivateKey = privateKey;
            material.IssuerBcCertificate = issuer;
            material.Password = password;
            material.Certificate = LoadPfx(BuildPfx(certificate, issuer, privateKey, password), password);
            return material;
        }

        private static X509Certificate2 LoadPfx(byte[] data, string password)
        {
            // 必须复用加载器的旧运行时回退：.NET Framework 不支持 EphemeralKeySet 时它会改用默认标志。
            return SaeaCertificateLoader.LoadPkcs12(data, password ?? string.Empty,
                SaeaCertificateLoader.DefaultKeyStorageFlags);
        }

        private static byte[] BuildPfx(BcCertificate certificate, BcCertificate issuer,
            AsymmetricKeyParameter privateKey, string password)
        {
            var chain = new List<X509CertificateEntry>();
            chain.Add(new X509CertificateEntry(certificate));
            if (issuer != null)
                chain.Add(new X509CertificateEntry(issuer));

            var store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry(KeyEntryAlias, new AsymmetricKeyEntry(privateKey), chain.ToArray());

            using (var buffer = new MemoryStream())
            {
                store.Save(buffer, (password ?? string.Empty).ToCharArray(), new SecureRandom());
                return buffer.ToArray();
            }
        }

        private static BcCertificate BuildCertificate(SaeaCertificateSpec spec, AsymmetricCipherKeyPair keyPair,
            bool certificateAuthority, BcCertificate issuerCertificate, AsymmetricKeyParameter issuerPrivateKey)
        {
            var random = new SecureRandom();
            var generator = new X509V3CertificateGenerator();

            generator.SetSerialNumber(new BigInteger(127, random).SetBit(127));
            generator.SetIssuerDN(issuerCertificate != null ? issuerCertificate.SubjectDN : spec.Subject);
            generator.SetSubjectDN(spec.Subject);
            generator.SetNotBefore(spec.NotBefore);
            generator.SetNotAfter(spec.NotAfter);
            generator.SetPublicKey(keyPair.Public);

            if (certificateAuthority)
            {
                generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(0));
                generator.AddExtension(X509Extensions.KeyUsage, true,
                    new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
            }
            else
            {
                generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
                generator.AddExtension(X509Extensions.KeyUsage, false,
                    new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

                var keyPurpose = spec.Purpose == SaeaCertificatePurpose.Client
                    ? KeyPurposeID.id_kp_clientAuth
                    : KeyPurposeID.id_kp_serverAuth;
                generator.AddExtension(X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(keyPurpose));
            }

            generator.AddExtension(X509Extensions.SubjectKeyIdentifier, false,
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(keyPair.Public));

            if (issuerCertificate != null)
            {
                generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false,
                    X509ExtensionUtilities.CreateAuthorityKeyIdentifier(issuerCertificate));
            }

            if (!certificateAuthority && spec.WriteAlternativeNames)
                AddAlternativeNames(generator, spec);

            var signingKey = issuerPrivateKey ?? keyPair.Private;
            return generator.Generate(new Asn1SignatureFactory(SignatureAlgorithm, signingKey, random));
        }

        private static void AddAlternativeNames(X509V3CertificateGenerator generator, SaeaCertificateSpec spec)
        {
            var names = new List<GeneralName>();

            if (spec.DnsNames != null)
            {
                foreach (var dnsName in spec.DnsNames)
                    names.Add(new GeneralName(GeneralName.DnsName, dnsName));
            }

            if (spec.IpAddresses != null)
            {
                foreach (var ipAddress in spec.IpAddresses)
                    names.Add(new GeneralName(GeneralName.IPAddress, ipAddress));
            }

            if (names.Count == 0) return;

            generator.AddExtension(X509Extensions.SubjectAlternativeName, false,
                new GeneralNames(names.ToArray()));
        }

        private static AsymmetricCipherKeyPair GenerateKeyPair(int keySize)
        {
            // 不用 RSA.Create(int)：.NET Framework 4.6.2 没有该重载。
            var generator = new RsaKeyPairGenerator();
            generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), new SecureRandom(), keySize, 64));
            return generator.GenerateKeyPair();
        }

        private static X509Name ParseSubject(string subject, string defaultSubject)
        {
            var value = string.IsNullOrWhiteSpace(subject) ? defaultSubject : subject.Trim();

            try
            {
                return new X509Name(value);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("证书主题 DN 无效：" + value, nameof(SaeaCertificateOptions.Subject), ex);
            }
        }

        private static string GetDefaultSubject(SaeaCertificatePurpose purpose)
        {
            if (purpose == SaeaCertificatePurpose.CertificateAuthority) return "CN=SAEA Dev Root CA";
            if (purpose == SaeaCertificatePurpose.Client) return "CN=saea-client";
            return "CN=localhost";
        }

        private static int ResolveKeySize(int keySize)
        {
            if (keySize == 0) return DefaultKeySize;

            if (keySize != 2048 && keySize != 3072 && keySize != 4096)
                throw new ArgumentException("密钥长度仅支持 2048/3072/4096。", nameof(SaeaCertificateOptions.KeySize));

            return keySize;
        }

        private static IList<string> ResolveDnsNames(string[] dnsNames, X509Name subject)
        {
            var result = new List<string>();

            if (dnsNames == null)
            {
                var values = subject.GetValueList(X509Name.CN);
                var commonName = values != null && values.Count > 0 ? values[0] : null;

                if (!string.IsNullOrWhiteSpace(commonName) && !IsIpAddress(commonName))
                    AddDistinct(result, commonName);

                AddDistinct(result, "localhost");
                return result;
            }

            foreach (var dnsName in dnsNames)
            {
                if (string.IsNullOrWhiteSpace(dnsName))
                    throw new ArgumentException("DNS 名称不能为空。", nameof(SaeaCertificateOptions.DnsNames));

                result.Add(dnsName);
            }

            return result;
        }

        private static IList<string> ResolveIpAddresses(string[] ipAddresses)
        {
            var result = new List<string>();

            if (ipAddresses == null)
            {
                result.Add("127.0.0.1");
                return result;
            }

            foreach (var value in ipAddresses)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("IP 地址不能为空。", nameof(SaeaCertificateOptions.IpAddresses));

                IPAddress address;
                if (!IPAddress.TryParse(value, out address))
                    throw new ArgumentException("IP 地址格式无效：" + value,
                        nameof(SaeaCertificateOptions.IpAddresses));

                result.Add(address.ToString());
            }

            return result;
        }

        private static bool IsIpAddress(string value)
        {
            IPAddress address;
            return IPAddress.TryParse(value, out address);
        }

        private static void AddDistinct(IList<string> values, string value)
        {
            foreach (var existing in values)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            values.Add(value);
        }

        private static bool IsCertificateAuthority(X509Certificate2 certificate)
        {
            foreach (var extension in certificate.Extensions)
            {
                var basicConstraints = extension as X509BasicConstraintsExtension;
                if (basicConstraints != null && basicConstraints.CertificateAuthority)
                    return true;
            }

            return false;
        }

        private static void WritePem(string path, object value)
        {
            var buffer = new StringWriter();
            buffer.NewLine = "\n";

            var writer = new PemWriter(buffer);
            writer.WriteObject(value);

            File.WriteAllText(path, buffer.ToString(), new UTF8Encoding(false));
        }
    }
}