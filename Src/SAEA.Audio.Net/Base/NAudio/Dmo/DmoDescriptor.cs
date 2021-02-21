using System;

namespace SAEA.Audio.Base.NAudio.Dmo
{
	public class DmoDescriptor
	{
		public string Name
		{
			get;
			private set;
		}

		public Guid Clsid
		{
			get;
			private set;
		}

		public DmoDescriptor(string name, Guid clsid)
		{
			this.Name = name;
			this.Clsid = clsid;
		}
	}
}
