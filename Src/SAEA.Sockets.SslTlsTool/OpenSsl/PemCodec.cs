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
*文件名： PemCodec
*版本号： net10.0
*唯一标识：4894be6d-cc2f-4280-ad0b-95fa526c4c56
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:54:11
*描述：PEM 块切分与写出
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:54:11
*修改人： yswenli
*版本号： net10.0
*描述：新增 PEM 块切分与写出
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>PEM 块切分与写出：保序、64 列、只用 LF、不写 BOM。</summary>
internal static class PemCodec
{
    private static readonly Regex Block = new(
        "-----BEGIN ([A-Z0-9 ]+)-----(.*?)-----END \\1-----",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>按出现顺序取出全部 PEM 块；空白与 BOM 不参与 base64。</summary>
    /// <param name="text">PEM 文本</param>
    /// <returns>块头与 DER 组成的键值对列表</returns>
    public static List<KeyValuePair<string, byte[]>> ParseAll(string text)
    {
        var blocks = new List<KeyValuePair<string, byte[]>>();
        foreach (Match match in Block.Matches(text))
        {
            var body = new string(match.Groups[2].Value
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '\uFEFF').ToArray());
            blocks.Add(new KeyValuePair<string, byte[]>(match.Groups[1].Value, Convert.FromBase64String(body)));
        }
        return blocks;
    }
    /// <summary>读整个 PEM 文件；StreamReader 默认检测并剥离 BOM。</summary>
    /// <param name="path">文件路径</param>
    /// <returns>文件文本</returns>
    public static string ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
    /// <summary>把 DER 编成 PEM：64 列 base64、LF 换行、末尾恰好一个换行。</summary>
    /// <param name="label">PEM 块头，如 CERTIFICATE</param>
    /// <param name="der">DER 字节</param>
    /// <returns>PEM 文本</returns>
    public static string Encode(string label, byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        var text = new StringBuilder();
        text.Append("-----BEGIN ").Append(label).Append("-----\n");
        for (var i = 0; i < base64.Length; i += 64)
            text.Append(base64, i, Math.Min(64, base64.Length - i)).Append('\n');
        return text.Append("-----END ").Append(label).Append("-----\n").ToString();
    }
    /// <summary>写 PEM 文件：固定 UTF-8 无 BOM。</summary>
    /// <param name="path">文件路径</param>
    /// <param name="text">PEM 文本</param>
    public static void WriteFile(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));
    /// <summary>取第一个指定块头的 DER。</summary>
    /// <param name="text">PEM 文本</param>
    /// <param name="labels">可接受的块头</param>
    /// <returns>第一个命中块的 DER</returns>
    public static byte[] DecodeFirst(string text, params string[] labels)
    {
        foreach (var block in ParseAll(text))
            foreach (var label in labels)
                if (string.Equals(block.Key, label, StringComparison.Ordinal))
                    return block.Value;
        throw new OpenSslException($"error: 未找到 {string.Join(" 或 ", labels)} 块。");
    }
}