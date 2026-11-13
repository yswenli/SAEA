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
*文件名： SubjectNameParser
*版本号： net10.0
*唯一标识：31893acf-2c4e-4409-abce-e3cd1e134e4f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:21:12
*描述：解析 openssl 斜杠格式的 -subj
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:21:12
*修改人： yswenli
*版本号： net10.0
*描述：新增 -subj 解析
*
*****************************************************************************/
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>把 openssl 斜杠格式的 -subj 解析成 X509Name；分隔符为 / 或 ,，支持 \/、\,、\\ 转义，不支持 + 多值。</summary>
internal static class SubjectNameParser
{
    private static readonly Dictionary<string, DerObjectIdentifier> Known = new(StringComparer.Ordinal)
    {
        ["C"] = X509Name.C, ["O"] = X509Name.O, ["OU"] = X509Name.OU, ["CN"] = X509Name.CN,
        ["L"] = X509Name.L, ["ST"] = X509Name.ST, ["emailAddress"] = X509Name.EmailAddress,
    };
    public static X509Name Parse(string value)
    {
        if (value.Length == 0 || value[0] != '/') throw Invalid(value);
        var oids = new List<DerObjectIdentifier>();
        var values = new List<string>();
        var index = 1;
        while (index < value.Length)
        {
            var text = new StringBuilder();
            var escaped = false;
            while (index < value.Length)
            {
                var ch = value[index];
                if (escaped) { text.Append(ch); escaped = false; index++; continue; }
                if (ch == '\\') { escaped = true; index++; continue; }
                if (ch == '/' || ch == ',') break;
                if (ch == '+') throw new OpenSslException("req: -subj 不支持 + 多值。");
                text.Append(ch);
                index++;
            }
            var split = text.ToString().IndexOf('=');
            if (split <= 0 || split == text.Length - 1 || !Known.TryGetValue(text.ToString(0, split), out var oid)) throw Invalid(value);
            oids.Add(oid);
            values.Add(text.ToString(split + 1, text.Length - split - 1));
            index++; // 跳过分隔符；值里的分隔符必须写转义。
        }
        return oids.Count > 0 ? new X509Name(oids, values) : throw Invalid(value);
    }
    private static OpenSslException Invalid(string value) => new($"req: 无法解析 -subj：\"{value}\"。");
}