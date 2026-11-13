/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                                 
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.SslTlsTool.OpenSsl
*文件名： SerialFile
*版本号： net10.0
*唯一标识：605c78eb-158b-4d21-b039-909e40b27591
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:30:26
*描述：CA 序列号文件的读写
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:30:26
*修改人： yswenli
*版本号： net10.0
*描述：新增 CA 序列号文件的读写
*
*****************************************************************************/
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>CA 序列号文件：旧值加一后回写，或取 64 位正随机数。</summary>
internal static class SerialFile
{
    public static BigInteger Next(string caPath, bool createSerial)
    {
        var path = PathFor(caPath);
        try
        {
            if (File.Exists(path))
            {
                // HexNumber 把最高位为 1 的值当负数（FF → -1），前置 0 强制按无符号解析。
                var old = BigInteger.Parse("0" + File.ReadAllText(path).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!createSerial) return old;
                var next = old + BigInteger.One;
                File.WriteAllText(path, next.ToString("X", CultureInfo.InvariantCulture) + "\n");
                return next;
            }
            if (!createSerial) return RandomValue();
            File.WriteAllText(path, "1\n");
            return BigInteger.One;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            throw new OpenSslException($"x509: 无法读写序列号文件 \"{path}\"：{ex.Message}");
        }
    }

    public static string PathFor(string caPath)
    {
        var extension = Path.GetExtension(caPath);
        return extension.Equals(".crt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pem", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cer", StringComparison.OrdinalIgnoreCase)
            ? caPath.Substring(0, caPath.Length - extension.Length) + ".srl"
            : caPath + ".srl";
    }

    private static BigInteger RandomValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        bytes[0] &= 0x7F; // 最高位清零，保证序列号为正数。
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        return value.IsZero ? BigInteger.One : value;
    }
}