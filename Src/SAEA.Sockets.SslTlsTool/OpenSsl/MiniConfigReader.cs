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
*文件名： MiniConfigReader
*版本号： net10.0
*唯一标识：6dbba7f3-db96-4234-9781-fbfa529bef92
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 14:21:12
*描述：最小配置文件的段落读取
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 14:21:12
*修改人： yswenli
*版本号： net10.0
*描述：新增最小配置文件的段落读取
*
*****************************************************************************/
namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>只认 [section]、key=value 与 ;/# 注释的最小配置读取器。</summary>
internal sealed class MiniConfigReader
{
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> _sections = new(StringComparer.Ordinal);
    public static MiniConfigReader Load(string path)
    {
        if (!File.Exists(path)) throw new OpenSslException($"error: 找不到文件 \"{path}\"。");
        var reader = new MiniConfigReader();
        List<KeyValuePair<string, string>>? current = null;
        foreach (var line in File.ReadAllLines(path).Select(raw => raw.Trim()))
        {
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                var name = line.Substring(1, line.Length - 2).Trim();
                reader._sections[name] = current = reader._sections.TryGetValue(name, out var existing) ? existing : new List<KeyValuePair<string, string>>();
                continue;
            }
            var split = line.IndexOf('=');
            if (current == null || split <= 0) throw new OpenSslException($"error: 无法解析配置行 \"{line}\"。");
            current.Add(new KeyValuePair<string, string>(line.Substring(0, split).Trim(), line.Substring(split + 1).Trim()));
        }
        return reader;
    }
    public IReadOnlyList<KeyValuePair<string, string>> Section(string name) =>
        _sections.TryGetValue(name, out var entries) ? entries : throw new OpenSslException($"x509: -extensions 指定的段落 \"{name}\" 不存在。");
}