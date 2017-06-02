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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class DataPoint {
    public double Mass;
    public float Intensity;
    public int Scan;
    public double RT;
    public int FileID;
}

public class FileRecord {
    public String FileName;
    public int FileID;
    public SortedList<int, double> RTs = new SortedList<int, double>();
    public void Save(BinaryWriter bw) {
        bw.Write(FileID);
        bw.Write(FileName);
        bw.Write(RTs.Count);
        for(int i = 0 ; i < RTs.Count ; i++) {
            bw.Write(RTs.Keys[i]);
            bw.Write(RTs.Values[i]);
        }
    }
}

public class MassComparer : IComparer<PointStream> {
    public int Compare(PointStream x, PointStream y) {
        return x.Current.Mass.CompareTo(y.Current.Mass);
    }
}

public abstract class PointStream {
    public abstract DataPoint Current { get; } 
    public abstract bool Pull();
    public abstract List<FileRecord> GetFiles();
}

public class RCH0Stream:PointStream {

    DataPoint mCurrent = new DataPoint();

    public override DataPoint Current { get { return mCurrent; } }

    public RCH0Stream(string RCHFile, int FileID) {
        this.FileID = FileID;
        FileName = RCHFile;
        fs = new FileStream(RCHFile,FileMode.Open,FileAccess.Read);
        sr = new BinaryReader(fs);
        //Read signature - currently RCH0
        string sig = sr.ReadString();
        int SpectraCount = sr.ReadInt32();
        for(int i = 0 ; i < SpectraCount ; i++) {
            RTs.Add(sr.ReadInt32(), sr.ReadSingle());
        }
        int PageCount = sr.ReadInt32();
        for(int i = 0 ; i < PageCount ; i++) {
            sr.ReadDouble();
        }
        //sr at the position of where data starts
        Pull();
    }

    public override bool Pull() {
        if(sr == null)
            return false;
        byte[] Pbyte = sr.ReadBytes(16);
        if(Pbyte.Length < 16) {
            sr.Close();
            sr = null;
            return false;
        }
        mCurrent.Mass = BitConverter.ToDouble(Pbyte,0);
        mCurrent.Intensity = BitConverter.ToSingle(Pbyte,8);
        mCurrent.Scan = Convert.ToInt32(BitConverter.ToSingle(Pbyte,12));
        mCurrent.RT = RTs[Current.Scan];
        mCurrent.FileID = FileID;
        return true;
    }

    public FileStream fs;
    public BinaryReader sr;
    public SortedList<int, double> RTs = new SortedList<int, double>();
    string FileName;
    int FileID;

    public override List<FileRecord> GetFiles() {
        List<FileRecord> Res = new List<FileRecord>();
        Res.Add(new FileRecord { FileName = this.FileName, FileID = this.FileID, RTs = this.RTs } );
        return Res;
    }
}

public class RCH1Stream : PointStream {

    DataPoint mCurrent = new DataPoint();

    public override DataPoint Current { get { return mCurrent; } }

    public BinaryReader sr;
    public string FileName;
    List<FileRecord> Files;

    public RCH1Stream(string FileName) {
        this.FileName = FileName;
        Files = new List<FileRecord>();
        sr = new BinaryReader(File.Open(FileName,FileMode.Open,FileAccess.Read,FileShare.Read),new ASCIIEncoding());
        //Read signature - currently RCH1
        string sig = sr.ReadString();
        int FileCount = sr.ReadInt32();
        for (int i = 0 ; i < FileCount ; i++) {
            FileRecord FR = new FileRecord();
            FR.FileID = sr.ReadInt32();
            FR.FileName = sr.ReadString();
            int SpectraCount = sr.ReadInt32();
            FR.RTs = new SortedList<int, double>();
            for(int j = 0 ; j < SpectraCount ; j++) {
                FR.RTs.Add(sr.ReadInt32(), sr.ReadDouble());
            }
            Files.Add(FR);
        }
        int PageCount = sr.ReadInt32();
        sr.BaseStream.Seek(PageCount*8,SeekOrigin.Current);
    }

    public override bool Pull() {
        byte[] Pbyte = sr.ReadBytes(20);
        if(Pbyte.Length < 20) {
            sr.Close();
            return false;
        }
        mCurrent.Mass = BitConverter.ToDouble(Pbyte,0);
        mCurrent.Intensity = BitConverter.ToSingle(Pbyte,8);
        mCurrent.Scan = Convert.ToInt32(BitConverter.ToSingle(Pbyte,12));
        mCurrent.FileID = BitConverter.ToInt32(Pbyte, 16);
        mCurrent.RT = Files.First(a => a.FileID == mCurrent.FileID).RTs[mCurrent.Scan];
        return true;
    }

    public override List<FileRecord> GetFiles() {
        return Files;
    }

    public void Close() {
        sr.Close();
    }

}


public class PointStreamSet:PointStream {

    public override bool Pull() {
        PointStream PS = PList[0];
        PList.RemoveAt(0);
        if (PS.Pull()) 
            Insert(PS);
        if(PList.Count == 0)
            return false;
        return true;
    }

    List<PointStream> PList = new List<PointStream>();
    List<PointStream> FList = new List<PointStream>();

