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
*文件名： OptionReader
*版本号： net10.0
*唯一标识：1845c5cc-efd1-4709-853b-d2af9494e69c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 13:40:21
*描述：单横线参数的读取与校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:40:21
*修改人： yswenli
*版本号： net10.0
*描述：新增兼容层参数读取
*
*****************************************************************************/
using System.Globalization;

namespace SAEA.Sockets.SslTlsTool.OpenSsl;

/// <summary>单横线参数读取：取值/开关、重复聚合、缺值与未知参数报错。</summary>
internal sealed class OptionReader
{
    private static readonly HashSet<string> Repeatable = new(StringComparer.Ordinal) { "-addext", "-certfile" };

    private readonly Dictionary<string, List<string?>> _options = new(StringComparer.Ordinal);

    private readonly List<string> _positional = new();

    /// <summary>按子命令允许的参数清单解析一次调用的参数。</summary>
    /// <param name="command">子命令名，用于错误提示</param>
    /// <param name="args">子命令之后的参数</param>
    /// <param name="allowedOptions">该子命令允许的参数名</param>
    public OptionReader(string command, string[] args, string[] allowedOptions)
    {
        var allowed = new HashSet<string>(allowedOptions, StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!IsOption(token))
            {
                _positional.Add(token);
                continue;
            }
            if (!allowed.Contains(token))
                throw new OpenSslException($"error: 不支持的参数 \"{token}\"；支持的参数见 -help。");
            string? value = null;
            if (i + 1 < args.Length && !IsOption(args[i + 1]))
                value = args[++i];
            if (!_options.TryGetValue(token, out var values))
                _options[token] = values = new List<string?>();
            else if (!Repeatable.Contains(token))
                throw new OpenSslException($"error: 参数 \"{token}\" 重复。");
            values.Add(value);
        }
    }

    /// <summary>非 - 开头的位置参数，按输入顺序。</summary>
    public IReadOnlyList<string> Positional => _positional;

    /// <summary>参数是否出现（含只写参数名不给取值的情况）。</summary>
    /// <param name="name">参数名</param>
    /// <returns>出现时返回 true</returns>
    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>取参数取值，未出现时返回 null。</summary>
    /// <param name="name">参数名</param>
    /// <returns>取值或 null</returns>
    public string? Get(string name) => !_options.TryGetValue(name, out var values)
        ? null
        // 给了参数却没跟取值（如末尾只有 -passin）时按缺值报错，不再退化成“未给”。
        : values[0] ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。");

    /// <summary>取必填参数取值，未出现或无数值时抛错。</summary>
    /// <param name="name">参数名</param>
    /// <returns>取值</returns>
    public string GetRequired(string name) => Get(name) ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。");

    /// <summary>取整数取值，未出现时返回默认值。</summary>
    /// <param name="name">参数名</param>
    /// <param name="defaultValue">未出现时的默认值</param>
    /// <returns>整数取值</returns>
    public int GetInt(string name, int defaultValue) => !Has(name) ? defaultValue : ParseInt(name, GetRequired(name));

    /// <summary>取可重复参数的全部取值。</summary>
    /// <param name="name">参数名</param>
    /// <returns>按输入顺序的取值列表，未出现时为空列表</returns>
    public IReadOnlyList<string> GetAll(string name) => !_options.TryGetValue(name, out var values)
        ? Array.Empty<string>()
        : values.Select(value => value ?? throw new OpenSslException($"error: 参数 \"{name}\" 缺少取值。")).ToList();

    private static int ParseInt(string name, string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new OpenSslException($"error: 参数 \"{name}\" 需要整数，实际为 \"{text}\"。");
        return value;
    }

    // 单横线且长度大于 1 才算参数；"-" 与空串按位置参数处理。
    private static bool IsOption(string token) => token.Length > 1 && token[0] == '-';
}