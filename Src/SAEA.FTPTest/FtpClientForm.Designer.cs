namespace SAEA.FTPTest
{
    partial class FtpClientForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FtpClientForm));
            this.skinButton1 = new CCWin.SkinControl.SkinButton();
            this.skinWaterTextBox4 = new CCWin.SkinControl.SkinWaterTextBox();
            this.skinWaterTextBox3 = new CCWin.SkinControl.SkinWaterTextBox();
            this.skinWaterTextBox2 = new CCWin.SkinControl.SkinWaterTextBox();
            this.skinWaterTextBox1 = new CCWin.SkinControl.SkinWaterTextBox();
            this.logTxt = new CCWin.SkinControl.SkinWaterTextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.parentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.dataGridView2 = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.parentToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.downloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip3 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.showUIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).BeginInit();
            this.contextMenuStrip2.SuspendLayout();
            this.contextMenuStrip3.SuspendLayout();
            this.SuspendLayout();
            // 
            // skinButton1
            // 
            this.skinButton1.BackColor = System.Drawing.Color.Transparent;
            this.skinButton1.ControlState = CCWin.SkinClass.ControlState.Normal;
            this.skinButton1.DownBack = null;
            this.skinButton1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinButton1.ForeColor = System.Drawing.Color.White;
            this.skinButton1.Location = new System.Drawing.Point(494, 22);
            this.skinButton1.MouseBack = null;
            this.skinButton1.MouseBaseColor = System.Drawing.SystemColors.MenuHighlight;
            this.skinButton1.Name = "skinButton1";
            this.skinButton1.NormlBack = null;
            this.skinButton1.Size = new System.Drawing.Size(90, 26);
            this.skinButton1.TabIndex = 9;
            this.skinButton1.Text = "Connect";
            this.skinButton1.UseVisualStyleBackColor = false;
            this.skinButton1.Click += new System.EventHandler(this.skinButton1_Click);
            // 
            // skinWaterTextBox4
            // 
            this.skinWaterTextBox4.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinWaterTextBox4.Location = new System.Drawing.Point(350, 22);
            this.skinWaterTextBox4.Name = "skinWaterTextBox4";
            this.skinWaterTextBox4.PasswordChar = '*';
            this.skinWaterTextBox4.Size = new System.Drawing.Size(125, 26);
            this.skinWaterTextBox4.TabIndex = 8;
            this.skinWaterTextBox4.Text = "12321";
            this.skinWaterTextBox4.WaterColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.skinWaterTextBox4.WaterText = "Password";
            // 
            // skinWaterTextBox3
            // 
            this.skinWaterTextBox3.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinWaterTextBox3.Location = new System.Drawing.Point(221, 22);
            this.skinWaterTextBox3.Name = "skinWaterTextBox3";
            this.skinWaterTextBox3.Size = new System.Drawing.Size(123, 26);
            this.skinWaterTextBox3.TabIndex = 7;
            this.skinWaterTextBox3.Text = "yswenli";
            this.skinWaterTextBox3.WaterColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.skinWaterTextBox3.WaterText = "UserName";
            // 
            // skinWaterTextBox2
            // 
            this.skinWaterTextBox2.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinWaterTextBox2.Location = new System.Drawing.Point(150, 22);
            this.skinWaterTextBox2.Name = "skinWaterTextBox2";
            this.skinWaterTextBox2.Size = new System.Drawing.Size(50, 26);
            this.skinWaterTextBox2.TabIndex = 6;
            this.skinWaterTextBox2.Text = "21";
            this.skinWaterTextBox2.WaterColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.skinWaterTextBox2.WaterText = "Port";
            // 
            // skinWaterTextBox1
            // 
            this.skinWaterTextBox1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinWaterTextBox1.Location = new System.Drawing.Point(17, 22);
            this.skinWaterTextBox1.Name = "skinWaterTextBox1";
            this.skinWaterTextBox1.Size = new System.Drawing.Size(121, 26);
            this.skinWaterTextBox1.TabIndex = 5;
            this.skinWaterTextBox1.Text = "127.0.0.1";
            this.skinWaterTextBox1.WaterColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.skinWaterTextBox1.WaterText = "IP";
            // 
            // logTxt
            // 
            this.logTxt.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.logTxt.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.logTxt.ForeColor = System.Drawing.Color.Green;
            this.logTxt.Location = new System.Drawing.Point(4, 570);
            this.logTxt.Multiline = true;
            this.logTxt.Name = "logTxt";
            this.logTxt.ReadOnly = true;
            this.logTxt.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.logTxt.Size = new System.Drawing.Size(981, 157);
            this.logTxt.TabIndex = 8;
            this.logTxt.WaterColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.logTxt.WaterText = "Logs";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.skinWaterTextBox4);
            this.groupBox1.Controls.Add(this.skinButton1);
            this.groupBox1.Controls.Add(this.skinWaterTextBox1);
            this.groupBox1.Controls.Add(this.skinWaterTextBox2);
            this.groupBox1.Controls.Add(this.skinWaterTextBox3);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(4, 32);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(981, 57);
            this.groupBox1.TabIndex = 10;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "FTPServer";
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(4, 89);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.dataGridView1);
            this.splitContainer2.Panel1.Controls.Add(this.textBox1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.dataGridView2);
            this.splitContainer2.Panel2.Controls.Add(this.textBox2);
            this.splitContainer2.Size = new System.Drawing.Size(981, 481);
            this.splitContainer2.SplitterDistance = 456;
            this.splitContainer2.TabIndex = 11;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.ContextMenuStrip = this.contextMenuStrip1;
            this.dataGridView1.Location = new System.Drawing.Point(4, 35);
            this.dataGridView1.MultiSelect = false;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowTemplate.Height = 23;
            this.dataGridView1.Size = new System.Drawing.Size(449, 443);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellDoubleClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.parentToolStripMenuItem,
            this.uploadToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(113, 48);
            // 
            // parentToolStripMenuItem
            // 
            this.parentToolStripMenuItem.Name = "parentToolStripMenuItem";
            this.parentToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.parentToolStripMenuItem.Text = "上一层";
            this.parentToolStripMenuItem.Click += new System.EventHandler(this.parentToolStripMenuItem_Click);
            // 
            // uploadToolStripMenuItem
            // 
            this.uploadToolStripMenuItem.Name = "uploadToolStripMenuItem";
            this.uploadToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.uploadToolStripMenuItem.Text = "上传";
            this.uploadToolStripMenuItem.Click += new System.EventHandler(this.uploadToolStripMenuItem_Click);
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox1.Location = new System.Drawing.Point(4, 4);
            this.textBox1.Margin = new System.Windows.Forms.Padding(0);
            this.textBox1.MinimumSize = new System.Drawing.Size(28, 28);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(449, 28);
            this.textBox1.TabIndex = 0;
            this.textBox1.Text = "c:\\";
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // dataGridView2
            // 
            this.dataGridView2.AllowUserToAddRows = false;
            this.dataGridView2.AllowUserToDeleteRows = false;
            this.dataGridView2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView2.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView2.ContextMenuStrip = this.contextMenuStrip2;
            this.dataGridView2.Location = new System.Drawing.Point(7, 34);
            this.dataGridView2.MultiSelect = false;
            this.dataGridView2.Name = "dataGridView2";
            this.dataGridView2.ReadOnly = true;
            this.dataGridView2.RowTemplate.Height = 23;
            this.dataGridView2.Size = new System.Drawing.Size(503, 443);
            this.dataGridView2.TabIndex = 3;
            this.dataGridView2.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView2_CellDoubleClick);
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.parentToolStripMenuItem1,
            this.downloadToolStripMenuItem});
            this.contextMenuStrip2.Name = "contextMenuStrip2";
            this.contextMenuStrip2.Size = new System.Drawing.Size(113, 48);
            // 
            // parentToolStripMenuItem1
            // 
            this.parentToolStripMenuItem1.Name = "parentToolStripMenuItem1";
            this.parentToolStripMenuItem1.Size = new System.Drawing.Size(112, 22);
            this.parentToolStripMenuItem1.Text = "上一层";
            this.parentToolStripMenuItem1.Click += new System.EventHandler(this.parentToolStripMenuItem1_Click);
            // 
            // downloadToolStripMenuItem
            // 
            this.downloadToolStripMenuItem.Name = "downloadToolStripMenuItem";
            this.downloadToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.downloadToolStripMenuItem.Text = "下载";
            this.downloadToolStripMenuItem.Click += new System.EventHandler(this.downloadToolStripMenuItem_Click);
            // 
            // textBox2
            // 
            this.textBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox2.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox2.Location = new System.Drawing.Point(7, 3);
            this.textBox2.Margin = new System.Windows.Forms.Padding(0);
            this.textBox2.MinimumSize = new System.Drawing.Size(28, 28);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(503, 28);
            this.textBox2.TabIndex = 2;
            this.textBox2.Text = "/";
            this.textBox2.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.BalloonTipTitle = "SAEA.FTPClient";
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip3;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "SAEA.FTPClient";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.notifyIcon1_DoubleClick);
            // 
            // contextMenuStrip3
            // 
            this.contextMenuStrip3.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showUIToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.contextMenuStrip3.Name = "contextMenuStrip3";
            this.contextMenuStrip3.Size = new System.Drawing.Size(125, 48);
            // 
            // showUIToolStripMenuItem
            // 
            this.showUIToolStripMenuItem.Name = "showUIToolStripMenuItem";
            this.showUIToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.showUIToolStripMenuItem.Text = "显示界面";
            this.showUIToolStripMenuItem.Click += new System.EventHandler(this.showUIToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.exitToolStripMenuItem.Text = "退出";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // FtpClientForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(989, 731);
            this.Controls.Add(this.splitContainer2);
            this.Controls.Add(this.logTxt);
            this.Controls.Add(this.groupBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FtpClientForm";
            this.Text = "FtpClientForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FtpClientForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FtpClientForm_FormClosed);
            this.Load += new System.EventHandler(this.FtpClientForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).EndInit();
            this.contextMenuStrip2.ResumeLayout(false);
            this.contextMenuStrip3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private CCWin.SkinControl.SkinButton skinButton1;
        private CCWin.SkinControl.SkinWaterTextBox skinWaterTextBox4;
        private CCWin.SkinControl.SkinWaterTextBox skinWaterTextBox3;
        private CCWin.SkinControl.SkinWaterTextBox skinWaterTextBox2;
        private CCWin.SkinControl.SkinWaterTextBox skinWaterTextBox1;
        private CCWin.SkinControl.SkinWaterTextBox logTxt;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.DataGridView dataGridView2;
        System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem uploadToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem downloadToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem parentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem parentToolStripMenuItem1;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip3;
        private System.Windows.Forms.ToolStripMenuItem showUIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    }
}