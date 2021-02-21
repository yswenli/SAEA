using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Gui
{
	public class Fader : Control
	{
		private int minimum;

		private int maximum;

		private float percent;

		private Orientation orientation;

		private Container components;

		private readonly int SliderHeight = 30;

		private readonly int SliderWidth = 15;

		private Rectangle sliderRectangle;

		private bool dragging;

		private int dragY;

		public int Minimum
		{
			get
			{
				return this.minimum;
			}
			set
			{
				this.minimum = value;
			}
		}

		public int Maximum
		{
			get
			{
				return this.maximum;
			}
			set
			{
				this.maximum = value;
			}
		}

		public int Value
		{
			get
			{
				return (int)(this.percent * (float)(this.maximum - this.minimum)) + this.minimum;
			}
			set
			{
				this.percent = (float)(value - this.minimum) / (float)(this.maximum - this.minimum);
			}
		}

		public Orientation Orientation
		{
			get
			{
				return this.orientation;
			}
			set
			{
				this.orientation = value;
			}
		}

		public Fader()
		{
			this.InitializeComponent();
			base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.components != null)
			{
				this.components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void DrawSlider(Graphics g)
		{
			Brush brush = new SolidBrush(Color.White);
			Pen pen = new Pen(Color.Black);
			this.sliderRectangle.X = (base.Width - this.SliderWidth) / 2;
			this.sliderRectangle.Width = this.SliderWidth;
			this.sliderRectangle.Y = (int)((float)(base.Height - this.SliderHeight) * this.percent);
			this.sliderRectangle.Height = this.SliderHeight;
			g.FillRectangle(brush, this.sliderRectangle);
			g.DrawLine(pen, this.sliderRectangle.Left, this.sliderRectangle.Top + this.sliderRectangle.Height / 2, this.sliderRectangle.Right, this.sliderRectangle.Top + this.sliderRectangle.Height / 2);
			brush.Dispose();
			pen.Dispose();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			if (this.Orientation == Orientation.Vertical)
			{
				Brush brush = new SolidBrush(Color.Black);
				graphics.FillRectangle(brush, base.Width / 2, this.SliderHeight / 2, 2, base.Height - this.SliderHeight);
				brush.Dispose();
				this.DrawSlider(graphics);
			}
			base.OnPaint(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (this.sliderRectangle.Contains(e.X, e.Y))
			{
				this.dragging = true;
				this.dragY = e.Y - this.sliderRectangle.Y;
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (this.dragging)
			{
				int num = e.Y - this.dragY;
				if (num < 0)
				{
					this.percent = 0f;
				}
				else if (num > base.Height - this.SliderHeight)
				{
					this.percent = 1f;
				}
				else
				{
					this.percent = (float)num / (float)(base.Height - this.SliderHeight);
				}
				base.Invalidate();
			}
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			this.dragging = false;
			base.OnMouseUp(e);
		}

		private void InitializeComponent()
		{
			this.components = new Container();
		}
	}
}