    public void AddtoList(PointStream NewList) {
        Insert(NewList);
        FList.Add(NewList);
    }

    void Insert(PointStream PS) {
        int Index = PList.BinarySearch(PS,new MassComparer());
        if(Index < 0)
            Index = ~Index;
        PList.Insert(Index,PS);
    }

    public override DataPoint Current { get { return PList[0].Current; } }

    public override List<FileRecord> GetFiles() {
        List<FileRecord> Res = new List<FileRecord>();
        foreach(PointStream ps in FList) {
            Res.AddRange(ps.GetFiles());
        }
        return Res;
    }

}

public partial class Cashing {

    static void RCH1Create(string CachePath) {

        PointStreamSet PSSet = new PointStreamSet();
        List<double> StartPageMasses = new List<double>();
        //look if there is folder.cache and load it
        int IDCount = 0;
        List<FileRecord> CFiles = new List<FileRecord>();

        if(File.Exists(CachePath + Path.DirectorySeparatorChar + "folder.cache")) {
            RCH1Stream FStr = new RCH1Stream(CachePath + Path.DirectorySeparatorChar + "folder.cache");
            CFiles = FStr.GetFiles();
            IDCount = CFiles.Max(cf => cf.FileID) + 1;
            PSSet.AddtoList(FStr);
        }


        //look for RCH and make collection 
        IEnumerable<string> RCHFiles = Directory.EnumerateFiles(CachePath, "*.rch");
        foreach(string FileName in RCHFiles) {
            if(CFiles.FirstOrDefault(cf => cf.FileName == FileName) == null) {
                RCH0Stream Str = new RCH0Stream(FileName, IDCount);
                IDCount++;
                PSSet.AddtoList(Str);
            }
        }

        if(PSSet.GetFiles().Count == 0)
            return;
        //Pull-Push datapoints to temp file
        int PageSpaceCounter = 64000;
        BinaryWriter tempw = new BinaryWriter(File.Open(CachePath+Path.DirectorySeparatorChar+"tmpfolder.cache", FileMode.Create));
            
        do {
            if (PageSpaceCounter == 64000) {
                //page headers
                PageSpaceCounter = 0;
                StartPageMasses.Add(PSSet.Current.Mass);
            }
            tempw.Write(PSSet.Current.Mass);
            tempw.Write(PSSet.Current.Intensity);
            tempw.Write(PSSet.Current.Scan);
            tempw.Write(PSSet.Current.FileID);
            PageSpaceCounter += 20;
        } while(PSSet.Pull());
        tempw.Close();

        BinaryWriter bw = new BinaryWriter(File.Open(CachePath+Path.DirectorySeparatorChar+"folder.cache", FileMode.Create));
        bw.Write("RCH1");

        //Save Files
        List<FileRecord> Files = PSSet.GetFiles();
        bw.Write(Files.Count);
        foreach(FileRecord fr in Files) {
            fr.Save(bw);
        }

        //Save Page Headers 
        bw.Write(StartPageMasses.Count);
        foreach(double M in StartPageMasses) {
            bw.Write(M);
        }
           
        //Attach data 

        BinaryReader dr = new BinaryReader(File.Open(CachePath+Path.DirectorySeparatorChar+"tmpfolder.cache",FileMode.Open),new ASCIIEncoding());
        while(dr.PeekChar() >= 0) {
            Byte[] R = dr.ReadBytes(1000000);
            bw.Write(R);
        }
        dr.Close();
        bw.Close();
        File.Delete(CachePath+Path.DirectorySeparatorChar+"tmpfolder.cache");
    }

    public static List<string> RCH1GetFiles(string FileName) {

        List<string> Res = new List<string>();
        BinaryReader sr = 
            new BinaryReader(File.Open(FileName,FileMode.Open,FileAccess.Read,FileShare.Read),new ASCIIEncoding());
        //Read signature - currently RCH1
        string sig = sr.ReadString();
        int FileCount = sr.ReadInt32();
        for (int i = 0 ; i < FileCount ; i++) {
            sr.ReadInt32();
            Res.Add(sr.ReadString());
            int SpectraCount = sr.ReadInt32();
            sr.BaseStream.Seek(SpectraCount * 12,SeekOrigin.Current);
        }
        sr.Close();
        return Res;
    }

    public static bool RCH1Complete(string FileName) {
        try {
            BinaryReader sr = null;
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            try {
                sr = new BinaryReader(fs);
                string sig = sr.ReadString();
                int FileCount = sr.ReadInt32();
                for (int i = 0 ; i < FileCount ; i++) {
                    sr.ReadInt32();
                    sr.ReadString();
                    int SpectraCount = sr.ReadInt32();
                    sr.BaseStream.Seek(SpectraCount * 12,SeekOrigin.Current);
                }
                int PageCount = sr.ReadInt32();
                fs.Seek(PageCount * 8, SeekOrigin.Current);
                long FileLen = fs.Length;
                if((fs.Length - fs.Position) % 64000 == 0) {
                    return (fs.Length - fs.Position) / 64000 == PageCount;
                }
                return (fs.Length - fs.Position) / 64000 == PageCount - 1;
            }
            finally {
                fs.Close();
            }
        }catch(Exception e) {
            return false;
        }
    }

}

