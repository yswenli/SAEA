using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
//using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Gui
{
	//public class WaveformPainter : Control
	//{
	//	private Pen foregroundPen;

	//	private List<float> samples = new List<float>(1000);

	//	private int maxSamples;

	//	private int insertPos;

	//	private IContainer components;

	//	public WaveformPainter()
	//	{
	//		base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
	//		this.InitializeComponent();
	//		this.OnForeColorChanged(EventArgs.Empty);
	//		this.OnResize(EventArgs.Empty);
	//	}

	//	protected override void OnResize(EventArgs e)
	//	{
	//		this.maxSamples = base.Width;
	//		base.OnResize(e);
	//	}

	//	protected override void OnForeColorChanged(EventArgs e)
	//	{
	//		this.foregroundPen = new Pen(this.ForeColor);
	//		base.OnForeColorChanged(e);
	//	}

	//	public void AddMax(float maxSample)
	//	{
	//		if (this.maxSamples == 0)
	//		{
	//			return;
	//		}
	//		if (this.samples.Count <= this.maxSamples)
	//		{
	//			this.samples.Add(maxSample);
	//		}
	//		else if (this.insertPos < this.maxSamples)
	//		{
	//			this.samples[this.insertPos] = maxSample;
	//		}
	//		this.insertPos++;
	//		this.insertPos %= this.maxSamples;
	//		base.Invalidate();
	//	}

	//	protected override void OnPaint(PaintEventArgs pe)
	//	{
	//		base.OnPaint(pe);
	//		for (int i = 0; i < base.Width; i++)
	//		{
	//			float num = (float)base.Height * this.GetSample(i - base.Width + this.insertPos);
	//			float num2 = ((float)base.Height - num) / 2f;
	//			pe.Graphics.DrawLine(this.foregroundPen, (float)i, num2, (float)i, num2 + num);
	//		}
	//	}

	//	private float GetSample(int index)
	//	{
	//		if (index < 0)
	//		{
	//			index += this.maxSamples;
	//		}
	//		if (index >= 0 & index < this.samples.Count)
	//		{
	//			return this.samples[index];
	//		}
	//		return 0f;
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
