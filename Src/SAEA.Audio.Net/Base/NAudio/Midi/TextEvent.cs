using SAEA.Audio.NAudio.Utils;
using System.IO;
using System.Text;

namespace SAEA.Audio.NAudio.Midi
{
    public class TextEvent : MetaEvent
    {
        private string text;

        public string Text
        {
            get
            {
                return this.text;
            }
            set
            {
                this.text = value;
                this.metaDataLength = this.text.Length;
            }
        }

        public TextEvent(BinaryReader br, int length)
        {
            Encoding instance = ByteEncoding.Instance;
            this.text = instance.GetString(br.ReadBytes(length));
        }

        public TextEvent(string text, MetaEventType metaEventType, long absoluteTime) : base(metaEventType, text.Length, absoluteTime)
        {
            this.text = text;
        }

        public override MidiEvent Clone()
        {
            return (TextEvent)base.MemberwiseClone();
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", base.ToString(), this.text);
        }

        public override void Export(ref long absoluteTime, BinaryWriter writer)
        {
            base.Export(ref absoluteTime, writer);
            byte[] bytes = ByteEncoding.Instance.GetBytes(this.text);
            writer.Write(bytes);
        }
    }
}
