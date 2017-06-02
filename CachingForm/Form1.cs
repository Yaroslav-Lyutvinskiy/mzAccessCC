using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;

namespace CachingForm {
    public partial class Form1 : Form {

        System.Timers.Timer MainTimer;

        public Form1() {
            InitializeComponent();
            Cashing.ThermoThreshold = Properties.Settings.Default.ThermoThrehold;
            Cashing.AgilentThreshold = Properties.Settings.Default.AgilentThreshold;
            MainTimer = new System.Timers.Timer(Properties.Settings.Default.Period*100.0);
            MainTimer.Elapsed += MainTimer_Elapsed;
            MainTimer.Start();
        }

        private void MainTimer_Elapsed(object sender, ElapsedEventArgs e) {
            MainTimer.Stop();
            bool RestartService = false;
            if(textBox1.Text != "") {
                RestartService |= Cashing.TraverseTree(textBox1.Text,false);
            } else {
                //use settings
                foreach(string root in Properties.Settings.Default.RootFolders) {
                    RestartService |= Cashing.TraverseTree(root,false);
                }
            }
            if(RestartService) {
                MSDataService.MSDataService Service = new MSDataService.MSDataService();
                Service.ServiceRescan();
            }
            MainTimer.Start();
        }


        private void button1_Click(object sender, EventArgs e) {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK) {
                textBox1.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            if(textBox1.Text != "") {
                Cashing.TraverseTree(textBox1.Text,false);
            } else {
                //use settings
                foreach(string root in Properties.Settings.Default.RootFolders) {
                    Cashing.TraverseTree(root,false);
                }
            }
        }

        public void Log(string Message) {
            textBox2.Text+=Message+"\n";
        }

        private void button3_Click(object sender, EventArgs e) {
            int files = 0, folders = 0;
            if(textBox1.Text != "") {
                Cashing.TraverseTreePreview(textBox1.Text,ref files,ref folders);
            } else {
                //use settings
                foreach(string root in Properties.Settings.Default.RootFolders) {
                    Cashing.TraverseTreePreview(root, ref files, ref folders);
                }
            }
        }
    }
}
