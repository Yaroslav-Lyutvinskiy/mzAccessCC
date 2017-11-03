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
using System.IO;
using ZetaLongPaths;

public partial class Cashing {
    //Fills file cashe by raw files

    public delegate void LogDelegate(string Message);
    public static LogDelegate Log;

    //static public void Log(String Message) {
    //    Console.WriteLine(Message);
    //}

    public static double ThermoThreshold = 0.0;
    public static double AgilentThreshold = 0.0;

    public static bool AgilentCacheOn = true; 
    public static bool mzMLCacheOn = true; 
    public static bool ThermoCacheOn = true; 

    static List<string> RawFiles;
    static List<string> R0Files;//key - file to cahce, value - Fact file
    static List<string> R1Files;


    static public bool TraverseTree(string root,bool Restart) {
        // Data structure to hold names of subfolders to be
        // examined for files.

        int Failed = 0;

        RawFiles = new List<string>();
        R0Files = new List<string>();
        bool RestartWebService = false;

        if(root == "")
            return false;
        Stack<string> dirs = new Stack<string>(20);

        if(!ZlpIOHelper.DirectoryExists(root)) {
            throw new ArgumentException();
        }
        dirs.Push(root);

        while(dirs.Count > 0) {
            string currentDir = dirs.Pop();

            ZlpDirectoryInfo di = new ZlpDirectoryInfo(currentDir);

            ZlpDirectoryInfo[] subDirs;
            try {
                subDirs = ZlpIOHelper.GetDirectories(currentDir);
            }
            catch(Exception e) {
                Log("TraverseTree: " + e.Message);
                continue;
            }

            ZlpFileInfo[] files = null;
            try {
                files = ZlpIOHelper.GetFiles(currentDir);
            }
            catch(Exception e) {
                Log("TraverseTree: " + e.Message);
                continue;
            }

            RawFiles.Clear();
            R0Files.Clear();
            R1Files = null;

            // Perform the required action on each file here.
            // Modify this block to perform your required task.
            foreach(ZlpFileInfo file in files) {
                try {
                    // Perform whatever action is required in your scenario.
                    //Console.WriteLine("{0}: {1}, {2}", file.Name, file.Length, file.LastWriteTime);
                    string Ext = file.Extension;
                    if((Ext.ToLower() == ".raw" && ThermoCacheOn) || (Ext.ToLower() == ".mzml" && mzMLCacheOn)) {
                        RawFiles.Add(file.FullName);
                    }
                    if(Ext == ".rch") {
                        if(RCH0Complete(file.FullName)) {
                            R0Files.Add(file.FullName);
                        }
                    }
                    if(file.Name == "folder.cache") {
                        if(RCH1Complete(file.FullName)) {
                            R1Files = RCH1GetFiles(file.FullName);
                        }
                    }
                    if(file.Name == "tmpfolder.cache") {
                        file.Delete();
                    }
                }
                catch(Exception e) {
                    // If file was deleted by a separate application
                    //  or thread since the call to TraverseTree()
                    // then just continue.
                    Log("TraverseTree: " + e.Message);
                    continue;
                }
            }

            // Push the subdirectories onto the stack for traversal.
            // This could also be done before handing the files.
            foreach(ZlpDirectoryInfo dir in subDirs) {
                try {
                    if(dir.FullName.IndexOf(".d") == dir.FullName.Length - 2 && AgilentCacheOn) {
                        RawFiles.Add(dir.FullName);
                    } else {
                        dirs.Push(dir.FullName);
                    }
                }
                catch(Exception e) {
                    Log("TraverseTree - AddDir: " + dir.FullName + e.Message);
                    continue;
                }
            }
            //join cache collections
            List<string> RFiles = new List<string>();
            RFiles.AddRange(R0Files);
            if (R1Files != null) RFiles.AddRange(R1Files);
            bool R1UpdateFlag = false;
            //folder collections are ready

            //if there is no cache for some raw file - make RCH0 of them
            foreach(string F in RawFiles) {
                if(RFiles.Find(x => (x.Substring(0,Math.Max(x.LastIndexOf(".")-1,0)) == F.Substring(0,F.LastIndexOf(".")-1))) == null) {
                    //Threshold should be rawdata dependant
                    string Ext = Path.GetExtension(F);
                    double Thres = 0.0;
                    if(Ext == ".raw")
                        Thres = ThermoThreshold;
                    if(Ext == ".d")
                        Thres = AgilentThreshold;
                    try {
                        RCH0Create(F, Thres);
                    }
                    catch(Exception e) {
                        if(Restart) {
                            Log(String.Format("Chaching for {0} cannot be finished. Service is to be restarted. \nException {1} \nSource:{2} \nCall Stack:{3}", F, e.Message, e.Source, e.StackTrace));
                            Environment.Exit(1);
                        } else {
                            Failed++;
                            Log(String.Format("Chaching for {0} cannot be finished. \nException {1} \nSource:{2} \nCall Stack:{3}", F, e.Message, e.Source, e.StackTrace));
                        }
                    }
                    Log(String.Format("File \"{0}\" has been cached.", F));
                    R1UpdateFlag = true;
                }
            }
            //if there are some RCH0 not joined to RCH1 - join them
            foreach(string R0 in R0Files) {
                if(R1Files == null || !R1Files.Contains(R0)) {
                    R1UpdateFlag = true;
                    break;
                }
            }

            //Here could be conditions for RCH1 generation
            if(R1UpdateFlag) { 
                RCH1Create(currentDir);
                Log(String.Format("Folder cache for path \"{0}\" has been created/updated.", currentDir));
                RestartWebService = true;
            }


            //delete excessive RCH0
            IEnumerable<string> RCHFiles = Directory.EnumerateFiles(currentDir, "*.rch");
            foreach(string f in RCHFiles)
                File.Delete(f);
        }

        if(Failed > 0) {
            Log(String.Format("Totaly {0} files caused exception", Failed));
            Success = false;
        } else {
            Success = true;
        }

        return RestartWebService;
    }

