using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Gui
{
	public class VolumeSlider : UserControl
	{
		private Container components;

		private float volume = 1f;

		private float MinDb = -48f;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler VolumeChanged;

		[DefaultValue(1f)]
		public float Volume
		{
			get
			{
				return this.volume;
			}
			set
			{
				if (value < 0f)
				{
					value = 0f;
				}
				if (value > 1f)
				{
					value = 1f;
				}
				if (this.volume != value)
				{
					this.volume = value;
					if (this.VolumeChanged != null)
					{
						this.VolumeChanged(this, EventArgs.Empty);
					}
					base.Invalidate();
				}
			}
		}

		public VolumeSlider()
		{
			this.InitializeComponent();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.components != null)
			{
				this.components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			base.Name = "VolumeSlider";
			base.Size = new Size(96, 16);
		}

		protected override void OnPaint(PaintEventArgs pe)
		{
			StringFormat stringFormat = new StringFormat();
			stringFormat.LineAlignment = StringAlignment.Center;
			stringFormat.Alignment = StringAlignment.Center;
			pe.Graphics.DrawRectangle(Pens.Black, 0, 0, base.Width - 1, base.Height - 1);
			float num = 20f * (float)Math.Log10((double)this.Volume);
			float num2 = 1f - num / this.MinDb;
			pe.Graphics.FillRectangle(Brushes.LightGreen, 1, 1, (int)((float)(base.Width - 2) * num2), base.Height - 2);
			string s = string.Format("{0:F2} dB", num);
			pe.Graphics.DrawString(s, this.Font, Brushes.Black, base.ClientRectangle, stringFormat);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				this.SetVolumeFromMouse(e.X);
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.SetVolumeFromMouse(e.X);
			base.OnMouseDown(e);
		}

		private void SetVolumeFromMouse(int x)
		{
			float num = (1f - (float)x / (float)base.Width) * this.MinDb;
			if (x <= 0)
			{
				this.Volume = 0f;
				return;
			}
			this.Volume = (float)Math.Pow(10.0, (double)(num / 20f));
		}
	}
}
