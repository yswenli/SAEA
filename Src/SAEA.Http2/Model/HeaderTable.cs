/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：HeaderTable
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 14:12:25
*描述：
*=====================================================================
*修改时间：2019/6/27 14:12:25
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 静态和动态头表的组合
    /// </summary>
    public class HeaderTable
    {
        DynamicTable dynamic;

        public HeaderTable(int dynamicTableSize)
        {
            this.dynamic = new DynamicTable(dynamicTableSize);
        }

        public int MaxDynamicTableSize
        {
            get { return this.dynamic.MaxTableSize; }
            set { this.dynamic.MaxTableSize = value; }
        }


        public int UsedDynamicTableSize => this.dynamic.UsedSize;


        public int DynamicTableLength => this.dynamic.Length;

        /// <summary>
        /// 在标题表中插入新元素
        /// </summary>
        public bool Insert(string name, int nameBytes, string value, int valueBytes)
        {
            return dynamic.Insert(name, nameBytes, value, valueBytes);
        }

        public TableEntry GetAt(int index)
        {
            if (index < 1) throw new IndexOutOfRangeException();

            // 将索引与静态表的开头关联，并查看元素是否在其中
            index--;
            if (index < StaticTable.Entries.Length)
            {
                return StaticTable.Entries[index];
            }

            index -= StaticTable.Entries.Length;
            if (index < this.dynamic.Length)
            {
                return this.dynamic.GetAt(index);
            }

            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// 返回头表中最佳匹配元素的索引。
        /// 如果没有找到索引，返回值为-1。
        /// 如果找到索引，并且名称和值match isfullmatch将设置为true。
        /// </summary>
        public int GetBestMatchingIndex(HeaderField field, out bool isFullMatch)
        {
            var bestMatch = -1;
            isFullMatch = false;

            var i = 1;
            foreach (var entry in StaticTable.Entries)
            {
                if (entry.Name == field.Name)
                {
                    if (bestMatch == -1)
                    {
                        // 使用了最低匹配字段索引，这使得搜索接收器的效率最高，并且提供了使用静态表的最高机会。
                        bestMatch = i;
                    }

                    if (entry.Value == field.Value)
                    {
                        isFullMatch = true;
                        return i;
                    }
                }
                i++;
            }

            bool dynamicHasFullMatch;

            var di = this.dynamic.GetBestMatchingIndex(field, out dynamicHasFullMatch);

            if (dynamicHasFullMatch)
            {
                isFullMatch = true;
                return di + 1 + StaticTable.Length;
            }

            if (di != -1 && bestMatch == -1) bestMatch = di + 1 + StaticTable.Length;

            return bestMatch;
        }
    }
}
