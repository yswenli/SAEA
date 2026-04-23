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
*命名空间：SAEA.DNS.Common.Utils
*文件名： ObjectStringifier
*版本号： v26.4.23.1
*唯一标识：a1b2c3d4-e5f6-7890-abcd-ef1234567890
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/28 22:43:28
*描述：ObjectStringifier类，提供对象字符串序列化功能
*
*=====================================================================
*修改标记
*修改时间：2019/11/28 22:43:28
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ObjectStringifier类，提供对象字符串序列化功能
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SAEA.DNS.Common.Utils
{
    public class ObjectStringifier
    {
        public static ObjectStringifier New(object obj)
        {
            return new ObjectStringifier(obj);
        }

        public static string Stringify(object obj)
        {
            return StringifyObject(obj);
        }

        private static string StringifyObject(object obj)
        {
            if (obj is string)
            {
                return (string)obj;
            }
            else if (obj is IDictionary)
            {
                return StringifyDictionary((IDictionary)obj);
            }
            else if (obj is IEnumerable)
            {
                return StringifyList((IEnumerable)obj);
            }
            else
            {
                return obj == null ? "null" : obj.ToString();
            }
        }

        private static string StringifyList(IEnumerable enumerable)
        {
            return "[" + string.Join(", ", enumerable.Cast<object>().Select(o => StringifyObject(o)).ToArray()) + "]";
        }

        private static string StringifyDictionary(IDictionary dict)
        {
            StringBuilder result = new StringBuilder();

            result.Append("{");

            foreach (DictionaryEntry pair in dict)
            {
                result
                    .Append(pair.Key)
                    .Append("=")
                    .Append(StringifyObject(pair.Value))
                    .Append(", ");
            }

            if (result.Length > 1)
            {
                result.Remove(result.Length - 2, 2);
            }

            return result.Append("}").ToString();
        }

        private object obj;
        private Dictionary<string, string> pairs;

        public ObjectStringifier(object obj)
        {
            this.obj = obj;
            this.pairs = new Dictionary<string, string>();
        }

        public ObjectStringifier Remove(params string[] names)
        {
            foreach (string name in names)
            {
                pairs.Remove(name);
            }

            return this;
        }

        public ObjectStringifier Add(params string[] names)
        {
            Type type = obj.GetType();

            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                object value = property.GetValue(obj, new object[] { });

                pairs.Add(name, StringifyObject(value));
            }

            return this;
        }

        public ObjectStringifier Add(string name, object value)
        {
            pairs.Add(name, StringifyObject(value));
            return this;
        }

        public ObjectStringifier AddAll()
        {
            PropertyInfo[] properties = obj.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                object value = property.GetValue(obj, new object[] { });
                pairs.Add(property.Name, StringifyObject(value));
            }

            return this;
        }

        public override string ToString()
        {
            return StringifyDictionary(pairs);
        }
    }
}
