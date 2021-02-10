using System.Text.RegularExpressions;

namespace SAEA.Audio.NAudio.Wave
{
    public class Cue
	{
		public int Position
		{
            get;private set;
		}

		public string Label
		{
            get; private set;
        }

		public Cue(int position, string label)
		{
			this.Position = position;
			if (label == null)
			{
				label = "";
			}
			this.Label = Regex.Replace(label, "[^\\u0000-\\u00FF]", "");
		}
	}
}
