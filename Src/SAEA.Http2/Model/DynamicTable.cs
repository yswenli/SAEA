/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：DynamicTable
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 14:13:19
*描述：
*=====================================================================
*修改时间：2019/6/27 14:13:19
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 动态头表
    /// </summary>
    public class DynamicTable
    {
        private List<TableEntry> _entries = new List<TableEntry>();

        private int _maxTableSize;

        private int _usedSize = 0;

        public int MaxTableSize
        {
            get { return this._maxTableSize; }

            set
            {
                if (value >= this._maxTableSize)
                {
                    this._maxTableSize = value;
                    return;
                }

                this._maxTableSize = value;
                this.EvictTo(value);
            }
        }

        public int UsedSize => this._usedSize;


        public int Length => this._entries.Count;

        public TableEntry GetAt(int index)
        {
            if (index < 0 || index >= this._entries.Count)
                throw new IndexOutOfRangeException();
            var elem = this._entries[index];
            return elem;
        }

        public DynamicTable(int maxTableSize)
        {
            this._maxTableSize = maxTableSize;
        }

        private void EvictTo(int newSize)
        {
            if (newSize < 0) newSize = 0;

            var delCount = 0;
            var used = this._usedSize;
            var index = this._entries.Count - 1; 

            while (used > newSize && index >= 0)
            {
                var item = this._entries[index];
                used -= (32 + item.NameLen + item.ValueLen);
                index--;
                delCount++;
            }

            if (delCount == 0) return;
            else if (delCount == this._entries.Count)
            {
                this._entries.Clear();
                this._usedSize = 0;
            }
            else
            {
                this._entries.RemoveRange(this._entries.Count - delCount, delCount);
                this._usedSize = used;
            }
        }


        public bool Insert(string name, int nameBytes, string value, int valueBytes)
        {

            var entrySize = 32 + nameBytes + valueBytes;


            var maxUsedSize = this._maxTableSize - entrySize;
            if (maxUsedSize < 0) maxUsedSize = 0;
            this.EvictTo(maxUsedSize);


            if (entrySize > this._maxTableSize) return false;


            var entry = new TableEntry
            {
                Name = name,
                NameLen = nameBytes,
                Value = value,
                ValueLen = valueBytes,
            };

            this._entries.Insert(0, entry);

            this._usedSize += entrySize;

            return true;
        }

        public int GetBestMatchingIndex(HeaderField field, out bool isFullMatch)
        {
            var bestMatch = -1;

            isFullMatch = false;

            var i = 0;

            foreach (var entry in _entries)
            {
                if (entry.Name == field.Name)
                {
                    if (bestMatch == -1)
                    {
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

            return bestMatch;
        }
    }
}
