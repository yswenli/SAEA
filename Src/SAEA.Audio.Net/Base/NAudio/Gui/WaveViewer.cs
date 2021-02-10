using SAEA.Audio.NAudio.Wave;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SAEA.Audio.NAudio.Gui
{
    public class WaveViewer : UserControl
    {
        private Container components;

        private WaveStream waveStream;

        private int samplesPerPixel = 128;

        private long startPosition;

        private int bytesPerSample;

        public WaveStream WaveStream
        {
            get
            {
                return this.waveStream;
            }
            set
            {
                this.waveStream = value;
                if (this.waveStream != null)
                {
                    this.bytesPerSample = this.waveStream.WaveFormat.BitsPerSample / 8 * this.waveStream.WaveFormat.Channels;
                }
                base.Invalidate();
            }
        }

        public int SamplesPerPixel
        {
            get
            {
                return this.samplesPerPixel;
            }
            set
            {
                this.samplesPerPixel = value;
                base.Invalidate();
            }
        }

        public long StartPosition
        {
            get
            {
                return this.startPosition;
            }
            set
            {
                this.startPosition = value;
            }
        }

        public WaveViewer()
        {
            this.InitializeComponent();
            this.DoubleBuffered = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.components != null)
            {
                this.components.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.waveStream != null)
            {
                this.waveStream.Position = 0L;
                byte[] array = new byte[this.samplesPerPixel * this.bytesPerSample];
                this.waveStream.Position = this.startPosition + (long)(e.ClipRectangle.Left * this.bytesPerSample * this.samplesPerPixel);
                for (float num = (float)e.ClipRectangle.X; num < (float)e.ClipRectangle.Right; num += 1f)
                {
                    short num2 = 0;
                    short num3 = 0;
                    int num4 = this.waveStream.Read(array, 0, this.samplesPerPixel * this.bytesPerSample);
                    if (num4 == 0)
                    {
                        break;
                    }
                    for (int i = 0; i < num4; i += 2)
                    {
                        short num5 = BitConverter.ToInt16(array, i);
                        if (num5 < num2)
                        {
                            num2 = num5;
                        }
                        if (num5 > num3)
                        {
                            num3 = num5;
                        }
                    }
                    float num6 = ((float)num2 - -32768f) / 65535f;
                    float num7 = ((float)num3 - -32768f) / 65535f;
                    e.Graphics.DrawLine(Pens.Black, num, (float)base.Height * num6, num, (float)base.Height * num7);
                }
            }
            base.OnPaint(e);
        }

        private void InitializeComponent()
        {
            this.components = new Container();
        }
    }
}
