using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioClockClient : IDisposable
	{
		private IAudioClock audioClockClientInterface;

		public int Characteristics
		{
			get
			{
				uint result;
				Marshal.ThrowExceptionForHR(this.audioClockClientInterface.GetCharacteristics(out result));
				return (int)result;
			}
		}

		public ulong Frequency
		{
			get
			{
				ulong result;
				Marshal.ThrowExceptionForHR(this.audioClockClientInterface.GetFrequency(out result));
				return result;
			}
		}

		public ulong AdjustedPosition
		{
			get
			{
				ulong num = 10000000uL / this.Frequency;
				int num2 = 0;
				ulong num3;
				ulong num4;
				while (!this.GetPosition(out num3, out num4) && ++num2 != 5)
				{
				}
				if (Stopwatch.IsHighResolution)
				{
					ulong num5 = ((ulong)(Stopwatch.GetTimestamp() * 10000000m / Stopwatch.Frequency) - num4) / 100uL / num;
					num3 += num5;
				}
				return num3;
			}
		}

		public bool CanAdjustPosition
		{
			get
			{
				return Stopwatch.IsHighResolution;
			}
		}

		internal AudioClockClient(IAudioClock audioClockClientInterface)
		{
			this.audioClockClientInterface = audioClockClientInterface;
		}

		public bool GetPosition(out ulong position, out ulong qpcPosition)
		{
			int position2 = this.audioClockClientInterface.GetPosition(out position, out qpcPosition);
			if (position2 == -1)
			{
				return false;
			}
			Marshal.ThrowExceptionForHR(position2);
			return true;
		}

		public void Dispose()
		{
			if (this.audioClockClientInterface != null)
			{
				Marshal.ReleaseComObject(this.audioClockClientInterface);
				this.audioClockClientInterface = null;
				GC.SuppressFinalize(this);
			}
		}
	}
}
