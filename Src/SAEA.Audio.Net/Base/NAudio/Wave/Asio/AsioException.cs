using System;

namespace SAEA.Audio.Base.NAudio.Wave.Asio
{
	internal class AsioException : Exception
	{
		private AsioError error;

		public AsioError Error
		{
			get
			{
				return this.error;
			}
			set
			{
				this.error = value;
				this.Data["ASIOError"] = this.error;
			}
		}

		public AsioException()
		{
		}

		public AsioException(string message) : base(message)
		{
		}

		public AsioException(string message, Exception innerException) : base(message, innerException)
		{
		}

		public static string getErrorName(AsioError error)
		{
			return Enum.GetName(typeof(AsioError), error);
		}
	}
}
