using System;
using System.Collections.Generic;
using System.IO;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	internal abstract class StructureBuilder<T>
	{
		protected List<T> data;

		public abstract int Length
		{
			get;
		}

		public T[] Data
		{
			get
			{
				return this.data.ToArray();
			}
		}

		public StructureBuilder()
		{
			this.Reset();
		}

		public abstract T Read(BinaryReader br);

		public abstract void Write(BinaryWriter bw, T o);

		public void Reset()
		{
			this.data = new List<T>();
		}
	}
}
