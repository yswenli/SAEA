using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SAEA.Audio.NAudio.Gui
{
	public class PanSlider : UserControl
	{
		private Container components;

		private float pan;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler PanChanged;

		public float Pan
		{
			get
			{
				return this.pan;
			}
			set
			{
				if (value < -1f)
				{
					value = -1f;
				}
				if (value > 1f)
				{
					value = 1f;
				}
				if (value != this.pan)
				{
					this.pan = value;
					if (this.PanChanged != null)
					{
						this.PanChanged(this, EventArgs.Empty);
					}
					base.Invalidate();
				}
			}
		}

		public PanSlider()
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
			base.Name = "PanSlider";
			base.Size = new Size(104, 16);
		}

		protected override void OnPaint(PaintEventArgs pe)
		{
			StringFormat stringFormat = new StringFormat();
			stringFormat.LineAlignment = StringAlignment.Center;
			stringFormat.Alignment = StringAlignment.Center;
			string s;
			if ((double)this.pan == 0.0)
			{
				pe.Graphics.FillRectangle(Brushes.Orange, base.Width / 2 - 1, 1, 3, base.Height - 2);
				s = "C";
			}
			else if (this.pan > 0f)
			{
				pe.Graphics.FillRectangle(Brushes.Orange, base.Width / 2, 1, (int)((float)(base.Width / 2) * this.pan), base.Height - 2);
				s = string.Format("{0:F0}%R", this.pan * 100f);
			}
			else
			{
				pe.Graphics.FillRectangle(Brushes.Orange, (int)((float)(base.Width / 2) * (this.pan + 1f)), 1, (int)((float)(base.Width / 2) * (0f - this.pan)), base.Height - 2);
				s = string.Format("{0:F0}%L", this.pan * -100f);
			}
			pe.Graphics.DrawRectangle(Pens.Black, 0, 0, base.Width - 1, base.Height - 1);
			pe.Graphics.DrawString(s, this.Font, Brushes.Black, base.ClientRectangle, stringFormat);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				this.SetPanFromMouse(e.X);
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.SetPanFromMouse(e.X);
			base.OnMouseDown(e);
		}

		private void SetPanFromMouse(int x)
		{
			this.Pan = (float)x / (float)base.Width * 2f - 1f;
		}
	}
}
