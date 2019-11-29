/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.Utils
*类 名 称：ObjectStringifier
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
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
