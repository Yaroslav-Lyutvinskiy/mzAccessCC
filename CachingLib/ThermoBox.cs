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
using System.Text;
using System.IO;
using MSFileReaderLib;

public class RawFileBox : FileBox{
        
    public MSFileReader_XRawfile RawFile;

    public override int LoadIndex(string FileName){

        this.RawFileName = FileName;

        RawFile = new MSFileReader_XRawfile();

        RawFile.Open(FileName);
        RawFile.SetCurrentController(0, 1);

        Spectra = 0;
        RawFile.GetNumSpectra(ref Spectra);

        if( Spectra <= 0) 
            return 0;

	    int i, lastfull = 0, total = 0;
        double TotalEsi = 0.0;

        //fake [0] spectra with no data and fake last spectra with no data 
	    ms2index = new int[Spectra+2];
        IndexDir = new int[Spectra+2];
        IndexRev = new int[Spectra+2];
        RawSpectra = new RawData[Spectra+2];
        for(int j = 0 ; j <= Spectra+1 ; j++){
            RawSpectra[j] = new RawData();
        }

        ESICurrents = new double[Spectra + 2];
        TimeStamps = new double[Spectra+2];
        TimeCoefs = new double[Spectra+2];

        string Filter = null;

        LowRT = 0.0;
        HighRT = 0.0;

        for(i = 1; i <= Spectra; i++){

		    RawFile.GetFilterForScanNum(i, ref Filter);

		    if(Filter.Contains(" Full ") &&  Filter.Contains(" ms ")  && Filter.Contains("FTMS") ) { //is a FULL MS

			    TimeStamps[i] = RawSpectra[lastfull].RT;
                    
                IndexDir[lastfull] = i;
			    IndexRev[i] = lastfull;

			    lastfull = i;
			    ms2index[i] = lastfull;

			    total++;

				RawFile.RTFromScanNum(i, ref RawSpectra[i].RT);
                RawSpectra[i].Filter = Filter;
                TotalRT = RawSpectra[i].RT;

                TimeStamps[i] = RawSpectra[i].RT - TimeStamps[i];

		    } 
		    Filter = null ;
	    }
        IndexDir[lastfull] = IndexDir.Length - 1;
        IndexDir[IndexDir.Length - 1] = -1;
        IndexRev[IndexDir.Length - 1] = lastfull;

        TotalRT = RawSpectra[lastfull].RT;
        AverageTimeStamp = TotalRT/total;

        //пересчитаем временные коэффициэнты 
        for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {
            TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);
            ESICurrents[i] = ESICurrents[i]/(TotalEsi/(double)total);
        }

        TimeCoefs[i] = 1.0;
    //Spectra number 0 has to have RT at the same distance as others
        if(total > 0) {
            double FRT = RawSpectra[IndexDir[0]].RT;
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
            RawSpectra[0].RT = Math.Max(0, FRT - (SRT - FRT));
            FRT = RawSpectra[lastfull].RT;
            SRT = RawSpectra[IndexRev[lastfull]].RT;
            RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
        }else {
            RawSpectra[0].RT = 0.0;
            RawSpectra[1].RT = 0.1;
                
        }
        RawSpectra[0].Data = new MZData[0];
        RawSpectra[IndexDir.Length - 1].Data = new MZData[0];

        return Spectra;
    }

    public override void ReadMS(int Scan){
	    int ArraySize = 0;
        Object MassList = null, EmptyRef=null;
        double temp=0.0;

        try {
            if(StickMode && Scan > 0 ){
                if (RawLabel){
                    (RawFile as IXRawfile2).GetLabelData(ref MassList, ref EmptyRef, ref  Scan);
                    ArraySize = (MassList as Array).GetLength(1); 
                    RawSpectra[Scan].Data = new MZData[ArraySize];
                    for (int k = 0 ; k<ArraySize ; k++ ){
                        RawSpectra[Scan].Data[k] = new MZData();
                        RawSpectra[Scan].Data[k].Mass = (double)(MassList as Array).GetValue(0, k);
                        RawSpectra[Scan].Data[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                    }
                }else{
                    double PeakWidth = 0.0;
                    object PeakFlags = null;
                    string Filter = RawSpectra[Scan].Filter;
                    string MassRange = Filter.Substring(Filter.IndexOf("[")+1,Filter.IndexOf("]")-Filter.IndexOf("[")-1);
                    (RawFile as IXRawfile3).GetMassListRangeFromScanNum(
                        ref Scan, null, 0, 0, 0, 1, ref PeakWidth,ref MassList , ref PeakFlags, MassRange, ref ArraySize);
                    RawSpectra[Scan].Data = new MZData[ArraySize];
                    for (int k = 0 ; k<ArraySize ; k++ ){
                        RawSpectra[Scan].Data[k] = new MZData();
                        RawSpectra[Scan].Data[k].Mass = (double)(MassList as Array).GetValue(0, k);
                        RawSpectra[Scan].Data[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                    }
                }
                return;
            }else{
	            RawFile.GetMassListFromScanNum(ref Scan, null, 
		            0, //type
                    0, //value
                    0, //peaks
                    0, //centeroid
                    ref temp,
		            ref MassList, 
                    ref EmptyRef, 
                    ref ArraySize);
            }

            Buf = new MZData[ArraySize];
            for (int ini = 0; ini < ArraySize; ini++ ) Buf[ini] = new MZData();
            for ( int j = 0 ; j<ArraySize ; j++){
                Buf[j].Mass = (double)(MassList as Array).GetValue(0,j);
                Buf[j].Intensity =  (double)(MassList as Array).GetValue(1,j);
            }
        }
        catch{
            Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan));
            throw e;
        }
			
        MassList = null;
        GC.Collect(2);
        int isCentroided = 0;
        RawFile.IsCentroidScanForScanNum(Scan,ref isCentroided);
        RawSpectra[Scan].Data = Centroid(Buf, ArraySize, isCentroided != 0);
    }

    public override double GetTIC(int Scan){
        int NumPackets=0 , Сhannels = 0 , UniTime = 0 ;
        double StartTime = 0.0, LowMass = 0.0, HighMass = 0.0, Tic = 0.0, BPMass = 0.0, BPInt = 0.0, Freq = 0.0;
        RawFile.GetScanHeaderInfoForScanNum(
            Scan, ref NumPackets, ref StartTime, ref LowMass, ref HighMass, ref Tic, ref BPMass, ref BPInt, ref Сhannels, ref UniTime, ref Freq);
        return Tic;
    }


}
