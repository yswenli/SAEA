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
*命名空间：SAEA.Common.NameValue
*文件名： NameValueCollection
*版本号： v26.4.23.1
*唯一标识：78cb0f73-7d3f-4da8-bf6d-7125f63b8c87
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：NameValueCollection接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common.NameValue
{
    /// <summary>
    /// Name/Value集合
    /// </summary>
    public class NameValueCollection : IEnumerable<NameValueItem>
    {
        List<NameValueItem> _list = new List<NameValueItem>();

        /// <summary>
        /// 按名称读写值，读取时忽略大小写，不存在返回 null，写入时覆盖同名项
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>对应值</returns>
        public object this[string name]
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

        /// <summary>
        /// 获取按插入顺序排列的枚举器
        /// </summary>
        /// <returns>项枚举器</returns>
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

        /// <summary>
        /// 按名称和值添加一项，不检查重名
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public void Add(string name, object value)
        {
            var header = new NameValueItem() { Name = name, Value = value };

            Add(header);
        }

        /// <summary>
        /// 添加一项，不检查重名
        /// </summary>
        /// <param name="header">待添加项</param>
        public void Add(NameValueItem header)
        {
            _list.Add(header);
        }

        /// <summary>
        /// 按名称查找项，忽略大小写
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>第一个匹配项，不存在返回 null</returns>
        public NameValueItem Get(string name)
        {
            return _list.Where(b => string.Compare(b.Name, name, true) == 0).FirstOrDefault();
        }

        /// <summary>
        /// 尝试按名称取值，忽略大小写
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">取值结果，未找到时为 null</param>
        /// <returns>找到返回 true</returns>
        public bool TryGetValue(string name, out object value)
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

        /// <summary>
        /// 判断是否存在名称与指定名称字符串比较结果大于等于 0 的项
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns>存在满足比较条件的项返回 true</returns>
        public bool ContainsName(string name)
        {
            return _list.Exists(b => string.Compare(b.Name, name) > -1);
        }

        /// <summary>
        /// 判断是否存在名称字符串比较结果大于等于 0 且值相等的项
        /// </summary>
        /// <param name="header">待比对的项</param>
        /// <returns>存在满足条件的项返回 true</returns>
        public bool Exists(NameValueItem header)
        {
            return _list.Exists(b => string.Compare(b.Name, header.Name) > -1 && b.Value == header.Value);
        }

        /// <summary>
        /// 按名称移除第一个匹配项，忽略大小写，不存在时忽略
        /// </summary>
        /// <param name="name">名称</param>
        public void Remove(string name)
        {
            var item = Get(name);
            if (item != null)
            {
                _list.Remove(item);
            }
        }

        /// <summary>
        /// 移除指定项，值相等的同名项存在时才真正删除
        /// </summary>
        /// <param name="header">待移除的项</param>
        public void Remove(NameValueItem header)
        {
            if (Exists(header))
            {
                _list.Remove(header);
            }
        }

        /// <summary>
        /// 按名称设置值，已存在同名项时替换，否则追加
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public void Set(string name, object value)
        {
            var header = new NameValueItem() { Name = name, Value = value };
            Set(header);
        }

        /// <summary>
        /// 设置一项，已存在同名项时移除旧项后追加到末尾
        /// </summary>
        /// <param name="header">待设置的项</param>
        public void Set(NameValueItem header)
        {
            var item = Get(header.Name);
            if (item != null)
            {
                Remove(item);
            }
            Add(header);
        }


        /// <summary>
        /// 项数量
        /// </summary>
        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        /// <summary>
        /// 所有名称数组，按插入顺序
        /// </summary>
        public string[] Names
        {
            get
            {
                return _list.Select(b => b.Name).ToArray();
            }
        }
        /// <summary>
        /// 所有值数组，按插入顺序
        /// </summary>
        public object[] Values
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

        /// <summary>
        /// 拼接所有项为 Name:名称,Value:值; 格式的字符串
        /// </summary>
        /// <returns>拼接结果，集合为空时返回空字符串</returns>
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

        /// <summary>
        /// 清空所有项
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }
    }

    /// <summary>
    /// Name/Value 集合扩展
    /// </summary>
    public static class NameValueCollectionExtends
    {
        /// <summary>
        /// 将字典转换为 Name/Value 集合，保持字典的枚举顺序
        /// </summary>
        /// <param name="dic">源字典，为 null 或空时返回空集合</param>
        /// <returns>新的 Name/Value 集合</returns>
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