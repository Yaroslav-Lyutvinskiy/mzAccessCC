/*******************************************************************************
  Copyright 2015-2017 Yaroslav Lyutvinskiy <Yaroslav.Lyutvinskiy@ki.se> and 
  Roland Nilsson <Roland.Nilsson@ki.se>
 
  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.using System;
 
 *******************************************************************************/

 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;

namespace mzAccess_Control_Center {
    public partial class Form2 : Form {
        public Form2() {
            InitializeComponent();
        }

        public void Log(string Message) {
            List<string> L = textBox1.Lines.ToList();
            L.Add(Message);
            textBox1.Lines = L.ToArray();
            Application.DoEvents();
        }

        public Form1 MainForm;

        private void Form2_Shown(object sender, EventArgs e) {
            Cashing.Log = Log;
            Application.UseWaitCursor = true;
            Application.DoEvents();
            Cashing.ThermoThreshold = MainForm.CSS.ThermoThreshold;
            Cashing.AgilentThreshold = MainForm.CSS.AgilentThreshold;
            Cashing.AgilentCacheOn = MainForm.CSS.AgilentCacheOn;
            Cashing.ThermoCacheOn = MainForm.CSS.ThermoCacheOn;
            Cashing.mzMLCacheOn = MainForm.CSS.mzMLCacheOn;
            for(int i = 0 ; i < MainForm.listBox1.Items.Count ; i++) {
                Cashing.TraverseTree(MainForm.listBox1.Items[i].ToString(),false);
                Application.DoEvents();
            }
            Application.UseWaitCursor = false;
        }

        private void button1_Click(object sender, EventArgs e) {
            Close();
        }

        private void button2_Click(object sender, EventArgs e) {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                StreamWriter sw = new StreamWriter(saveFileDialog1.FileName);
                foreach(string s in textBox1.Lines) {
                    sw.WriteLine(s);
                }
                sw.Close();
            }
        }
    }
}
