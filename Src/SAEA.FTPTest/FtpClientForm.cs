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

        #region private

        void Log(string operationName, string msg = "")
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

        #endregion

        #region form event

        private void FtpClientForm_Load(object sender, EventArgs e)
        {
            _loadingUserControl = new LoadingUserControl();

            _loadingUserControl.Size = this.Size;

            _loadingUserControl.Hide(this);

            this.Controls.Add(_loadingUserControl);

            this.Controls.SetChildIndex(_loadingUserControl, 9999);
        }

        private void FtpClientForm_SizeChanged(object sender, EventArgs e)
        {
            _loadingUserControl.Size = this.Size;
        }

        private void FtpClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            notifyIcon1.ShowBalloonTip(2 * 1000);
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

        #endregion      

        #region context menus

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1_TextChanged(null, null);
        }

        private void refreshToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            textBox2_TextChanged(null, null);
        }

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
                        var fileName = dr.Cells[0].Value.ToString();

                        var filePath = Path.Combine(textBox1.Text, fileName);

                        _loadingUserControl.Show(this);

                        _loadingUserControl.Message = "正在准备上传文件...";

                        Task.Run(() =>
                        {
                            try
                            {
                                long size = 0;

                                _client.Upload(filePath, (o, c) =>
                                {
                                    size = c;
                                    _loadingUserControl.Message = $"正在上传文件,{(o * 100 / c)}%";
                                });

                                var rs = _client.FileSize(fileName);

                                if (rs == size)
                                {
                                    Log("上传文件成功，fileName:" + fileName);

                                    textBox2_TextChanged(null, null);
                                }
                                else
                                {
                                    Log("上传文件失败，fileName:" + fileName, "未能完整上传文件！");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log("上传文件失败，fileName:" + fileName, ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
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

                        _loadingUserControl.Show(this);

                        _loadingUserControl.Message = "正在准备下载文件...";

                        Task.Run(() =>
                        {
                            try
                            {
                                _client.Download(fileName, Path.Combine(filePath, fileName), (o, c) =>
                                {
                                    _loadingUserControl.Message = $"正在下载文件，{(o * 100 / c)}%";
                                });

                                Log("下载文件成功，fileName:" + fileName);

                                textBox1_TextChanged(null, null);
                            }
                            catch (Exception ex)
                            {
                                Log("下载文件失败，fileName:" + fileName, ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
                            }
                        });

                    }
                }
            }
        }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除此项吗？", "SAEA.FtpClient", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                DataGridViewRow dr = null;

                var rows = dataGridView1.SelectedRows;

                var filePath = textBox1.Text;

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
                    var fileName = dr.Cells[0].Value.ToString();

                    var type = dr.Cells[2].Value.ToString();

                    if (type == "文件")
                    {
                        try
                        {
                            File.Delete(Path.Combine(textBox1.Text, fileName));
                        }
                        catch { }
                    }
                    else
                    {
                        try
                        {
                            Directory.Delete(Path.Combine(textBox1.Text, fileName));
                        }
                        catch { }
                    }
                    textBox1_TextChanged(null, null);
                }
            }
        }

        private void deleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除此项吗？", "SAEA.FtpClient", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
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
                    var fileName = dr.Cells[0].Value.ToString();

                    var type = dr.Cells[2].Value.ToString();

                    _loadingUserControl.Message = $"正在删除{type}...";

                    _loadingUserControl.Show(this);

                    if (type == "文件")
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                _client.Delete(fileName);
                            }
                            catch (Exception ex)
                            {
                                Log($"删除文件{fileName}失败", ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
                            }
                        });
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                _client.RemoveDir(fileName);
                            }
                            catch (Exception ex)
                            {
                                Log($"删除文件夹{fileName}失败", ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
                            }
                        });
                    }
                    textBox2_TextChanged(null, null);
                }
            }
        }

        #endregion

        #region controls
        private void skinButton1_Click(object sender, EventArgs e)
        {
            if (skinButton1.Text == "Connect")
            {
                _loadingUserControl.Message = "正在连接到FTPServer...";

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

                            Log($"连接到FTPServer {ip}:{port}成功");

                            splitContainer2.BeginInvoke(new Action(() =>
                            {
                                groupBox1.Enabled = true;

                                skinWaterTextBox1.Enabled = skinWaterTextBox2.Enabled
                                = skinWaterTextBox3.Enabled
                                = skinWaterTextBox4.Enabled = false;

                                skinButton1.Enabled = true;
                                skinButton1.Text = "DisConnect";

                                splitContainer2.Panel2.Enabled = true;
                                textBox2.Text = "/";
                                dataGridView2.Enabled = true;
                            }));
                            textBox2_TextChanged(null, null);

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("连接到FTPServer失败，ex:" + ex.Message);

                            Log("连接到FTPServer失败", ex.Message);

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
                    MessageBox.Show("连接到FTPServer失败，ex:" + ex.Message);
                    Log("连接到FTPServer失败", ex.Message);
                    groupBox1.Enabled = true;
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            }
            else
            {

                Task.Run(() =>
                {
                    try
                    {
                        _client.Quit();
                    }
                    catch (Exception ex)
                    {
                        Log("断开FTP失败", ex.Message);
                    }
                    finally
                    {
                        skinButton1.Invoke(new Action(() =>
                        {
                            skinWaterTextBox1.Enabled = skinWaterTextBox2.Enabled
                                = skinWaterTextBox3.Enabled
                                = skinWaterTextBox4.Enabled = true;
                            skinButton1.Enabled = true;
                            dataGridView2.Enabled = false;
                            dataGridView2.DataSource = null;
                            skinButton1.Text = "Connect";
                        }));

                    }
                });

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
            _loadingUserControl.Message = "正在获取FTPServer文件列表...";

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

        #endregion       

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
            _client?.Dispose();
            notifyIcon1.Dispose();
            Environment.Exit(-1);
        }


        #endregion


    }
}
