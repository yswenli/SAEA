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
*命名空间：SAEA.DNS.Common.ResourceRecords
*文件名： TextResourceRecord
*版本号： v26.4.23.1
*唯一标识：d60c5ff6-20b1-415f-8fa1-02c39660d1bf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：TextResourceRecord记录类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:23:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补齐英文与乱码注释的中文说明
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.DNS.Common.ResourceRecords
{
    /// <summary>
    /// 文本资源记录，以键值对形式保存 TXT 数据
    /// </summary>
    public class TextResourceRecord : BaseResourceRecord
    {
        /// <summary>
        /// 匹配未转义的「名称=值」形式，并保留值中的空白
        /// </summary>
        private static readonly Regex PATTERN_TXT_RECORD = new Regex(@"^([ -~]*?)(?<!`)=([ -~]*)$");

        /// <summary>
        /// 匹配名称首尾未转义的空白
        /// </summary>
        private static readonly Regex PATTERN_TRIM_NAME = new Regex(@"^\s+|((?<!`)\s)+$");

        /// <summary>
        /// 匹配需要转义的字符（反引号与等号）
        /// </summary>
        private static readonly Regex PATTERN_ESCAPE = new Regex(@"([`=])");

        /// <summary>
        /// 匹配已转义的字符（反引号后跟反引号、等号或空白）
        /// </summary>
        private static readonly Regex PATTERN_UNESCAPE = new Regex(@"`([`=\s])");

        private static string Trim(string value) => PATTERN_TRIM_NAME.Replace(value, string.Empty);
        private static string Escape(string value) => PATTERN_ESCAPE.Replace(value, "`$1");
        private static string Unescape(string value) => PATTERN_UNESCAPE.Replace(value, "$1");

        private static IResourceRecord Create(Domain domain, IList<CharacterString> characterStrings, TimeSpan ttl)
        {
            byte[] data = new byte[characterStrings.Sum(c => c.Size)];
            int offset = 0;

            foreach (CharacterString characterString in characterStrings)
            {
                characterString.ToArray().CopyTo(data, offset);
                offset += characterString.Size;
            }

            return new ResourceRecord(domain, data, RecordType.TXT, RecordClass.IN, ttl);
        }

        private static IList<CharacterString> FormatAttributeNameValue(string attributeName, string attributeValue)
        {
            return CharacterString.FromString($"{Escape(attributeName)}={attributeValue}");
        }

        /// <summary>
        /// 从已解析记录的数据段构造 TXT 记录
        /// </summary>
        /// <param name="record">已解析的记录，数据段为若干长度前缀字符串</param>
        public TextResourceRecord(IResourceRecord record) :
            base(record)
        {
            TextData = CharacterString.GetAllFromArray(Data, 0);
        }

        /// <summary>
        /// 用一组字符串构造 TXT 记录，数据段按顺序拼接
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="characterStrings">字符串列表，单条最长 255 字节</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public TextResourceRecord(Domain domain, IList<CharacterString> characterStrings,
                TimeSpan ttl = default(TimeSpan)) : base(Create(domain, characterStrings, ttl))
        {
            TextData = new ReadOnlyCollection<CharacterString>(characterStrings);
        }

        /// <summary>
        /// 用属性名与属性值构造 TXT 记录，按 name=value 写入并对属性名中的等号、反引号转义
        /// </summary>
        /// <param name="domain">记录所属域名</param>
        /// <param name="attributeName">属性名</param>
        /// <param name="attributeValue">属性值</param>
        /// <param name="ttl">缓存有效期，默认零时长</param>
        public TextResourceRecord(Domain domain, string attributeName, string attributeValue,
                TimeSpan ttl = default(TimeSpan)) :
                this(domain, FormatAttributeNameValue(attributeName, attributeValue), ttl)
        { }

        /// <summary>
        /// 记录中的字符串列表，为只读视图
        /// </summary>
        public IList<CharacterString> TextData
        {
            get;
            private set;
        }

        /// <summary>
        /// 把文本内容解析为属性键值对，缺少未转义等号时键为 null
        /// </summary>
        public KeyValuePair<string, string> Attribute
        {
            get
            {
                string text = ToStringTextData();
                Match match = PATTERN_TXT_RECORD.Match(text);

                if (match.Success)
                {
                    string attributeName = (match.Groups[1].Length > 0) ?
                        Unescape(Trim(match.Groups[1].ToString())) : null;
                    string attributeValue = Unescape(match.Groups[2].ToString());
                    return new KeyValuePair<string, string>(attributeName, attributeValue);
                }
                else
                {
                    return new KeyValuePair<string, string>(null, Unescape(text));
                }
            }
        }

        /// <summary>
        /// 按 ASCII 拼接全部字符串内容
        /// </summary>
        /// <returns>拼接后的文本</returns>
        public string ToStringTextData()
        {
            return ToStringTextData(Encoding.ASCII);
        }

        /// <summary>
        /// 按指定编码拼接全部字符串内容
        /// </summary>
        /// <param name="encoding">解码字符串所用的编码</param>
        /// <returns>拼接后的文本</returns>
        public string ToStringTextData(Encoding encoding)
        {
            return String.Join(string.Empty, TextData.Select(c => c.ToString(encoding)));
        }

        /// <summary>
        /// 输出记录名、类型、类别、有效期、数据长度与文本内容
        /// </summary>
        /// <returns>字段拼接后的文本</returns>
        public override string ToString()
        {
            return Stringify().Add("TextData", (object)ToStringTextData()).ToString();
        }
    }
}
