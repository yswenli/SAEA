using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Gui
{
	public class Pot : UserControl
	{
		private double minimum;

		private double maximum = 1.0;

		private double value = 0.5;

		private int beginDragY;

		private double beginDragValue;

		private bool dragging;

		private IContainer components;

		[method: CompilerGenerated]
		[CompilerGenerated]
		public event EventHandler ValueChanged;

		public double Minimum
		{
			get
			{
				return this.minimum;
			}
			set
			{
				if (value >= this.maximum)
				{
					throw new ArgumentOutOfRangeException("Minimum must be less than maximum");
				}
				this.minimum = value;
				if (this.Value < this.minimum)
				{
					this.Value = this.minimum;
				}
			}
		}

		public double Maximum
		{
			get
			{
				return this.maximum;
			}
			set
			{
				if (value <= this.minimum)
				{
					throw new ArgumentOutOfRangeException("Maximum must be greater than minimum");
				}
				this.maximum = value;
				if (this.Value > this.maximum)
				{
					this.Value = this.maximum;
				}
			}
		}

		public double Value
		{
			get
			{
				return this.value;
			}
			set
			{
				this.SetValue(value, false);
			}
		}

		public Pot()
		{
			base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
			this.InitializeComponent();
		}

		private void SetValue(double newValue, bool raiseEvents)
		{
			if (this.value != newValue)
			{
				this.value = newValue;
				if (raiseEvents && this.ValueChanged != null)
				{
					this.ValueChanged(this, EventArgs.Empty);
				}
				base.Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			int num = Math.Min(base.Width - 4, base.Height - 4);
			Pen pen = new Pen(this.ForeColor, 3f);
			pen.LineJoin = LineJoin.Round;
			GraphicsState gstate = e.Graphics.Save();
			e.Graphics.TranslateTransform((float)(base.Width / 2), (float)(base.Height / 2));
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.DrawArc(pen, new Rectangle(num / -2, num / -2, num, num), 135f, 270f);
			double num2 = (this.value - this.minimum) / (this.maximum - this.minimum);
			double num3 = 135.0 + num2 * 270.0;
			double num4 = (double)num / 2.0 * Math.Cos(3.1415926535897931 * num3 / 180.0);
			double num5 = (double)num / 2.0 * Math.Sin(3.1415926535897931 * num3 / 180.0);
			e.Graphics.DrawLine(pen, 0f, 0f, (float)num4, (float)num5);
			e.Graphics.Restore(gstate);
			base.OnPaint(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.dragging = true;
			this.beginDragY = e.Y;
			this.beginDragValue = this.value;
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			this.dragging = false;
			base.OnMouseUp(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (this.dragging)
			{
				int num = this.beginDragY - e.Y;
				double num2 = (this.maximum - this.minimum) * ((double)num / 150.0);
				double num3 = this.beginDragValue + num2;
				if (num3 < this.minimum)
				{
					num3 = this.minimum;
				}
				if (num3 > this.maximum)
				{
					num3 = this.maximum;
				}
				this.SetValue(num3, true);
			}
			base.OnMouseMove(e);
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
			base.SuspendLayout();
			base.AutoScaleDimensions = new SizeF(6f, 13f);
			base.AutoScaleMode = AutoScaleMode.Font;
			base.Name = "Pot";
			base.Size = new Size(32, 32);
			base.ResumeLayout(false);
		}
	}
}
