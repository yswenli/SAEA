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
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TextResourceRecord记录类
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
    /// �ı���Դ��¼
    /// </summary>
    public class TextResourceRecord : BaseResourceRecord
    {
        ///��������/ֵƥ���������ʽ��
        ///��һ��δת��Ⱥ�������/ֵ�ָ����
        private static readonly Regex PATTERN_TXT_RECORD = new Regex(@"^([ -~]*?)(?<!`)=([ -~]*)$");

        ///ƥ��δת��ǰ��/β��հ׵�������ʽ��
        private static readonly Regex PATTERN_TRIM_NAME = new Regex(@"^\s+|((?<!`)\s)+$");

        ///ƥ��δת���ַ���������ʽ��
        private static readonly Regex PATTERN_ESCAPE = new Regex(@"([`=])");

        ///ƥ��ת���ַ���������ʽ��
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

        public TextResourceRecord(IResourceRecord record) :
            base(record)
        {
            TextData = CharacterString.GetAllFromArray(Data, 0);
        }

        public TextResourceRecord(Domain domain, IList<CharacterString> characterStrings,
                TimeSpan ttl = default(TimeSpan)) : base(Create(domain, characterStrings, ttl))
        {
            TextData = new ReadOnlyCollection<CharacterString>(characterStrings);
        }

        public TextResourceRecord(Domain domain, string attributeName, string attributeValue,
                TimeSpan ttl = default(TimeSpan)) :
                this(domain, FormatAttributeNameValue(attributeName, attributeValue), ttl)
        { }

        public IList<CharacterString> TextData
        {
            get;
            private set;
        }

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

        public string ToStringTextData()
        {
            return ToStringTextData(Encoding.ASCII);
        }

        public string ToStringTextData(Encoding encoding)
        {
            return String.Join(string.Empty, TextData.Select(c => c.ToString(encoding)));
        }

        public override string ToString()
        {
            return Stringify().Add("TextData", (object)ToStringTextData()).ToString();
        }
    }
}
