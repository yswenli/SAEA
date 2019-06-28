/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：Encoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:38:14
*描述：
*=====================================================================
*修改时间：2019/6/27 15:38:14
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// HPACK encoder
    /// </summary>
    public class Encoder
    {

        public struct Options
        {
            public int? DynamicTableSize;

            public HuffmanStrategy? HuffmanStrategy;
        }


        public struct Result
        {
            public int UsedBytes;

            public int FieldCount;
        }

        HeaderTable _headerTable;
        HuffmanStrategy _huffmanStrategy;

        int _tableSizeUpdateMinValue = -1;

        int _tableSizeUpdateFinalValue = -1;

        public int DynamicTableSize
        {
            get { return this._headerTable.MaxDynamicTableSize; }
            set
            {
                var current = _headerTable.MaxDynamicTableSize;
                if (current == value) return;
                _tableSizeUpdateFinalValue = value;
                if (_tableSizeUpdateMinValue == -1 || value < _tableSizeUpdateMinValue)
                {
                    _tableSizeUpdateMinValue = value;
                }
                this._headerTable.MaxDynamicTableSize = value;
            }
        }

        public int DynamicTableUsedSize
        {
            get { return this._headerTable.UsedDynamicTableSize; }
        }

        public int DynamicTableLength
        {
            get { return this._headerTable.DynamicTableLength; }
        }


        public Encoder() : this(null)
        {
        }

        public Encoder(Options? options)
        {
            var dynamicTableSize = Defaults.DynamicTableSize;
            this._huffmanStrategy = HuffmanStrategy.IfSmaller;

            if (options.HasValue)
            {
                var opts = options.Value;
                if (opts.DynamicTableSize.HasValue)
                {
                    dynamicTableSize = opts.DynamicTableSize.Value;
                }
                if (opts.HuffmanStrategy.HasValue)
                {
                    this._huffmanStrategy = opts.HuffmanStrategy.Value;
                }
            }
            this._headerTable = new HeaderTable(dynamicTableSize);
        }
        public Result EncodeInto(
            ArraySegment<byte> buf,
            IEnumerable<HeaderField> headers)
        {
            var nrEncodedHeaders = 0;
            var offset = buf.Offset;
            var count = buf.Count;

            var tableUpdatesOk = true;
            if (_tableSizeUpdateMinValue != -1 &&
                _tableSizeUpdateMinValue != _tableSizeUpdateFinalValue)
            {
                var used = IntEncoder.EncodeInto(
                    new ArraySegment<byte>(buf.Array, offset, count),
                    this._tableSizeUpdateMinValue, 0x20, 5);
                if (used == -1)
                {
                    tableUpdatesOk = false;
                }
                else
                {
                    offset += used;
                    count -= used;
                    _tableSizeUpdateMinValue = -1;
                }
            }
            if (_tableSizeUpdateFinalValue != -1)
            {
                var used = IntEncoder.EncodeInto(
                    new ArraySegment<byte>(buf.Array, offset, count),
                    this._tableSizeUpdateFinalValue, 0x20, 5);
                if (used == -1)
                {
                    tableUpdatesOk = false;
                }
                else
                {
                    offset += used;
                    count -= used;
                    _tableSizeUpdateMinValue = -1;
                    _tableSizeUpdateFinalValue = -1;
                }
            }

            if (!tableUpdatesOk)
            {
                return new Result
                {
                    UsedBytes = 0,
                    FieldCount = 0,
                };
            }

            foreach (var header in headers)
            {
                bool fullMatch;
                var idx = _headerTable.GetBestMatchingIndex(header, out fullMatch);
                var nameMatch = idx != -1;

                var tempOffset = offset;

                if (fullMatch)
                {
                    var used = IntEncoder.EncodeInto(
                        new ArraySegment<byte>(buf.Array, tempOffset, count),
                        idx, 0x80, 7);
                    if (used == -1) break;
                    tempOffset += used;
                    count -= used;
                }
                else
                {
                    var nameLen = StringEncoder.GetByteLength(header.Name);
                    var valLen = StringEncoder.GetByteLength(header.Value);

                    var addToIndex = false;
                    var neverIndex = false;
                    if (header.Sensitive)
                    {
                        neverIndex = true;
                    }
                    else
                    {
                        if (this.DynamicTableSize >= 32 + nameLen + valLen)
                        {
                            addToIndex = true;
                        }
                    }

                    if (addToIndex)
                    {
                        if (nameMatch)
                        {
                            var used = IntEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, tempOffset, count),
                                idx, 0x40, 6);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                        else
                        {
                            if (count < 1) break;
                            buf.Array[tempOffset] = 0x40;
                            tempOffset += 1;
                            count -= 1;
                            var used = StringEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, tempOffset, count),
                                header.Name, nameLen, this._huffmanStrategy);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                        this._headerTable.Insert(header.Name, nameLen, header.Value, valLen);
                    }
                    else if (!neverIndex)
                    {
                        if (nameMatch)
                        {
                            var used = IntEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, tempOffset, count),
                                idx, 0x00, 4);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                        else
                        {
                            if (count < 1) break;
                            buf.Array[tempOffset] = 0x00;
                            tempOffset += 1;
                            count -= 1;
                            var used = StringEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, tempOffset, count),
                                header.Name, nameLen, this._huffmanStrategy);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                    }
                    else
                    {
                        if (nameMatch)
                        {
                            var used = IntEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, offset, count),
                                idx, 0x10, 4);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                        else
                        {
                            if (count < 1) break;
                            buf.Array[tempOffset] = 0x10;
                            tempOffset += 1;
                            count -= 1;
                            var used = StringEncoder.EncodeInto(
                                new ArraySegment<byte>(buf.Array, tempOffset, count),
                                header.Name, nameLen, this._huffmanStrategy);
                            if (used == -1) break;
                            tempOffset += used;
                            count -= used;
                        }
                    }

                    var usedForValue = StringEncoder.EncodeInto(
                        new ArraySegment<byte>(buf.Array, tempOffset, count),
                        header.Value, valLen, this._huffmanStrategy);
                    if (usedForValue == -1) break;

                    tempOffset += usedForValue;
                    count -= usedForValue;
                }

                offset = tempOffset;
                nrEncodedHeaders++;
            }

            return new Result
            {
                UsedBytes = offset - buf.Offset,
                FieldCount = nrEncodedHeaders,
            };
        }
    }
}
