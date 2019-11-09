using CCWin;
using SAEA.Common;
using SAEA.FTP;
using SAEA.FTPTest.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class FtpClientForm : Skin_Mac
    {
        FTPClient _client = null;

        LoadingUserControl _loadingUserControl;

        public FtpClientForm()
        {
            InitializeComponent();

            textBox1_TextChanged(null, null);
        }

        private void FtpClientForm_Load(object sender, EventArgs e)
        {
            _loadingUserControl = new LoadingUserControl();

            _loadingUserControl.Size = this.Size;

            _loadingUserControl.Hide(this);

            this.Controls.Add(_loadingUserControl);

            this.Controls.SetChildIndex(_loadingUserControl, 9999);
        }

        private void skinButton1_Click(object sender, EventArgs e)
        {
            _loadingUserControl.Show(this);

            groupBox1.Enabled = false;

            splitContainer2.Panel2.Enabled = false;

            try
            {
                var ip = skinWaterTextBox1.Text;
                var port = int.Parse(skinWaterTextBox2.Text);
                var username = skinWaterTextBox3.Text;
                var pwd = skinWaterTextBox4.Text;

                _client = new FTPClient(ip, port, username, pwd);

                Task.Run(() =>
                {
                    try
                    {
                        _client.Connect();

                        Log("连接到FTP成功");

                        splitContainer2.BeginInvoke(new Action(() =>
                        {
                            splitContainer2.Panel2.Enabled = true;
                            textBox2.Text = "/";
                            textBox2_TextChanged(null, null);
                        }));

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("连接到FTP失败，ex:" + ex.Message);

                        Log("连接到FTP失败", ex.Message);

                        this.BeginInvoke(new Action(() =>
                        {
                            groupBox1.Enabled = true;
                        }));

                    }
                    finally
                    {
                        _loadingUserControl.Hide(this);
                    }
                });
            }
            catch (Exception ex)
            {
                _loadingUserControl.Hide(this);
                MessageBox.Show("连接到FTP失败，ex:" + ex.Message);
                Log("连接到FTP失败", ex.Message);
                groupBox1.Enabled = true;
            }
            finally
            {
                _loadingUserControl.Hide(this);
            }
        }

        public void Log(string operationName, string msg = "")
        {
            var action = new Action(() =>
            {
                logTxt.Text = $"{DateTime.Now.ToString("HH:mm:ss.fff")}\t{operationName} \t{msg}{Environment.NewLine}{logTxt.Text}";
                logTxt.Select(0, 1);
                logTxt.ScrollToCaret();
            });

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(action));
            }
            else
            {
                action.Invoke();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var filePath = textBox1.Text;

            List<ListInfo> listInfos = new List<ListInfo>();

            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var dirs = PathHelper.GetDirectories(filePath, out List<FileInfo> files);

                    if (dirs != null)
                    {
                        foreach (var item in dirs)
                        {
                            listInfos.Add(new ListInfo()
                            {
                                FileName = item.Name,
                                Type = "文件夹"
                            });
                        }
                    }

                    if (files != null)
                    {
                        foreach (var item in files)
                        {
                            listInfos.Add(new ListInfo()
                            {
                                FileName = item.Name,
                                Type = "文件",
                                Size = item.Length
                            });
                        }
                    }

                    listInfos = listInfos.OrderBy(b => b.FileName).ToList();
                }
                catch (Exception ex)
                {
                    Log("访问本地目录：" + filePath, ex.Message);
                }
            }

            var action = new Action(() =>
            {
                dataGridView1.DataSource = null;
                dataGridView1.DataSource = listInfos;
            });

            if (dataGridView1.InvokeRequired)
            {
                dataGridView1.Invoke(action);
            }
            else
            {
                action?.Invoke();
            }

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            _loadingUserControl.Show(this);

            Task.Run(() =>
            {
                try
                {
                    var filePath = string.Empty;

                    textBox2.Invoke(new Action(() =>
                    {
                        filePath = textBox2.Text;
                    }));


                    var list = _client.Dir(filePath, FTP.Model.DirType.MLSD);

                    if (list != null && list.Any())
                    {
                        List<ListInfo> listInfos = new List<ListInfo>();

                        foreach (var item in list)
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                var arr = item.Split(";", StringSplitOptions.RemoveEmptyEntries);

                                if (arr.Length >= 3)
                                {
                                    var fileName = arr[2].Trim();

                                    var type = (arr[0] == "type=dir" ? "文件夹" : "文件");

                                    var size = 0L;

                                    if (type == "文件")
                                    {
                                        fileName = arr[3].Trim();
                                        size = _client.FileSize(fileName);
                                    }

                                    listInfos.Add(new ListInfo()
                                    {
                                        FileName = fileName,
                                        Type = type,
                                        Size = size
                                    });
                                }
                            }
                        }

                        listInfos = listInfos.OrderBy(b => b.FileName).ToList();

                        dataGridView2.BeginInvoke(new Action(() =>
                        {
                            dataGridView2.DataSource = null;
                            dataGridView2.DataSource = listInfos;
                        }));
                    }
                    else
                    {
                        dataGridView2.BeginInvoke(new Action(() =>
                        {
                            dataGridView2.DataSource = null;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Log("初始化ftpserver列表失败", ex.Message);
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            });
        }

        #region context menus
        private void parentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dir = PathHelper.GetPreDir(textBox1.Text);

            if (dir != null)
            {
                textBox1.Text = dir.FullName;
            }
        }

        private void parentToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    if (_client.ChangeToParentDir())
                    {
                        var cp = _client.CurrentDir();

                        textBox2.Invoke(new Action(() =>
                        {
                            textBox2.Text = cp;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Log("获取ftpserver列表失败", ex.Message);
                }
            });

        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {


            DataGridViewRow dr = null;

            var rows = dataGridView1.SelectedRows;

            if (rows != null && rows.Count > 0)
            {
                dr = rows[0];
            }
            else
            {
                var cells = dataGridView1.SelectedCells;

                if (cells != null && cells.Count > 0)
                {
                    dr = dataGridView1.Rows[cells[0].RowIndex];
                }
            }
            if (dr != null)
            {
                if (_client.Connected)
                {
                    if (dr.Cells[2].Value.ToString() == "文件")
                    {
                        var filePath = Path.Combine(textBox1.Text, dr.Cells[0].Value.ToString());

                        Task.Run(() =>
                        {
                            try
                            {
                                _client.Upload(filePath);

                                Log("上传文件成功");

                                textBox2_TextChanged(null, null);
                            }
                            catch (Exception ex)
                            {
                                Log("上传文件失败！", ex.Message);
                            }
                        });

                    }
                }
            }
        }

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataGridViewRow dr = null;

            var rows = dataGridView2.SelectedRows;

            var filePath = textBox1.Text;

            if (rows != null && rows.Count > 0)
            {
                dr = rows[0];
            }
            else
            {
                var cells = dataGridView2.SelectedCells;

                if (cells != null && cells.Count > 0)
                {
                    dr = dataGridView2.Rows[cells[0].RowIndex];
                }
            }
            if (dr != null)
            {
                if (_client.Connected)
                {
                    if (dr.Cells[2].Value.ToString() == "文件")
                    {
                        var fileName = dr.Cells[0].Value.ToString();

                        Task.Run(() =>
                        {
                            try
                            {
                                _client.Download(fileName, Path.Combine(filePath, fileName));

                                Log("下载文件成功");

                                textBox1_TextChanged(null, null);
                            }
                            catch (Exception ex)
                            {
                                Log("下载文件失败！", ex.Message);
                            }
                        });

                    }
                }
            }
        }

        #endregion


        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if (dataGridView1.Rows[e.RowIndex].Cells[2].Value.ToString() == "文件夹")
                {
                    var fileName = dataGridView1.Rows[e.RowIndex].Cells[0].Value.ToString();

                    textBox1.Text = Path.Combine(textBox1.Text, fileName);
                }
            }
            else
            {
                var dir = PathHelper.GetPreDir(textBox1.Text);

                if (dir != null)
                {
                    textBox1.Text = dir.FullName;
                }
            }
        }

        private void dataGridView2_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if (dataGridView2.Rows[e.RowIndex].Cells[2].Value.ToString() == "文件夹")
                {
                    var path = textBox2.Text;

                    var fileName = dataGridView2.Rows[e.RowIndex].Cells[0].Value.ToString().Trim();

                    Task.Run(() =>
                    {
                        try
                        {
                            if (_client.ChangeDir(fileName))
                            {
                                var cp = _client.CurrentDir();

                                textBox2.Invoke(new Action(() =>
                                {
                                    textBox2.Text = cp;
                                }));
                            }
                            else
                            {
                                textBox2_TextChanged(null, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("获取ftpserver列表失败", ex.Message);
                        }
                    });
                }
            }
            else
            {
                Task.Run(() =>
                {
                    try
                    {
                        if (_client.ChangeToParentDir())
                        {
                            var cp = _client.CurrentDir();

                            textBox2.Invoke(new Action(() =>
                            {
                                textBox2.Text = cp;
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("获取ftpserver列表失败", ex.Message);
                    }
                });
            }
        }

        private void FtpClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void FtpClientForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                if (_client != null && _client.Connected)
                {
                    _client.Quit();
                }
            }
            catch { }
        }

        #region notify
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private void showUIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (_client != null && _client.Connected)
                {
                    _client.Quit();
                }
            }
            catch { }
            Environment.Exit(-1);
        }
        #endregion


    }
}
