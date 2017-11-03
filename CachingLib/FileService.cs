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
using System.Linq;
using System.Text;
using System.IO;

class FileService{

    static public bool StickMode = true;


    public FileBox RawFile;
    public FileService(string FileName){
        string Ext = Path.GetExtension(FileName);

        switch (Ext.ToLower()){
            case ".raw" : {
                RawFile = new RawFileBox();
                break;
            }
            case ".mzml": {
                RawFile = new mzMLBox();
                break;
            }
            case ".d": {
                RawFile = new AgilentFileBox();
                break;
            }
        }
        RawFile.StickMode = StickMode;
        RawFile.RawLabel = true;
        RawFile.LoadIndex(FileName);
        RawFile.RTCorrection = true;
        MZData.SetRawFile(RawFile);
        double EndRT=0.0;
        for(int i = RawFile.RawSpectra.Length-1;i>0;i--){
            EndRT= RawFile.RawSpectra[i].RT;
            if (EndRT>0.0)break;
        }
        RawFile.LoadInterval(0.0, EndRT);
    }

    public List<MZData> DataMap = new List<MZData>();

    public static int CompMZDatabyIntensity(MZData x,MZData y){
        return (x.Intensity==y.Intensity)?0:(x.Intensity>y.Intensity?-1:1);
    }

    public static int CompMZDatabyMZ(MZData x,MZData y){
        return (x.Mass==y.Mass)?((x.RT==y.RT)?0:(x.RT<y.RT?-1:1)):(x.Mass<y.Mass?-1:1);
    }

    public class MZDatabyMZ:IComparer<MZData>{
        public int Compare(MZData x, MZData y){
            return (x.Mass==y.Mass)?
                (x.RT==y.RT?0:(x.RT>y.RT?1:-1)):
                (x.Mass>y.Mass?1:-1);
        }
    }

    public void BuildDataMap(){
        //int EndScan = RawFile.ScanNumFromRT(EndRT);
        for(int i = 0 ; i >= 0 ; i=RawFile.IndexDir[i]){
            for(int j = 0 ; j < RawFile.RawSpectra[i].Data.Length ; j++) {
                DataMap.Add(RawFile.RawSpectra[i].Data[j]);
            }
        }
        DataMap.Sort(CompMZDatabyIntensity);
    }

    public void BuildMZMap(double Thres){
        //int EndScan = RawFile.ScanNumFromRT(EndRT);
        for(int i = 0 ; i >= 0 ; i=RawFile.IndexDir[i]){
            for(int j = 0 ; j < RawFile.RawSpectra[i].Data.Length ; j++) { 
                if(RawFile.RawSpectra[i].Data[j].Intensity >= Thres) {
                    DataMap.Add(RawFile.RawSpectra[i].Data[j]);
                }
            }
        }
        DataMap.Sort(CompMZDatabyMZ);
    }
}