    static public bool Success = true;

    static public void TraverseTreePreview(string root, ref int Files, ref int Folders) {
        // Data structure to hold names of subfolders to be
        // examined for files.

        RawFiles = new List<string>();
        R0Files = new List<string>();

        if(root == "")
            return;
        Stack<string> dirs = new Stack<string>(20);

        int FilesToCache = 0;
        int FolderToCache = 0;

        if(!ZlpIOHelper.DirectoryExists(root)) {
            throw new ArgumentException();
        }
        dirs.Push(root);

        while(dirs.Count > 0) {
            string currentDir = dirs.Pop();
            ZlpDirectoryInfo di = new ZlpDirectoryInfo(currentDir);
            ZlpDirectoryInfo[] subDirs;
            subDirs = ZlpIOHelper.GetDirectories(currentDir);
            ZlpFileInfo[] files = null;
            files = ZlpIOHelper.GetFiles(currentDir);

            RawFiles.Clear();
            R0Files.Clear();
            R1Files = null;

            // Perform the required action on each file here.
            // Modify this block to perform your required task.
            foreach(ZlpFileInfo file in files) {
                string Ext = file.Extension;
                if((Ext.ToLower() == ".raw" && ThermoCacheOn) || (Ext.ToLower() == ".mzml" && mzMLCacheOn)) {
                    RawFiles.Add(file.FullName);
                }
                if(Ext == ".rch") {
                    R0Files.Add(file.FullName);
                }
                if(file.Name == "folder.cache") {
                    R1Files = RCH1GetFiles(file.FullName);
                }
            }

            // Push the subdirectories onto the stack for traversal.
            // This could also be done before handing the files.
            foreach(ZlpDirectoryInfo dir in subDirs) {
                if(dir.FullName.IndexOf(".d") == dir.FullName.Length - 2 && AgilentCacheOn) {
                    RawFiles.Add(dir.FullName);
                } else {
                    dirs.Push(dir.FullName);
                }
            }
            //join cache collections
            List<string> RFiles = new List<string>();
            RFiles.AddRange(R0Files);
            if (R1Files != null) RFiles.AddRange(R1Files);

            bool R1UpdateFlag = false;
            //folder collections are ready

            foreach(string F in RawFiles) {
                if(RFiles.Find(x => (x.Substring(0,Math.Max(0,x.LastIndexOf(".")-1)) == F.Substring(0,F.LastIndexOf(".")-1))) == null) {
                    Log(String.Format("File \"{0}\" has not been cached.", F));
                    FilesToCache++;
                    R1UpdateFlag = true;
                }
            }
            foreach(string R0 in R0Files) {
                if(R1Files == null || !R1Files.Contains(R0)) {
                    R1UpdateFlag = true;
                    break;
                }
            }

            if(R1UpdateFlag) { 
                Log(String.Format("Folder cache for path \"{0}\" need to be created/updated.", currentDir));
                FolderToCache++;

            }
        }
        Log(String.Format("Totaly {0} files to be cached.", FilesToCache));
        Files = FilesToCache;
        Log(String.Format("Totaly {0} folder caches need to be created/updated.", FolderToCache));
        Folders = FolderToCache;
    }
}
