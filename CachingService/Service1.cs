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
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CachingService {
    public partial class Service1 : ServiceBase {
        public Service1() {
            InitializeComponent();
        }

        System.Timers.Timer MainTimer;

        List<string> Roots = new List<string>();

        void Log(string Message) {
            EventLog appLog = 
                new EventLog("Application");
            appLog.Source = "Application";
            appLog.WriteEntry(Message);
        }

        public EventLog appLog;
        RegistryKey CachSettings;

        protected override void OnStart(string[] args) {
            RegistryKey R = Registry.LocalMachine;
            RegistryKey RR =  R.OpenSubKey("SOFTWARE\\mzAccess Server");
            CachSettings = RR.OpenSubKey("CServiceSettings");
            Cashing.ThermoThreshold = Convert.ToDouble(CachSettings.GetValue("AgilentThreshold").ToString());
            Cashing.AgilentThreshold = Convert.ToDouble(CachSettings.GetValue("ThermoThreshold").ToString());
            Cashing.AgilentCacheOn = Convert.ToInt32(CachSettings.GetValue("AgilentCacheOn").ToString()) == 0 ? false : true;
            Cashing.ThermoCacheOn = Convert.ToInt32(CachSettings.GetValue("ThermoCacheOn ").ToString()) == 0 ? false : true;
            Cashing.mzMLCacheOn = Convert.ToInt32(CachSettings.GetValue("mzMLCacheOn").ToString()) == 0 ? false : true;
            Cashing.Log = Log;

            MainTimer = new System.Timers.Timer(Convert.ToInt32(CachSettings.GetValue("Period").ToString())*60000.0);
            MainTimer.Elapsed += MainTimer_Elapsed;
            MainTimer.Start();
        }

        private void MainTimer_Elapsed(object sender, ElapsedEventArgs e) {
            MainTimer.Stop();
            bool RestartService = false;
            string[] Values = CachSettings.GetValueNames();
            foreach(string s in Values) {
                if (s.IndexOf("Root") == 0) {
                    RestartService |= Cashing.TraverseTree(CachSettings.GetValue(s).ToString(),true);
                }
            }
            if(RestartService) {
                MSDataService.MSDataService Service = new MSDataService.MSDataService();
                Service.ServiceRescan();
            }
            MainTimer.Start();
        }


        protected override void OnStop() {
        }
    }
}
