/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC.Http.Base
*文件名： Headers
*版本号： v5.0.0.1
*唯一标识：7ac31ac9-292a-46c3-b0ac-796d1b89e067
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/13 15:25:33
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/13 15:25:33
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// Name/Value集合
    /// </summary>
    public class NameValueCollection : IEnumerable<NameValueItem>
    {
        List<NameValueItem> _list = new List<NameValueItem>();

        public string this[string name]
        {
            get
            {
                var item = Get(name);
                if (item == null) return null;
                return Get(name).Value;
            }
            set
            {
                Set(name, value);
            }
        }

        public IEnumerator<NameValueItem> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator GetEnumerator1()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator1();
        }

        public void Add(string name, string value)
        {
            var header = new NameValueItem() { Name = name, Value = value };

            Add(header);
        }

        public void Add(NameValueItem header)
        {
            _list.Add(header);
        }

        public NameValueItem Get(string name)
        {
            return _list.Where(b => string.Compare(b.Name, name, true) == 0).FirstOrDefault();
        }

        public bool TryGetValue(string name, out string value)
        {
            value = null;

            var result = false;

            var item = Get(name);

            if (item != null)
            {
                value = item.Value;
                result = true;
            }
            return result;
        }

        public bool ContainsName(string name)
        {
            return _list.Exists(b => string.Compare(b.Name, name) > -1);
        }

        public bool Exists(NameValueItem header)
        {
            return _list.Exists(b => string.Compare(b.Name, header.Name) > -1 && b.Value == header.Value);
        }

        public void Remove(string name)
        {
            var item = Get(name);
            if (item != null)
            {
                _list.Remove(item);
            }
        }

        public void Remove(NameValueItem header)
        {
            if (Exists(header))
            {
                _list.Remove(header);
            }
        }

        public void Set(string name, string value)
        {
            var header = new NameValueItem() { Name = name, Value = value };
            Set(header);
        }

        public void Set(NameValueItem header)
        {
            var item = Get(header.Value);
            if (item != null)
            {
                Remove(item);
            }
            Add(header);
        }


        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        public string[] Names
        {
            get
            {
                return _list.Select(b => b.Name).ToArray();
            }
        }
        public string[] Values
        {
            get
            {
                return _list.Select(b => b.Value).ToArray();
            }
        }

        /// <summary>
        /// 获取object数组
        /// </summary>
        /// <returns></returns>
        public object[] ToValArray()
        {
            List<object> args = new List<object>();

            if (this.Count > 0)
            {
                foreach (var val in this.Values)
                {
                    args.Add(val);
                }
            }
            return args.ToArray();
        }

        public new string ToString()
        {
            if (!_list.Any())
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();

            foreach (var item in _list)
            {
                sb.Append($"Name:${item.Name},Value:{item.Value};");
            }
            return sb.ToString();
        }

        public void Clear()
        {
            _list.Clear();
        }
    }

    public static class NameValueCollectionExtends
    {
        public static NameValueCollection ToNameValueCollection(this Dictionary<string, string> dic)
        {
            NameValueCollection result = new NameValueCollection();

            if (dic != null && dic.Count > 0)
            {
                foreach (var item in dic)
                {
                    var nv = new NameValueItem()
                    {
                        Name = item.Key,
                        Value = item.Value
                    };
                    result.Add(nv);
                }
            }
            return result;
        }
    }
}
