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
 *命名空间：SAEA.Sockets.Ssl
 *文件名： SaeaCertificateAuthority
 *版本号： v26.11.5.1
 *唯一标识：ad4ad840-a5c8-4ce7-9226-fffe85e3cfbd
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：本地根 CA 的持有与签发
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
using System.Security.Cryptography.X509Certificates;

using SAEA.Sockets.Ssl.Internal;

namespace SAEA.Sockets.Ssl
{
    /// <summary>
    /// 本地根证书颁发机构：持有 CA 私钥，用于签发服务端/客户端证书。
    /// 生成阶段请单线程使用，本类型不保证线程安全。
    /// </summary>
    public sealed class SaeaCertificateAuthority : IDisposable
    {
        private readonly SaeaCertificateMaterial _material;

        private bool _disposed;

        internal SaeaCertificateAuthority(SaeaCertificateMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            _material = material;
        }

        /// <summary>
        /// CA 证书，包含私钥。所有权归本对象。
        /// </summary>
        public X509Certificate2 Certificate
        {
            get { return _material.Certificate; }
        }

        /// <summary>
        /// 新建根 CA。忽略 <see cref="SaeaCertificateOptions.Purpose"/>、DnsNames 与 IpAddresses。
        /// </summary>
        /// <param name="options">生成参数；为 null 时抛 ArgumentNullException</param>
        /// <returns>可用于签发叶子的 CA 句柄</returns>
        /// <exception cref="ArgumentException">主题 DN、KeySize 或有效期非法</exception>
        public static SaeaCertificateAuthority Create(SaeaCertificateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var spec = SaeaCertificateWriter.Resolve(options, SaeaCertificatePurpose.CertificateAuthority);
            return new SaeaCertificateAuthority(SaeaCertificateWriter.CreateAuthority(spec));
        }

        /// <summary>
        /// 从 PFX/P12 复用已存在的 CA。
        /// </summary>
        /// <param name="path">PFX 文件路径</param>
        /// <param name="password">读取口令；同时作为后续 SavePfx 的默认口令，null 表示无口令</param>
        /// <returns>可用于签发叶子的 CA 句柄</returns>
        /// <exception cref="SaeaTlsException">证书没有私钥，或不是 CA 证书</exception>
        public static SaeaCertificateAuthority FromPfx(string path, string password = null)
        {
            return new SaeaCertificateAuthority(SaeaCertificateWriter.LoadAuthorityFromPfx(path, password));
        }

        /// <summary>
        /// 从 PEM 证书与 PEM 私钥复用已存在的 CA。
        /// </summary>
        /// <param name="certificatePath">证书文件路径（PEM）</param>
        /// <param name="privateKeyPath">私钥文件路径（PEM）</param>
        /// <param name="password">私钥口令；同时作为后续 SavePfx 的默认口令，null 表示无口令</param>
        /// <returns>可用于签发叶子的 CA 句柄</returns>
        /// <exception cref="SaeaTlsException">证书没有私钥，或不是 CA 证书</exception>
        public static SaeaCertificateAuthority FromPem(string certificatePath, string privateKeyPath,
            string password = null)
        {
            return new SaeaCertificateAuthority(
                SaeaCertificateWriter.LoadAuthorityFromPem(certificatePath, privateKeyPath, password));
        }

        /// <summary>
        /// 签发服务端证书。以方法名为准，忽略 options.Purpose；主题与有效期按服务端默认值解析。
        /// </summary>
        /// <param name="options">生成参数；为 null 时抛 ArgumentNullException</param>
        /// <returns>服务端叶子证书，含私钥与签发者 CA 链</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="ArgumentException">主题 DN、SAN、KeySize 或有效期非法</exception>
        public SaeaCertificateBundle IssueServerCertificate(SaeaCertificateOptions options)
        {
            ThrowIfDisposed();
            return Issue(options, SaeaCertificatePurpose.Server);
        }

        /// <summary>
        /// 签发客户端证书。以方法名为准，忽略 options.Purpose、DnsNames 与 IpAddresses。
        /// </summary>
        /// <param name="options">生成参数；为 null 时抛 ArgumentNullException</param>
        /// <returns>客户端叶子证书，含私钥与签发者 CA 链</returns>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        /// <exception cref="ArgumentException">主题 DN、KeySize 或有效期非法</exception>
        public SaeaCertificateBundle IssueClientCertificate(SaeaCertificateOptions options)
        {
            ThrowIfDisposed();
            return Issue(options, SaeaCertificatePurpose.Client);
        }

        /// <summary>
        /// 保存 CA 自身为 PKCS#12，不写叶子证书。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <param name="password">PFX 口令；null 表示使用创建/加载时的口令，空字符串表示无口令</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SavePfx(string path, string password = null)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SavePfx(path, _material, password);
        }

        /// <summary>
        /// 保存 CA 证书为 PEM（CERTIFICATE），不写证书链。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SaveCertificatePem(string path)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SaveCertificatePem(path, _material);
        }

        /// <summary>
        /// 保存 CA 私钥为 PEM。写出的是不加密的私钥，泄露等于签发权泄露。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SavePrivateKeyPem(string path)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SavePrivateKeyPem(path, _material);
        }

        /// <summary>
        /// 释放 CA 证书与私钥句柄。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _material.Certificate.Dispose();
        }

        private SaeaCertificateBundle Issue(SaeaCertificateOptions options, SaeaCertificatePurpose purpose)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var spec = SaeaCertificateWriter.Resolve(options, purpose);
            return new SaeaCertificateBundle(SaeaCertificateWriter.IssueLeaf(spec, _material));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SaeaCertificateAuthority));
        }
    }
}