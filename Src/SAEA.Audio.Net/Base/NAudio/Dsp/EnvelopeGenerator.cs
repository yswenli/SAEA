using System;

namespace SAEA.Audio.NAudio.Dsp
{
	public class EnvelopeGenerator
	{
		public enum EnvelopeState
		{
			Idle,
			Attack,
			Decay,
			Sustain,
			Release
		}

		private EnvelopeGenerator.EnvelopeState state;

		private float output;

		private float attackRate;

		private float decayRate;

		private float releaseRate;

		private float attackCoef;

		private float decayCoef;

		private float releaseCoef;

		private float sustainLevel;

		private float targetRatioAttack;

		private float targetRatioDecayRelease;

		private float attackBase;

		private float decayBase;

		private float releaseBase;

		public float AttackRate
		{
			get
			{
				return this.attackRate;
			}
			set
			{
				this.attackRate = value;
				this.attackCoef = EnvelopeGenerator.CalcCoef(value, this.targetRatioAttack);
				this.attackBase = (1f + this.targetRatioAttack) * (1f - this.attackCoef);
			}
		}

		public float DecayRate
		{
			get
			{
				return this.decayRate;
			}
			set
			{
				this.decayRate = value;
				this.decayCoef = EnvelopeGenerator.CalcCoef(value, this.targetRatioDecayRelease);
				this.decayBase = (this.sustainLevel - this.targetRatioDecayRelease) * (1f - this.decayCoef);
			}
		}

		public float ReleaseRate
		{
			get
			{
				return this.releaseRate;
			}
			set
			{
				this.releaseRate = value;
				this.releaseCoef = EnvelopeGenerator.CalcCoef(value, this.targetRatioDecayRelease);
				this.releaseBase = -this.targetRatioDecayRelease * (1f - this.releaseCoef);
			}
		}

		public float SustainLevel
		{
			get
			{
				return this.sustainLevel;
			}
			set
			{
				this.sustainLevel = value;
				this.decayBase = (this.sustainLevel - this.targetRatioDecayRelease) * (1f - this.decayCoef);
			}
		}

		public EnvelopeGenerator.EnvelopeState State
		{
			get
			{
				return this.state;
			}
		}

		public EnvelopeGenerator()
		{
			this.Reset();
			this.AttackRate = 0f;
			this.DecayRate = 0f;
			this.ReleaseRate = 0f;
			this.SustainLevel = 1f;
			this.SetTargetRatioAttack(0.3f);
			this.SetTargetRatioDecayRelease(0.0001f);
		}

		private static float CalcCoef(float rate, float targetRatio)
		{
			return (float)Math.Exp(-Math.Log((double)((1f + targetRatio) / targetRatio)) / (double)rate);
		}

		private void SetTargetRatioAttack(float targetRatio)
		{
			if (targetRatio < 1E-09f)
			{
				targetRatio = 1E-09f;
			}
			this.targetRatioAttack = targetRatio;
			this.attackBase = (1f + this.targetRatioAttack) * (1f - this.attackCoef);
		}

		private void SetTargetRatioDecayRelease(float targetRatio)
		{
			if (targetRatio < 1E-09f)
			{
				targetRatio = 1E-09f;
			}
			this.targetRatioDecayRelease = targetRatio;
			this.decayBase = (this.sustainLevel - this.targetRatioDecayRelease) * (1f - this.decayCoef);
			this.releaseBase = -this.targetRatioDecayRelease * (1f - this.releaseCoef);
		}

		public float Process()
		{
			switch (this.state)
			{
			case EnvelopeGenerator.EnvelopeState.Attack:
				this.output = this.attackBase + this.output * this.attackCoef;
				if (this.output >= 1f)
				{
					this.output = 1f;
					this.state = EnvelopeGenerator.EnvelopeState.Decay;
				}
				break;
			case EnvelopeGenerator.EnvelopeState.Decay:
				this.output = this.decayBase + this.output * this.decayCoef;
				if (this.output <= this.sustainLevel)
				{
					this.output = this.sustainLevel;
					this.state = EnvelopeGenerator.EnvelopeState.Sustain;
				}
				break;
			case EnvelopeGenerator.EnvelopeState.Release:
				this.output = this.releaseBase + this.output * this.releaseCoef;
				if ((double)this.output <= 0.0)
				{
					this.output = 0f;
					this.state = EnvelopeGenerator.EnvelopeState.Idle;
				}
				break;
			}
			return this.output;
		}

		public void Gate(bool gate)
		{
			if (gate)
			{
				this.state = EnvelopeGenerator.EnvelopeState.Attack;
				return;
			}
			if (this.state != EnvelopeGenerator.EnvelopeState.Idle)
			{
				this.state = EnvelopeGenerator.EnvelopeState.Release;
			}
		}

		public void Reset()
		{
			this.state = EnvelopeGenerator.EnvelopeState.Idle;
			this.output = 0f;
		}

		public float GetOutput()
		{
			return this.output;
		}
	}
}
