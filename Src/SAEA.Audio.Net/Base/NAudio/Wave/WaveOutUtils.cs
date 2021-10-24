/****************************************************************************
*项目名称：SAEA.Audio.Base.NAudio.Wave
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Base.NAudio.Wave
*类 名 称：WaveOutUtils
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/26 19:18:32
*描述：
*=====================================================================
*修改时间：2021/1/26 19:18:32
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.Wave
{
    static class WaveOutUtils
    {
        public static float GetWaveOutVolume(IntPtr hWaveOut, object lockObject)
        {
            int stereoVolume;
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutGetVolume(hWaveOut, out stereoVolume);
            }
            MmException.Try(result, "waveOutGetVolume");
            return (stereoVolume & 0xFFFF) / (float)0xFFFF;
        }

        public static void SetWaveOutVolume(float value, IntPtr hWaveOut, object lockObject)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            float left = value;
            float right = value;

            int stereoVolume = (int)(left * 0xFFFF) + ((int)(right * 0xFFFF) << 16);
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutSetVolume(hWaveOut, stereoVolume);
            }
            MmException.Try(result, "waveOutSetVolume");
        }

        public static long GetPositionBytes(IntPtr hWaveOut, object lockObject)
        {
            lock (lockObject)
            {
                var mmTime = new MmTime();
                mmTime.wType = MmTime.TIME_BYTES; // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?
                MmException.Try(WaveInterop.waveOutGetPosition(hWaveOut, ref mmTime, Marshal.SizeOf(mmTime)), "waveOutGetPosition");

                if (mmTime.wType != MmTime.TIME_BYTES)
                    throw new Exception(string.Format("waveOutGetPosition: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType));

                return mmTime.cb;
            }
        }
    }
}
