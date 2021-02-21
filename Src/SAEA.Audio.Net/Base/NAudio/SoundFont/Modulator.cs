using System;

namespace SAEA.Audio.Base.NAudio.SoundFont
{
	public class Modulator
	{
		private ModulatorType sourceModulationData;

		private GeneratorEnum destinationGenerator;

		private short amount;

		private ModulatorType sourceModulationAmount;

		private TransformEnum sourceTransform;

		public ModulatorType SourceModulationData
		{
			get
			{
				return this.sourceModulationData;
			}
			set
			{
				this.sourceModulationData = value;
			}
		}

		public GeneratorEnum DestinationGenerator
		{
			get
			{
				return this.destinationGenerator;
			}
			set
			{
				this.destinationGenerator = value;
			}
		}

		public short Amount
		{
			get
			{
				return this.amount;
			}
			set
			{
				this.amount = value;
			}
		}

		public ModulatorType SourceModulationAmount
		{
			get
			{
				return this.sourceModulationAmount;
			}
			set
			{
				this.sourceModulationAmount = value;
			}
		}

		public TransformEnum SourceTransform
		{
			get
			{
				return this.sourceTransform;
			}
			set
			{
				this.sourceTransform = value;
			}
		}

		public override string ToString()
		{
			return string.Format("Modulator {0} {1} {2} {3} {4}", new object[]
			{
				this.sourceModulationData,
				this.destinationGenerator,
				this.amount,
				this.sourceModulationAmount,
				this.sourceTransform
			});
		}
	}
}
