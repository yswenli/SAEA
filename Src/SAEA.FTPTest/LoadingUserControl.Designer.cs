namespace SAEA.FTPTest
{
    partial class LoadingUserControl
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadingUserControl));
            this.gifBox1 = new CCWin.SkinControl.GifBox();
            this.skinLabel1 = new CCWin.SkinControl.SkinLabel();
            this.SuspendLayout();
            // 
            // gifBox1
            // 
            this.gifBox1.BorderColor = System.Drawing.Color.Transparent;
            this.gifBox1.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.gifBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gifBox1.Image = ((System.Drawing.Image)(resources.GetObject("gifBox1.Image")));
            this.gifBox1.Location = new System.Drawing.Point(0, 0);
            this.gifBox1.Name = "gifBox1";
            this.gifBox1.Size = new System.Drawing.Size(354, 370);
            this.gifBox1.TabIndex = 1;
            this.gifBox1.Text = "gifBox1";
            // 
            // skinLabel1
            // 
            this.skinLabel1.BackColor = System.Drawing.Color.Transparent;
            this.skinLabel1.BorderColor = System.Drawing.Color.Transparent;
            this.skinLabel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.skinLabel1.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.skinLabel1.Location = new System.Drawing.Point(0, 350);
            this.skinLabel1.Name = "skinLabel1";
            this.skinLabel1.Size = new System.Drawing.Size(354, 20);
            this.skinLabel1.TabIndex = 2;
            this.skinLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // LoadingUserControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.skinLabel1);
            this.Controls.Add(this.gifBox1);
            this.Name = "LoadingUserControl";
            this.Size = new System.Drawing.Size(354, 370);
            this.ResumeLayout(false);

        }

        #endregion

        private CCWin.SkinControl.GifBox gifBox1;
        private CCWin.SkinControl.SkinLabel skinLabel1;
    }
}
