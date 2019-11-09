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
            this.gifBox1.Size = new System.Drawing.Size(354, 350);
            this.gifBox1.TabIndex = 1;
            this.gifBox1.Text = "gifBox1";
            // 
            // LoadingUserControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gifBox1);
            this.Name = "LoadingUserControl";
            this.Size = new System.Drawing.Size(354, 350);
            this.ResumeLayout(false);

        }

        #endregion

        private CCWin.SkinControl.GifBox gifBox1;
    }
}
