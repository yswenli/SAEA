using System;
using System.IO;

namespace SAEA.Audio.Base.NAudio.Midi
{
	public class ControlChangeEvent : MidiEvent
	{
		private MidiController controller;

		private byte controllerValue;

		public MidiController Controller
		{
			get
			{
				return this.controller;
			}
			set
			{
				if (value < MidiController.BankSelect || value > (MidiController)127)
				{
					throw new ArgumentOutOfRangeException("value", "Controller number must be in the range 0-127");
				}
				this.controller = value;
			}
		}

		public int ControllerValue
		{
			get
			{
				return (int)this.controllerValue;
			}
			set
			{
				if (value < 0 || value > 127)
				{
					throw new ArgumentOutOfRangeException("value", "Controller Value must be in the range 0-127");
				}
				this.controllerValue = (byte)value;
			}
		}

		public ControlChangeEvent(BinaryReader br)
		{
			byte b = br.ReadByte();
			this.controllerValue = br.ReadByte();
			if ((b & 128) != 0)
			{
				throw new InvalidDataException("Invalid controller");
			}
			this.controller = (MidiController)b;
			if ((this.controllerValue & 128) != 0)
			{
				throw new InvalidDataException(string.Format("Invalid controllerValue {0} for controller {1}, Pos 0x{2:X}", this.controllerValue, this.controller, br.BaseStream.Position));
			}
		}

		public ControlChangeEvent(long absoluteTime, int channel, MidiController controller, int controllerValue) : base(absoluteTime, channel, MidiCommandCode.ControlChange)
		{
			this.Controller = controller;
			this.ControllerValue = controllerValue;
		}

		public override string ToString()
		{
			return string.Format("{0} Controller {1} Value {2}", base.ToString(), this.controller, this.controllerValue);
		}

		public override int GetAsShortMessage()
		{
			byte b = (byte)this.controller;
			return base.GetAsShortMessage() + ((int)b << 8) + ((int)this.controllerValue << 16);
		}

		public override void Export(ref long absoluteTime, BinaryWriter writer)
		{
			base.Export(ref absoluteTime, writer);
			writer.Write((byte)this.controller);
			writer.Write(this.controllerValue);
		}
	}
}
