using SAEA.Audio.NAudio.Wave;

namespace SAEA.Audio.GAudio
{
    class Gsm610ChatCodec : AcmChatCodec
    {
        public Gsm610ChatCodec()
            : base(new WaveFormat(8000, 16, 1), new Gsm610WaveFormat())
        {
        }

        public override string Name => "GSM 6.10";
    }
}
