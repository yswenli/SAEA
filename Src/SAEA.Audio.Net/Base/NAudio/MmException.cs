using System;

namespace SAEA.Audio.NAudio
{
	public class MmException : Exception
	{
		private MmResult result;

		private string function;

		public MmResult Result
		{
			get
			{
				return this.result;
			}
		}

		public MmException(MmResult result, string function) : base(MmException.ErrorMessage(result, function))
		{
			this.result = result;
			this.function = function;
		}

		private static string ErrorMessage(MmResult result, string function)
		{
			return string.Format("{0} calling {1}", result, function);
		}

		public static void Try(MmResult result, string function)
		{
			if (result != MmResult.NoError)
			{
				throw new MmException(result, function);
			}
		}
	}
}
