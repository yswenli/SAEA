using SAEA.Audio.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.CoreAudioApi
{
    public class AudioStreamVolume : IDisposable
	{
		private IAudioStreamVolume audioStreamVolumeInterface;

		public int ChannelCount
		{
			get
			{
				uint result;
				Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.GetChannelCount(out result));
				return (int)result;
			}
		}

		internal AudioStreamVolume(IAudioStreamVolume audioStreamVolumeInterface)
		{
			this.audioStreamVolumeInterface = audioStreamVolumeInterface;
		}

		private void CheckChannelIndex(int channelIndex, string parameter)
		{
			int channelCount = this.ChannelCount;
			if (channelIndex >= channelCount)
			{
				throw new ArgumentOutOfRangeException(parameter, "You must supply a valid channel index < current count of channels: " + channelCount.ToString());
			}
		}

		public float[] GetAllVolumes()
		{
			uint num;
			Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.GetChannelCount(out num));
			float[] array = new float[num];
			Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.GetAllVolumes(num, array));
			return array;
		}

		public float GetChannelVolume(int channelIndex)
		{
			this.CheckChannelIndex(channelIndex, "channelIndex");
			float result;
			Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.GetChannelVolume((uint)channelIndex, out result));
			return result;
		}

		public void SetAllVolumes(float[] levels)
		{
			int channelCount = this.ChannelCount;
			if (levels == null)
			{
				throw new ArgumentNullException("levels");
			}
			if (levels.Length != channelCount)
			{
				throw new ArgumentOutOfRangeException("levels", string.Format(CultureInfo.InvariantCulture, "SetAllVolumes MUST be supplied with a volume level for ALL channels. The AudioStream has {0} channels and you supplied {1} channels.", new object[]
				{
					channelCount,
					levels.Length
				}));
			}
			for (int i = 0; i < levels.Length; i++)
			{
				float expr_56 = levels[i];
				if (expr_56 < 0f)
				{
					throw new ArgumentOutOfRangeException("levels", "All volumes must be between 0.0 and 1.0. Invalid volume at index: " + i.ToString());
				}
				if (expr_56 > 1f)
				{
					throw new ArgumentOutOfRangeException("levels", "All volumes must be between 0.0 and 1.0. Invalid volume at index: " + i.ToString());
				}
			}
			Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.SetAllVoumes((uint)channelCount, levels));
		}

		public void SetChannelVolume(int index, float level)
		{
			this.CheckChannelIndex(index, "index");
			if (level < 0f)
			{
				throw new ArgumentOutOfRangeException("level", "Volume must be between 0.0 and 1.0");
			}
			if (level > 1f)
			{
				throw new ArgumentOutOfRangeException("level", "Volume must be between 0.0 and 1.0");
			}
			Marshal.ThrowExceptionForHR(this.audioStreamVolumeInterface.SetChannelVolume((uint)index, level));
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing && this.audioStreamVolumeInterface != null)
			{
				Marshal.ReleaseComObject(this.audioStreamVolumeInterface);
				this.audioStreamVolumeInterface = null;
			}
		}
	}
}
