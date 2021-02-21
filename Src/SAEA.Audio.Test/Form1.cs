using SAEA.Audio.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.Audio.Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            ServerInit();

            ClientInit();
        }

        #region server

        static AudioServer<VStorage> _audioServer;

        static void ServerInit()
        {
            _audioServer = new AudioServer<VStorage>(39656);

            _audioServer.Start();
        }

        #endregion

        #region client

        static void ClientInit()
        {
            AudioClient audioClient1 = new AudioClient("127.0.0.1", 39656);
            audioClient1.Start();
            audioClient1.Join("audiochat");

            AudioClient audioClient2 = new AudioClient("127.0.0.1", 39656);
            audioClient2.Start();
            audioClient2.Join("audiochat");
        }

        #endregion
    }
}
