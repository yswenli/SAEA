using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class LoadingUserControl : UserControl
    {
        public LoadingUserControl()
        {
            InitializeComponent();
        }

        public void Show(Form parent)
        {
            var action = new Action(() =>
            {
                this.Visible = true;
                this.BringToFront();
            });

            if (parent.InvokeRequired)
            {
                parent.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }

        public void Hide(Form parent)
        {
            var action = new Action(() =>
            {
                this.Visible = false;
            });

            if (parent.InvokeRequired)
            {
                parent.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }
    }
}
