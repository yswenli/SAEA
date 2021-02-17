using System;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	internal struct DmoPartialMediaType
	{
		private Guid type;

		private Guid subtype;

		public Guid Type
		{
			get
			{
				return this.type;
			}
			internal set
			{
				this.type = value;
			}
		}

		public Guid Subtype
		{
			get
			{
				return this.subtype;
			}
			internal set
			{
				this.subtype = value;
			}
		}
	}
}
