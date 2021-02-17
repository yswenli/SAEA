using System;
using System.ComponentModel;
using System.Drawing;
//using System.Windows.Forms;

namespace SAEA.Audio.Base.NAudio.Utils
{
	//public class ProgressLog : UserControl
	//{
	//	private delegate void LogMessageDelegate(Color color, string message);

	//	private delegate void ClearLogDelegate();

	//	private IContainer components;

	//	private RichTextBox richTextBoxLog;

	//	public new string Text
	//	{
	//		get
	//		{
	//			return this.richTextBoxLog.Text;
	//		}
	//	}

	//	public ProgressLog()
	//	{
	//		this.InitializeComponent();
	//	}

	//	public void LogMessage(Color color, string message)
	//	{
	//		if (this.richTextBoxLog.InvokeRequired)
	//		{
	//			base.Invoke(new ProgressLog.LogMessageDelegate(this.LogMessage), new object[]
	//			{
	//				color,
	//				message
	//			});
	//			return;
	//		}
	//		this.richTextBoxLog.SelectionStart = this.richTextBoxLog.TextLength;
	//		this.richTextBoxLog.SelectionColor = color;
	//		this.richTextBoxLog.AppendText(message);
	//		this.richTextBoxLog.AppendText(Environment.NewLine);
	//	}

	//	public void ClearLog()
	//	{
	//		if (this.richTextBoxLog.InvokeRequired)
	//		{
	//			base.Invoke(new ProgressLog.ClearLogDelegate(this.ClearLog), new object[0]);
	//			return;
	//		}
	//		this.richTextBoxLog.Clear();
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
	//		this.richTextBoxLog = new RichTextBox();
	//		base.SuspendLayout();
	//		this.richTextBoxLog.BorderStyle = BorderStyle.None;
	//		this.richTextBoxLog.Dock = DockStyle.Fill;
	//		this.richTextBoxLog.Location = new Point(1, 1);
	//		this.richTextBoxLog.Name = "richTextBoxLog";
	//		this.richTextBoxLog.ReadOnly = true;
	//		this.richTextBoxLog.Size = new Size(311, 129);
	//		this.richTextBoxLog.TabIndex = 0;
	//		this.richTextBoxLog.Text = "";
	//		base.AutoScaleDimensions = new SizeF(6f, 13f);
	//		base.AutoScaleMode = AutoScaleMode.Font;
	//		this.BackColor = SystemColors.ControlDarkDark;
	//		base.Controls.Add(this.richTextBoxLog);
	//		base.Name = "ProgressLog";
	//		base.Padding = new Padding(1);
	//		base.Size = new Size(313, 131);
	//		base.ResumeLayout(false);
	//	}
	//}
}
