using SAEA.Audio.Base.NAudio.CoreAudioApi.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace SAEA.Audio.Base.NAudio.CoreAudioApi
{
    public class AudioMeterInformationChannels
	{
		private readonly IAudioMeterInformation audioMeterInformation;

		public int Count
		{
			get
			{
				int result;
				Marshal.ThrowExceptionForHR(this.audioMeterInformation.GetMeteringChannelCount(out result));
				return result;
			}
		}

		public float this[int index]
		{
			get
			{
				int count = this.Count;
				if (index >= count)
				{
					throw new ArgumentOutOfRangeException("index", string.Format("Peak index cannot be greater than number of channels ({0})", count));
				}
				float[] array = new float[this.Count];
				GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
				Marshal.ThrowExceptionForHR(this.audioMeterInformation.GetChannelsPeakValues(array.Length, gCHandle.AddrOfPinnedObject()));
				gCHandle.Free();
				return array[index];
			}
		}

		internal AudioMeterInformationChannels(IAudioMeterInformation parent)
		{
			this.audioMeterInformation = parent;
		}
	}
}
