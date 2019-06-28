/****************************************************************************
*项目名称：SAEA.Http2.HPack
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.HPack
*类 名 称：Decoder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 14:03:42
*描述：
*=====================================================================
*修改时间：2019/6/27 14:03:42
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;
using System.Buffers;

namespace SAEA.Http2.HPack
{
    /// <summary>
    /// HPACK decoder
    /// </summary>
    public class Decoder : IDisposable
    {
        /// <summary>
        /// 创建hpack解码器的选项
        /// </summary>
        public struct Options
        {
            /// <summary>
            /// 限制大小的动态表
            /// 这个限制可能不是exceeded by size frames表的更新
            /// 默认值4096
            /// </summary>
            public int? DynamicTableSizeLimit;

            /// <summary>
            /// 动态表的起始大小
            /// </summary>
            public int? DynamicTableSize;

            /// <summary>
            /// 接收字符串的最大长度
            /// </summary>
            public int? MaxStringLength;

            /// <summary>
            /// 应该从中租用缓冲区进行字符串解码的缓冲池
            /// </summary>
            public ArrayPool<byte> BufferPool;
        }

        private enum TaskType
        {
            None,
            StartReadInt,
            ContReadInt,
            StartReadString,
            ContReadString,
            HandleFullyIndexed,
            HandleNameIndexed,
            HandleNoneIndexed,
            HandleTableUpdate,
        }

        private struct Task
        {
            public TaskType Type;
            public int IntData;
            public string StringData;
        }

        /// <summary>
        /// 解码是否完成。这是在调用decode（）后设置的。如果可以从输入缓冲区解码完整的头字段，则值为true。在这种情况下，HeaderField将存储在HeaderField成员中，其长度存储在HeaderSize成员中。
        /// </summary>
        public bool Done = true;

        /// <summary>
        /// 控制在下次解码调用时解码表更新是否有效。应在每个头段块的开头将其设置为true。
        /// </summary>
        public bool AllowTableSizeUpdates = true;

        /// <summary>
        /// 返回hpack解码器是否处于初始状态，在该状态下需要头块片段的开头。如果未设置，则表示hpack解码器正在等待更多数据以处理头数据。
        /// </summary>
        public bool HasInitialState => _curTask == -1;

        /// <summary>解码操作的结果</summary>
        public HeaderField HeaderField;

        /// <summary>
        /// 根据hpack解码规则返回头字段的大小，这意味着名称的大小，值+32
        /// </summary>
        public int HeaderSize;

        private int _dynamicTableSizeLimit;
        private HeaderTable _headerTable;
        private IntDecoder _intDecoder = new IntDecoder();
        private StringDecoder _stringDecoder;


        private Task[] _tasks = new Task[] {
            new Task{ Type = TaskType.None },
            new Task{ Type = TaskType.None },
            new Task{ Type = TaskType.None }
        };
        private int _curTask = -1;


        private bool _addToTable = false;
        private bool _sensitive = false;


        public int DynamicTableSize => this._headerTable.MaxDynamicTableSize;


        public int DynamicTableUsedSize => this._headerTable.UsedDynamicTableSize;


        public int DynamicTableLength => this._headerTable.DynamicTableLength;

        public int DynamicTableSizeLimit => this._dynamicTableSizeLimit;

        public Decoder() : this(null)
        {
        }

        /// <summary>
        /// Creates a new HPACK decoder
        /// </summary>
        /// <param name="options">Decoder options</param>
        public Decoder(Options? options)
        {
            var dynamicTableSize = Defaults.DynamicTableSize;
            this._dynamicTableSizeLimit = Defaults.DynamicTableSizeLimit;
            var maxStringLength = Defaults.MaxStringLength;
            ArrayPool<byte> bufferPool = null;

            if (options.HasValue)
            {
                var opts = options.Value;
                if (opts.DynamicTableSize.HasValue)
                {
                    dynamicTableSize = opts.DynamicTableSize.Value;
                }
                if (opts.DynamicTableSizeLimit.HasValue)
                {
                    this._dynamicTableSizeLimit = opts.DynamicTableSizeLimit.Value;
                }
                if (opts.MaxStringLength.HasValue)
                {
                    maxStringLength = opts.MaxStringLength.Value;
                }
                if (opts.BufferPool != null)
                {
                    bufferPool = opts.BufferPool;
                }
            }

            if (bufferPool == null)
            {
                bufferPool = ArrayPool<byte>.Shared;
            }

            this._stringDecoder = new StringDecoder(maxStringLength, bufferPool);

            if (dynamicTableSize > this._dynamicTableSizeLimit)
                throw new ArgumentException("Dynamic table size must not exceeded limit");

            this._headerTable = new HeaderTable(dynamicTableSize);
        }

        public void Dispose()
        {
            _stringDecoder.Dispose();
            _stringDecoder = null;
        }

        private void Reset()
        {
            this._curTask = -1;
            this._addToTable = false;
            this._sensitive = false;
        }


        private void HandleDecodeIndexed()
        {
            var idx = this._tasks[0].IntData;
            this.Reset();
            var tableHeader = this._headerTable.GetAt(idx);

            AllowTableSizeUpdates = false;
            Done = true;
            HeaderField = new HeaderField
            {
                Name = tableHeader.Name,
                Value = tableHeader.Value,
                Sensitive = false
            };
            HeaderSize = 32 + tableHeader.NameLen + tableHeader.ValueLen;
        }


        private void HandleDecodeNameIndexed()
        {
            var idx = this._tasks[0].IntData;
            var tableHeader = this._headerTable.GetAt(idx); 
            var val = this._tasks[1].StringData;
            var valLen = this._tasks[1].IntData;
            var sensitive = this._sensitive;

            if (this._addToTable)
            {
                this._headerTable.Insert(tableHeader.Name, tableHeader.NameLen, val, valLen);
            }

            this.Reset(); 

            AllowTableSizeUpdates = false;
            Done = true;
            HeaderField = new HeaderField
            {
                Name = tableHeader.Name,
                Value = val,
                Sensitive = sensitive,
            };
            HeaderSize = 32 + tableHeader.NameLen + valLen;
        }


        private void HandleDecodeNoneIndexed()
        {
            var key = this._tasks[0].StringData;
            var keyLen = this._tasks[0].IntData;
            var val = this._tasks[1].StringData;
            var valLen = this._tasks[1].IntData;
            var sensitive = this._sensitive;

            if (this._addToTable)
            {
                this._headerTable.Insert(key, keyLen, val, valLen);
            }

            this.Reset(); 

            AllowTableSizeUpdates = false;
            Done = true;
            HeaderField = new HeaderField
            {
                Name = key,
                Value = val,
                Sensitive = sensitive,
            };
            HeaderSize = 32 + keyLen + valLen;
        }

        private void HandleTableUpdate()
        {
            var newLen = this._tasks[0].IntData;
            this.Reset();
            if (newLen > this._dynamicTableSizeLimit)
            {
                throw new Exception("table size limit exceeded");
            }
            this._headerTable.MaxDynamicTableSize = newLen;
        }

        /// <summary>
        /// 处理一块hpack字节
        /// </summary>
        /// <returns></returns>
        public int Decode(ArraySegment<byte> input)
        {
            if (input.Array == null) throw new ArgumentException(nameof(input));
            var offset = input.Offset;
            var count = input.Count;

            for (; ; )
            {
                var segment = new ArraySegment<byte>(input.Array, offset, count);
                if (this._curTask == -1)
                {
                    this.Done = false; 
                    if (count < 1) break;
                    
                    var consumed = HandleStartOfPacket(segment);
                    offset += consumed;
                    count -= consumed;
                }
                else
                {
                    bool executeMoreTasks;
                    var consumed = ExecutePendingTask(segment, out executeMoreTasks);
                    offset += consumed;
                    count -= consumed;
                    if (!executeMoreTasks)
                    {
                        break;
                    }
                }
            }

            return offset - input.Offset;
        }


        private int HandleStartOfPacket(ArraySegment<byte> buf)
        {
            var startByte = buf.Array[buf.Offset];

            this._curTask = 0;
            if ((startByte & 0x80) == 0x80)
            {
                this._tasks[0].Type = TaskType.StartReadInt;
                this._tasks[0].IntData = 7;
                this._tasks[1].Type = TaskType.HandleFullyIndexed;
                return 0;
            }
            else if ((startByte & 0xC0) == 0x40)
            {
                this._addToTable = true;
                if (startByte == 0x40)
                {
                    this._tasks[0].Type = TaskType.StartReadString;
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNoneIndexed;
                    return 1;
                }
                else
                {
                    this._tasks[0].Type = TaskType.StartReadInt;
                    this._tasks[0].IntData = 6; 
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNameIndexed;
                    return 0;
                }
            }
            else if ((startByte & 0xF0) == 0x00)
            {
                if (startByte == 0x00)
                {
                    this._tasks[0].Type = TaskType.StartReadString;
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNoneIndexed;
                    return 1;
                }
                else
                {
                    this._tasks[0].Type = TaskType.StartReadInt;
                    this._tasks[0].IntData = 4; 
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNameIndexed;
                    return 0;
                }
            }
            else if ((startByte & 0xF0) == 0x10)
            {
                this._sensitive = true;
                if (startByte == 0x10)
                {
                    this._tasks[0].Type = TaskType.StartReadString;
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNoneIndexed;
                    return 1;
                }
                else
                {
                    this._tasks[0].Type = TaskType.StartReadInt;
                    this._tasks[0].IntData = 4; 
                    this._tasks[1].Type = TaskType.StartReadString;
                    this._tasks[2].Type = TaskType.HandleNameIndexed;
                    return 0;
                }
            }
            else if ((startByte & 0xE0) == 0x20)
            {
                if (!AllowTableSizeUpdates)
                {
                    throw new Exception("Table update is not allowed");
                }
                this._tasks[0].Type = TaskType.StartReadInt;
                this._tasks[0].IntData = 5; 
                this._tasks[1].Type = TaskType.HandleTableUpdate;
                return 0;
            }
            else
            {
                throw new Exception("Invalid frame");
            }
        }

        private int ExecutePendingTask(ArraySegment<byte> buf, out bool executeMoreTasks)
        {
            executeMoreTasks = false;

            var offset = buf.Offset;
            var count = buf.Count;

            var currentTask = this._tasks[this._curTask];
            if (currentTask.Type == TaskType.StartReadInt)
            {
                if (count < 1) return 0;
                var consumed = this._intDecoder.Decode(currentTask.IntData, buf);
                offset += consumed;
                count -= consumed;
                if (this._intDecoder.Done)
                {
                    this._tasks[this._curTask].IntData = this._intDecoder.Result;
                    
                    this._curTask++;
                    executeMoreTasks = true;
                }
                else
                {
                    this._tasks[this._curTask].Type = TaskType.ContReadInt;
                }
            }
            else if (currentTask.Type == TaskType.ContReadInt)
            {
                var consumed = this._intDecoder.DecodeCont(buf);
                offset += consumed;
                count -= consumed;
                if (this._intDecoder.Done)
                {
                    this._tasks[this._curTask].IntData = this._intDecoder.Result;
                    this._curTask++;
                    executeMoreTasks = true;
                }
                else
                {
                    //todo
                }
            }
            else if (currentTask.Type == TaskType.StartReadString)
            {
                if (count < 1) return 0;
                var consumed = this._stringDecoder.Decode(buf);
                offset += consumed;
                count -= consumed;
                if (this._stringDecoder.Done)
                {
                    this._tasks[this._curTask].IntData = this._stringDecoder.StringLength;
                    this._tasks[this._curTask].StringData = this._stringDecoder.Result;
                   
                    this._curTask++;
                    executeMoreTasks = true;
                }
                else
                {
                    this._tasks[this._curTask].Type = TaskType.ContReadString;
                }
            }
            else if (currentTask.Type == TaskType.ContReadString)
            {
                var consumed = this._stringDecoder.DecodeCont(buf);
                offset += consumed;
                count -= consumed;
                if (this._stringDecoder.Done)
                {
                    this._tasks[this._curTask].IntData = this._stringDecoder.StringLength;
                    this._tasks[this._curTask].StringData = this._stringDecoder.Result;
                    
                    this._curTask++;
                    executeMoreTasks = true;
                }
                else
                {
                    //todo
                }
            }
            else if (currentTask.Type == TaskType.HandleNoneIndexed)
            {
                this.HandleDecodeNoneIndexed();
            }
            else if (currentTask.Type == TaskType.HandleNameIndexed)
            {
                this.HandleDecodeNameIndexed();
            }
            else if (currentTask.Type == TaskType.HandleFullyIndexed)
            {
                this.HandleDecodeIndexed();
            }
            else if (currentTask.Type == TaskType.HandleTableUpdate)
            {
                this.HandleTableUpdate();
                executeMoreTasks = true;
            }
            else
            {
                throw new Exception("invalid task");
            }

            return offset - buf.Offset;
        }
    }
}
