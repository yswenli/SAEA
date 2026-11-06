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
 *文件名： SaeaCertificateBundle
 *版本号： v26.11.5.1
 *唯一标识：86543f5e-32b2-4343-ab14-49f68c4e8eaf
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/6 13:38:08
 *描述：证书与其私钥的持有与保存
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
    /// 证书与其私钥，附带保存为 PFX/PEM 的能力。调用方负责释放。
    /// </summary>
    public sealed class SaeaCertificateBundle : IDisposable
    {
        private readonly SaeaCertificateMaterial _material;

        private bool _disposed;

        internal SaeaCertificateBundle(SaeaCertificateMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            _material = material;
        }

        /// <summary>
        /// 生成的证书，包含私钥。所有权归本对象。
        /// </summary>
        public X509Certificate2 Certificate
        {
            get { return _material.Certificate; }
        }

        /// <summary>
        /// 保存为 PKCS#12。自签证书只写当前证书，CA 签发的证书写入当前证书与签发者 CA。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <param name="password">PFX 口令；null 表示使用生成时的口令，空字符串表示无口令</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SavePfx(string path, string password = null)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SavePfx(path, _material, password);
        }

        /// <summary>
        /// 保存单张证书为 PEM（CERTIFICATE），不写证书链。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SaveCertificatePem(string path)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SaveCertificatePem(path, _material);
        }

        /// <summary>
        /// 保存私钥为 PEM。写出的是不加密的私钥，调用方必须按明文私钥对待。
        /// </summary>
        /// <param name="path">输出文件路径</param>
        /// <exception cref="ObjectDisposedException">对象已释放</exception>
        public void SavePrivateKeyPem(string path)
        {
            ThrowIfDisposed();
            SaeaCertificateWriter.SavePrivateKeyPem(path, _material);
        }

        /// <summary>
        /// 释放证书与私钥句柄。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _material.Certificate.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SaeaCertificateBundle));
        }
    }
}