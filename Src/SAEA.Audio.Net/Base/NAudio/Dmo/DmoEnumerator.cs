using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.Audio.NAudio.Dmo
{
	public class DmoEnumerator
	{
		public static IEnumerable<DmoDescriptor> GetAudioEffectNames()
		{
			return DmoEnumerator.GetDmos(DmoGuids.DMOCATEGORY_AUDIO_EFFECT);
		}

		public static IEnumerable<DmoDescriptor> GetAudioEncoderNames()
		{
			return DmoEnumerator.GetDmos(DmoGuids.DMOCATEGORY_AUDIO_ENCODER);
		}

		public static IEnumerable<DmoDescriptor> GetAudioDecoderNames()
		{
			return DmoEnumerator.GetDmos(DmoGuids.DMOCATEGORY_AUDIO_DECODER);
		}

		private static IEnumerable<DmoDescriptor> GetDmos(Guid category)
		{
			IEnumDmo enumDmo;
			Marshal.ThrowExceptionForHR(DmoInterop.DMOEnum(ref category, DmoEnumFlags.None, 0, null, 0, null, out enumDmo));
			int num;
			do
			{
				Guid clsid;
				IntPtr ptr;
				enumDmo.Next(1, out clsid, out ptr, out num);
				if (num == 1)
				{
					string name = Marshal.PtrToStringUni(ptr);
					Marshal.FreeCoTaskMem(ptr);
					yield return new DmoDescriptor(name, clsid);
				}
			}
			while (num > 0);
			yield break;
		}
	}
}
