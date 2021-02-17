using System;
using System.ComponentModel;
using System.Drawing;
//using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Gui
{
	//public class VolumeMeter : Control
	//{
	//	private Brush foregroundBrush;

	//	private float amplitude;

	//	private IContainer components;

	//	[DefaultValue(-3.0)]
	//	public float Amplitude
	//	{
	//		get
	//		{
	//			return this.amplitude;
	//		}
	//		set
	//		{
	//			this.amplitude = value;
	//			base.Invalidate();
	//		}
	//	}

	//	[DefaultValue(-60.0)]
	//	public float MinDb
	//	{
	//		get;
	//		set;
	//	}

	//	[DefaultValue(18.0)]
	//	public float MaxDb
	//	{
	//		get;
	//		set;
	//	}

	//	[DefaultValue(Orientation.Vertical)]
	//	public Orientation Orientation
	//	{
	//		get;
	//		set;
	//	}

	//	public VolumeMeter()
	//	{
	//		base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
	//		this.MinDb = -60f;
	//		this.MaxDb = 18f;
	//		this.Amplitude = 0f;
	//		this.Orientation = Orientation.Vertical;
	//		this.InitializeComponent();
	//		this.OnForeColorChanged(EventArgs.Empty);
	//	}

	//	protected override void OnForeColorChanged(EventArgs e)
	//	{
	//		this.foregroundBrush = new SolidBrush(this.ForeColor);
	//		base.OnForeColorChanged(e);
	//	}

	//	protected override void OnPaint(PaintEventArgs pe)
	//	{
	//		pe.Graphics.DrawRectangle(Pens.Black, 0, 0, base.Width - 1, base.Height - 1);
	//		double num = 20.0 * Math.Log10((double)this.Amplitude);
	//		if (num < (double)this.MinDb)
	//		{
	//			num = (double)this.MinDb;
	//		}
	//		if (num > (double)this.MaxDb)
	//		{
	//			num = (double)this.MaxDb;
	//		}
	//		double num2 = (num - (double)this.MinDb) / (double)(this.MaxDb - this.MinDb);
	//		int num3 = base.Width - 2;
	//		int num4 = base.Height - 2;
	//		if (this.Orientation == Orientation.Horizontal)
	//		{
	//			num3 = (int)((double)num3 * num2);
	//			pe.Graphics.FillRectangle(this.foregroundBrush, 1, 1, num3, num4);
	//			return;
	//		}
	//		num4 = (int)((double)num4 * num2);
	//		pe.Graphics.FillRectangle(this.foregroundBrush, 1, base.Height - 1 - num4, num3, num4);
	//	}

	//	protected override void Dispose(bool disposing)
	//	{
	//		if (disposing && this.components != null)
	//		{
	//			this.components.Dispose();
	//		}
	//		base.Dispose(disposing);
	//	}

	//	private void InitializeComponent()
	//	{
	//		this.components = new Container();
	//	}
	//}
}
