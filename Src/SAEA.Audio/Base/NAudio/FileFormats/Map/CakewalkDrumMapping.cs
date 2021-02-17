using System;

namespace SAEA.Audio.Base.NAudio.FileFormats.Map
{
	public class CakewalkDrumMapping
	{
		public string NoteName
		{
			get;
			set;
		}

		public int InNote
		{
			get;
			set;
		}

		public int OutNote
		{
			get;
			set;
		}

		public int OutPort
		{
			get;
			set;
		}

		public int Channel
		{
			get;
			set;
		}

		public int VelocityAdjust
		{
			get;
			set;
		}

		public float VelocityScale
		{
			get;
			set;
		}

		public override string ToString()
		{
			return string.Format("{0} In:{1} Out:{2} Ch:{3} Port:{4} Vel+:{5} Vel:{6}%", new object[]
			{
				this.NoteName,
				this.InNote,
				this.OutNote,
				this.Channel,
				this.OutPort,
				this.VelocityAdjust,
				this.VelocityScale * 100f
			});
		}
	}
}
