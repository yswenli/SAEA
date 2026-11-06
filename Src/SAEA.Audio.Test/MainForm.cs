using System;
using System.Windows.Forms;

using SAEA.Audio.Storage;

namespace SAEA.Audio.Test
{
    /// <summary>
    /// 音频聊天演示窗体：按钮分别启动内嵌音频服务器与两个加入同一频道的客户端
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// 初始化窗体控件
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        #region server

        static AudioServer<VStorage> _audioServer;

        static void ServerInit()
        {
            _audioServer = new AudioServer<VStorage>(38083);            
            _audioServer.Start();
        }

        #endregion

        #region client

        static AudioClient _audioClient1;

        static void ClientInit1()
        {
            _audioClient1 = new AudioClient("127.0.0.1", 38083);
            _audioClient1.Start();
            _audioClient1.Join("audiochat");
        }

        static AudioClient _audioClient2;

        static void ClientInit2()
        {
            _audioClient2 = new AudioClient("127.0.0.1", 38083);
            _audioClient2.Start();
            _audioClient2.Join("audiochat");

        }
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                button1.Enabled = false;
                ServerInit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("服务器启动异常，Error:" + ex.Message);
                button1.Enabled = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                button2.Enabled = false;
                ClientInit1();
            }
            catch (Exception ex)
            {
                MessageBox.Show("客户端一启动异常，Error:" + ex.Message);
                button2.Enabled = true;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                button3.Enabled = false;
                ClientInit2();
            }
            catch (Exception ex)
            {
                MessageBox.Show("客户端二启动异常，Error:" + ex.Message);
                button3.Enabled = true;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;

            try
            {
                _audioServer.Stop();
            }
            catch(Exception ex)
            {
                MessageBox.Show("服务器关闭异常，Error:" + ex.Message);
            }

            try
            {
                _audioClient1.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show("客户端一关闭异常，Error:" + ex.Message);
            }

            try
            {
                _audioClient2.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show("客户端二关闭异常，Error:" + ex.Message);
            }
        }
    }
}
