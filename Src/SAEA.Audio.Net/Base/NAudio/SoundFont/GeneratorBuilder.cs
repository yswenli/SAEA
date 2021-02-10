using System;
using System.IO;

namespace SAEA.Audio.NAudio.SoundFont
{
	internal class GeneratorBuilder : StructureBuilder<Generator>
	{
		public override int Length
		{
			get
			{
				return 4;
			}
		}

		public Generator[] Generators
		{
			get
			{
				return this.data.ToArray();
			}
		}

		public override Generator Read(BinaryReader br)
		{
			Generator generator = new Generator();
			generator.GeneratorType = (GeneratorEnum)br.ReadUInt16();
			generator.UInt16Amount = br.ReadUInt16();
			this.data.Add(generator);
			return generator;
		}

		public override void Write(BinaryWriter bw, Generator o)
		{
		}

		public void Load(Instrument[] instruments)
		{
			Generator[] generators = this.Generators;
			for (int i = 0; i < generators.Length; i++)
			{
				Generator generator = generators[i];
				if (generator.GeneratorType == GeneratorEnum.Instrument)
				{
					generator.Instrument = instruments[(int)generator.UInt16Amount];
				}
			}
		}

		public void Load(SampleHeader[] sampleHeaders)
		{
			Generator[] generators = this.Generators;
			for (int i = 0; i < generators.Length; i++)
			{
				Generator generator = generators[i];
				if (generator.GeneratorType == GeneratorEnum.SampleID)
				{
					generator.SampleHeader = sampleHeaders[(int)generator.UInt16Amount];
				}
			}
		}
	}
}
