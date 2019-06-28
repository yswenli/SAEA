/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：Settings
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:03:20
*描述：
*=====================================================================
*修改时间：2019/6/27 16:03:20
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 存储HTTP/2连接的所有已知设置的结构。
    /// </summary>
    public struct Settings
    {
        public uint HeaderTableSize;

        public bool EnablePush;

        public uint MaxConcurrentStreams;

        public uint InitialWindowSize;

        public uint MaxFrameSize;

        public uint MaxHeaderListSize;

        public readonly static Settings Default = new Settings
        {
            HeaderTableSize = 4096,
            EnablePush = true,
            MaxConcurrentStreams = uint.MaxValue,
            InitialWindowSize = 65535,
            MaxFrameSize = 16384,
            MaxHeaderListSize = uint.MaxValue,
        };

        public readonly static Settings Min = new Settings
        {
            HeaderTableSize = 0,
            EnablePush = false,
            MaxConcurrentStreams = 0,
            InitialWindowSize = 0,
            MaxFrameSize = 16384,
            MaxHeaderListSize = 0,
        };

        public readonly static Settings Max = new Settings
        {
            HeaderTableSize = uint.MaxValue,
            EnablePush = true,
            MaxConcurrentStreams = uint.MaxValue,
            InitialWindowSize = int.MaxValue,
            MaxFrameSize = 16777215,
            MaxHeaderListSize = uint.MaxValue,
        };

        private void EncodeSingleSetting(
            ushort id, uint value, byte[] buffer, int offset)
        {
            // 16 bit ID
            buffer[offset + 0] = (byte)((id >> 8) & 0xFF);
            buffer[offset + 1] = (byte)((id) & 0xFF);

            // 32bit value
            buffer[offset + 2] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 4] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 5] = (byte)((value) & 0xFF);
        }

        public int RequiredSize => 6 * 6;

        public void EncodeInto(ArraySegment<byte> bytes)
        {
            var b = bytes.Array;
            var o = bytes.Offset;

            EncodeSingleSetting(
                (ushort)SettingId.EnablePush, EnablePush ? 1u : 0u, b, o);
            o += 6;
            EncodeSingleSetting(
                (ushort)SettingId.HeaderTableSize, HeaderTableSize, b, o);
            o += 6;
            EncodeSingleSetting(
                (ushort)SettingId.InitialWindowSize, InitialWindowSize, b, o);
            o += 6;
            EncodeSingleSetting(
                (ushort)SettingId.MaxConcurrentStreams, MaxConcurrentStreams, b, o);
            o += 6;
            EncodeSingleSetting(
                (ushort)SettingId.MaxFrameSize, MaxFrameSize, b, o);
            o += 6;
            EncodeSingleSetting(
                (ushort)SettingId.MaxHeaderListSize, MaxHeaderListSize, b, o);
        }

        public Http2Error? UpdateFromData(ArraySegment<byte> data)
        {
            if (data.Count % 6 != 0)
            {
                return new Http2Error
                {
                    StreamId = 0,
                    Code = ErrorCode.ProtocolError,
                    Message = "Invalid SETTINGS frame length",
                };
            }

            var b = data.Array;
            for (var o = data.Offset; o < data.Offset + data.Count; o = o + 6)
            {
                var id = (ushort)(
                    (b[o + 0] << 8)
                    | (ushort)b[o + 1]);
                var value =
                    ((uint)b[o + 2] << 24)
                    | ((uint)b[o + 3] << 16)
                    | ((uint)b[o + 4] << 8)
                    | (uint)b[o + 5];


                var err = this.UpdateFieldById(id, value);
                if (err != null)
                {
                    return err;
                }
            }

            return null;
        }

        /// <summary>
        /// 指示设置是否在有效范围内
        /// </summary>
        public bool Valid
        {
            get
            {
                if (HeaderTableSize < Min.HeaderTableSize ||
                    HeaderTableSize > Max.HeaderTableSize)
                {
                    return false;
                }
                if (InitialWindowSize < Min.InitialWindowSize ||
                    InitialWindowSize > Max.InitialWindowSize)
                {
                    return false;
                }
                if (MaxConcurrentStreams < Min.MaxConcurrentStreams ||
                    MaxConcurrentStreams > Max.MaxConcurrentStreams)
                {
                    return false;
                }
                if (MaxFrameSize < Min.MaxFrameSize ||
                    MaxFrameSize > Max.MaxFrameSize)
                {
                    return false;
                }
                if (MaxHeaderListSize < Min.MaxHeaderListSize ||
                    MaxHeaderListSize > Max.MaxHeaderListSize)
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 将具有给定ID的设置值修改为新值。
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Http2Error? UpdateFieldById(ushort id, uint value)
        {
            switch ((SettingId)id)
            {
                case SettingId.EnablePush:
                    if (value == 0 || value == 1)
                    {
                        EnablePush = value == 1 ? true : false;
                        return null;
                    }
                    break;
                case SettingId.HeaderTableSize:
                    if (value >= Settings.Min.HeaderTableSize &&
                        value <= Settings.Max.HeaderTableSize)
                    {
                        HeaderTableSize = value;
                        return null;
                    }
                    break;
                case SettingId.InitialWindowSize:
                    if (value >= Settings.Min.InitialWindowSize &&
                        value <= Settings.Max.InitialWindowSize)
                    {
                        InitialWindowSize = value;
                        return null;
                    }
                    break;
                case SettingId.MaxConcurrentStreams:
                    if (value >= Settings.Min.MaxConcurrentStreams &&
                        value <= Settings.Max.MaxConcurrentStreams)
                    {
                        MaxConcurrentStreams = value;
                        return null;
                    }
                    break;
                case SettingId.MaxFrameSize:
                    if (value >= Settings.Min.MaxFrameSize &&
                        value <= Settings.Max.MaxFrameSize)
                    {
                        MaxFrameSize = value;
                        return null;
                    }
                    break;
                case SettingId.MaxHeaderListSize:
                    if (value >= Settings.Min.MaxHeaderListSize &&
                        value <= Settings.Max.MaxHeaderListSize)
                    {
                        MaxHeaderListSize = value;
                        return null;
                    }
                    break;
                default:

                    return null;
            }

            var code = ErrorCode.ProtocolError;
            if ((SettingId)id == SettingId.InitialWindowSize)
            {
                code = ErrorCode.FlowControlError;
            }

            return new Http2Error
            {
                StreamId = 0,
                Code = code,
                Message = "Invalid value " + value + " for setting with ID " + id,
            };
        }
    }
}
