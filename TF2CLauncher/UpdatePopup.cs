using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TF2CLauncher
{
    public partial class UpdatePopup : Form
    {
        public UpdatePopup()
        {
            InitializeComponent();
            iconPictureBox.Image = SystemIcons.Information.ToBitmap();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://wiki.tf2classic.com/wiki/Updating");
        }
    }
}
