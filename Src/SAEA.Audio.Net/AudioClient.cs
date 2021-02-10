using SAEA.Audio.NAudio.Wave;
using SAEA.Audio.Net;
using System.Net;

namespace SAEA.Audio.GAudio
{
    public class GAudioClient
    {
        SocketClient _audioClient;

        private readonly IWavePlayer _waveOut;
        private readonly BufferedWaveProvider _waveProvider;
        private readonly WideBandSpeexCodec _speexCodec;
        private readonly WaveIn _waveIn;

        public GAudioClient(IPEndPoint endPoint)
        {
            _audioClient = new SocketClient(endPoint);
            _audioClient.OnReceive += _audioClient_OnReceive;

            _speexCodec = new WideBandSpeexCodec();
            
            _waveProvider = new BufferedWaveProvider(_speexCodec.RecordFormat);

            _waveOut = new WaveOut();
            _waveOut.Init(_waveProvider);

            _waveIn = new WaveIn();
            _waveIn.BufferMilliseconds = 50;
            _waveIn.DeviceNumber = 0;
            _waveIn.WaveFormat = _speexCodec.RecordFormat;
            _waveIn.DataAvailable += OnAudioCaptured;
        }

        public GAudioClient(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {

        }

        public void Start()
        {
            _audioClient.Connect();

            _waveOut.Play();

            _waveIn.StartRecording();
        }

        private void _audioClient_OnReceive(byte[] data)
        {
            byte[] decoded = _speexCodec.Decode(data, 0, data.Length);
            _waveProvider.AddSamples(decoded, 0, decoded.Length);
        }

        void OnAudioCaptured(object sender, WaveInEventArgs e)
        {
            byte[] encoded = _speexCodec.Encode(e.Buffer, 0, e.BytesRecorded);
            _audioClient.Send(encoded);
        }


        public void Stop()
        {
            _waveIn.StopRecording();
            _waveOut.Pause();
            _audioClient.Disconnect();
        }

        public void Dispose()
        {
            Stop();
            _waveIn.Dispose();
            _waveOut.Dispose();            
        }
    }
}
