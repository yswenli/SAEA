using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace SAEA.Audio.Base.NAudio.Wave
{
	public class RiffChunk
	{
		public int Identifier
		{
            get;private set;
		}

		public string IdentifierAsString
		{
			get
			{
				return Encoding.UTF8.GetString(BitConverter.GetBytes(this.Identifier));
			}
		}

		public int Length
		{
			get;
			private set;
		}

		public long StreamPosition
		{
			get;
			private set;
		}

		public RiffChunk(int identifier, int length, long streamPosition)
		{
			this.Identifier = identifier;
			this.Length = length;
			this.StreamPosition = streamPosition;
		}
	}
}
