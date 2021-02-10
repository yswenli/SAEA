using System;

namespace SAEA.Audio.NAudio.SoundFont
{
	public class Generator
	{
		private GeneratorEnum generatorType;

		private ushort rawAmount;

		private Instrument instrument;

		private SampleHeader sampleHeader;

		public GeneratorEnum GeneratorType
		{
			get
			{
				return this.generatorType;
			}
			set
			{
				this.generatorType = value;
			}
		}

		public ushort UInt16Amount
		{
			get
			{
				return this.rawAmount;
			}
			set
			{
				this.rawAmount = value;
			}
		}

		public short Int16Amount
		{
			get
			{
				return (short)this.rawAmount;
			}
			set
			{
				this.rawAmount = (ushort)value;
			}
		}

		public byte LowByteAmount
		{
			get
			{
				return (byte)(this.rawAmount & 255);
			}
			set
			{
				this.rawAmount &= 65280;
				this.rawAmount += (ushort)value;
			}
		}

		public byte HighByteAmount
		{
			get
			{
				return (byte)((this.rawAmount & 65280) >> 8);
			}
			set
			{
				this.rawAmount &= 255;
				this.rawAmount += (ushort)(value << 8);
			}
		}

		public Instrument Instrument
		{
			get
			{
				return this.instrument;
			}
			set
			{
				this.instrument = value;
			}
		}

		public SampleHeader SampleHeader
		{
			get
			{
				return this.sampleHeader;
			}
			set
			{
				this.sampleHeader = value;
			}
		}

		public override string ToString()
		{
			if (this.generatorType == GeneratorEnum.Instrument)
			{
				return string.Format("Generator Instrument {0}", this.instrument.Name);
			}
			if (this.generatorType == GeneratorEnum.SampleID)
			{
				return string.Format("Generator SampleID {0}", this.sampleHeader);
			}
			return string.Format("Generator {0} {1}", this.generatorType, this.rawAmount);
		}
	}
}
