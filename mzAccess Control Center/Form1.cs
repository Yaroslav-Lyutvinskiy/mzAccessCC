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
using System.Xml;
using Microsoft.Win32;
using System.ServiceModel.Configuration;

namespace mzAccess_Control_Center {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        XmlDocument wConfig = new XmlDocument();
        XmlNode N;
        WSSettings WSS = new WSSettings();
        public CSSettings CSS = null; 
        string wConfigFileName;
        RegistryKey RR;

        private void Form1_Load(object sender, EventArgs e) {
            CSS = new CSSettings(this);
            RegistryKey R = Registry.LocalMachine;
            RR =  R.OpenSubKey("SOFTWARE\\mzAccess Server");
            if(RR == null) {
                MessageBox.Show("Can't find installation paths for mzAccess.\n Probably, mzAccess has not been installed.");
                Application.Exit();
            }
            wConfigFileName = RR.GetValue("IISPath").ToString()+"Web.config";
            wConfig.Load(wConfigFileName);
            N = wConfig.SelectSingleNode("/configuration/appSettings");   
            foreach(XmlNode NN in N.ChildNodes) {
                if(NN.Attributes == null)
                    continue;
                if (NN.Attributes["key"].Value == "ThermoEnabled") {
                    WSS.ThermoEnabled = Convert.ToBoolean(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "ThermoFiles") {
                    WSS.ThermoFiles = Convert.ToInt32(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "AgilentEnabled") {
                    WSS.AgilentEnabled = Convert.ToBoolean(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "AgilentFiles") {
                    WSS.AgilentFiles = Convert.ToInt32(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "mzMLEnabled") {
                    WSS.mzMLEnabled = Convert.ToBoolean(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "mzMLFiles") {
                    WSS.mzMLFiles = Convert.ToInt32(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "CasheEnabled") {
                    WSS.CasheEnabled = Convert.ToBoolean(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value == "RCHFiles") {
                    WSS.RCHFiles  = Convert.ToInt32(NN.Attributes["value"].Value);
                }
                if (NN.Attributes["key"].Value.Contains("Root")) {
                    listBox1.Items.Add(NN.Attributes["value"].Value);
                }
            }
            propertyGrid1.SelectedObject = WSS;

            linkLabel1.Links.Add(0,26,"http://localhost/mzAccess/");
            linkLabel2.Links.Add(0,43,"http://localhost/mzAccess/Service.asmx?WSDL");
            linkLabel3.Links.Add(0,24,"http://www.mzaccess.org/");

            RegistryKey CachSettings = RR.OpenSubKey("CServiceSettings");
            CSS.AgilentThreshold = Convert.ToDouble(CachSettings.GetValue("AgilentThreshold").ToString());
            CSS.ThermoThreshold = Convert.ToDouble(CachSettings.GetValue("ThermoThreshold").ToString());
            CSS.Period = Convert.ToInt32(CachSettings.GetValue("Period").ToString());
            CSS._AgilentCacheOn = Convert.ToInt32(CachSettings.GetValue("AgilentCacheOn").ToString()) == 0 ? false : true;
            CSS._ThermoCacheOn = Convert.ToInt32(CachSettings.GetValue("ThermoCacheOn").ToString()) == 0 ? false : true;
            CSS._mzMLCacheOn = Convert.ToInt32(CachSettings.GetValue("mzMLCacheOn").ToString()) == 0 ? false : true;
            propertyGrid2.SelectedObject = CSS;

            //if service does not exists - uncheck and gray checkbox
            if(!ServiceInstaller.ServiceIsInstalled("CachingService.exe")) {
                checkBox1.Checked = false;
                checkBox1.Enabled = false;
            } else {
                //Stop service 
                ServiceInstaller.StopService("CachingService.exe");
                //if service on demand uncheck box, if server on autostart - scheck box
                ServiceBootFlag SBF = ServiceInstaller.GetBootFlag("CachingService.exe");
                if(SBF == ServiceBootFlag.DemandStart || SBF == ServiceBootFlag.Disabled) {
                    checkBox1.Checked = false;
                } else {
                    checkBox1.Checked = true;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK) {
                listBox1.Items.Add(folderBrowserDialog1.SelectedPath+Path.DirectorySeparatorChar);
                PreviewFiles();
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            if (listBox1.SelectedItem != null) {
                listBox1.Items.Remove(listBox1.SelectedItem);
                PreviewFiles();
            }
        }

        private void ApplyButton_Click(object sender, EventArgs e) {
            //Web service settings
            foreach(XmlNode NN in N.ChildNodes) {
                if(NN.Attributes == null)
                    continue;
                if (NN.Attributes["key"].Value == "ThermoEnabled") {
                    NN.Attributes["value"].Value = WSS.ThermoEnabled.ToString();
                }
                if (NN.Attributes["key"].Value == "ThermoFiles") {
                    NN.Attributes["value"].Value = WSS.ThermoFiles.ToString();
                }
                if (NN.Attributes["key"].Value == "AgilentEnabled") {
                    NN.Attributes["value"].Value = WSS.AgilentEnabled.ToString();
                }
                if (NN.Attributes["key"].Value == "AgilentFiles") {
                    NN.Attributes["value"].Value = WSS.AgilentFiles.ToString();
                }
                if (NN.Attributes["key"].Value == "mzMLEnabled") {
                    NN.Attributes["value"].Value = WSS.mzMLEnabled.ToString();
                }
                if (NN.Attributes["key"].Value == "mzMLFiles") {
                    NN.Attributes["value"].Value = WSS.mzMLFiles.ToString();
                }
                if (NN.Attributes["key"].Value == "CasheEnabled") {
                    NN.Attributes["value"].Value = WSS.CasheEnabled.ToString();
                }
                if (NN.Attributes["key"].Value == "RCHFiles") {
                    NN.Attributes["value"].Value = WSS.RCHFiles.ToString();
                }
                if (NN.Attributes["key"].Value.Contains("Root")) {
                    N.RemoveChild(NN);
                }
            }
            for(int i = 1 ; i<=listBox1.Items.Count ; i++) {
                XmlNode NewPath = wConfig.CreateElement("add");
                XmlAttribute key = wConfig.CreateAttribute("key");
                key.Value = "Root" + i.ToString();
                NewPath.Attributes.Append(key);
                XmlAttribute value = wConfig.CreateAttribute("value");
                value.Value = listBox1.Items[i - 1].ToString();
                NewPath.Attributes.Append(value);
                N.AppendChild(NewPath);
            }
            wConfig.Save(wConfigFileName);
            //Settings to register
            RegistryKey CachSettings = RR.OpenSubKey("CServiceSettings",true);
            CachSettings.SetValue("AgilentThreshold", Convert.ToString(CSS.AgilentThreshold));
            CachSettings.SetValue("ThermoThreshold", Convert.ToString(CSS.ThermoThreshold));
            CachSettings.SetValue("Period", Convert.ToString(CSS.Period));

            CachSettings.SetValue("AgilentCacheOn", Convert.ToString(CSS.AgilentCacheOn?1:0));
            CachSettings.SetValue("ThermoCacheOn", Convert.ToString(CSS.ThermoCacheOn?1:0));
            CachSettings.SetValue("mzMLCacheOn", Convert.ToString(CSS.mzMLCacheOn?1:0));

            //Roots
            string[] Values = CachSettings.GetValueNames();
            foreach(string s in Values) {
                if (s.IndexOf("Root") == 0) {
                    CachSettings.DeleteValue(s);
                }
            }
            for(int i = 1 ; i <= listBox1.Items.Count ; i++) {
                CachSettings.SetValue("Root" + i.ToString(), listBox1.Items[i - 1].ToString());
            }
            // checkbox unchecked - do nothing 
            // if box is checked - set it to automatic and start service
            if(checkBox1.Checked) {
                ServiceInstaller.SetBootFlag("CachingService.exe", ServiceBootFlag.AutoStart);
                ServiceInstaller.StartService("CachingService.exe");
            } else {
                ServiceInstaller.SetBootFlag("CachingService.exe", ServiceBootFlag.DemandStart);
            }
            Close();
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }

        private void button5_Click(object sender, EventArgs e) {
            this.Close();
        }

        public void PreviewFiles() {
            int FilesCount = 0, FoldersCount = 0;
            int files = 0, folders = 0;
            label1.Text = "Files for caching are being counted...";
            label1.ForeColor = Color.DarkRed;
            Application.UseWaitCursor = true;
            Application.DoEvents();
            Cashing.ThermoThreshold = CSS.ThermoThreshold;
            Cashing.AgilentThreshold = CSS.AgilentThreshold;
            Cashing.AgilentCacheOn = CSS.AgilentCacheOn;
            Cashing.ThermoCacheOn = CSS.ThermoCacheOn;
            Cashing.mzMLCacheOn = CSS.mzMLCacheOn;

            for(int i = 0 ; i < listBox1.Items.Count ; i++) {
                Cashing.TraverseTreePreview(listBox1.Items[i].ToString(), ref files, ref folders);
                FilesCount += files;
                FoldersCount += folders;
                label1.Text = String.Format("{0} files in {1} folders need to be cached",FilesCount,FoldersCount);
                Application.DoEvents();
            }
            label1.Text = String.Format("{0} files in {1} folders need to be cached",FilesCount,FoldersCount);
            label1.ForeColor = Color.FromKnownColor(KnownColor.ControlText);
            Application.UseWaitCursor = false;
        }

        public void Log(string Message) {
            Console.WriteLine(Message);
        }

        private void Form1_Shown(object sender, EventArgs e) {
            Cashing.Log = Log;
            PreviewFiles();
        }

        private void button3_Click(object sender, EventArgs e) {
            Form2 F = new Form2();
            F.MainForm = this;
            F.ShowDialog();
            Cashing.Log = Log;
            PreviewFiles();
        }
    }

    class WSSettings {
        public bool ThermoEnabled { get; set; }
        public int ThermoFiles { get; set; }
        public bool AgilentEnabled { get; set; }
        public int AgilentFiles { get; set; }
        public bool mzMLEnabled { get; set; }
        public int mzMLFiles { get; set; }
        public bool CasheEnabled { get; set; }
        public int RCHFiles { get; set; }
        public int FileTimeOut { get; set; }
    }

    public class CSSettings {
        public CSSettings(Form1 F) {
            this.F = F;
        }
        Form1 F;
        public double ThermoThreshold { get; set;  }
        public double AgilentThreshold { get; set;  }
        public bool _ThermoCacheOn; 
        public bool ThermoCacheOn {
            get { return _ThermoCacheOn; }
            set { _ThermoCacheOn = value; F.PreviewFiles(); } }
        public bool _AgilentCacheOn; 
        public bool AgilentCacheOn {
            get { return _AgilentCacheOn; }
            set { _AgilentCacheOn = value; F.PreviewFiles(); } }
        public bool _mzMLCacheOn; 
        public bool mzMLCacheOn {
            get { return _mzMLCacheOn; }
            set { _mzMLCacheOn = value; F.PreviewFiles(); } }
        public int Period { get; set; }
    }


}
