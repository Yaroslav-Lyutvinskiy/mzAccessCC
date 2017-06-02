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
using Agilent.MassSpectrometry.DataAnalysis;

    public class AgilentFileBox : FileBox{
        
        public MassSpecDataReader RawFile; 
        private IMsdrDataReader MSReader;
        private IMsdrPeakFilter PeakFilter;

        bool HasProfileData;

        public override int LoadIndex(string FileName){

            this.RawFileName = FileName;



            RawFile = new MassSpecDataReader();
            MSReader = RawFile;

            MSReader.OpenDataFile(FileName);

            HasProfileData = File.Exists(FileName+Path.DirectorySeparatorChar+"AcqData"+Path.DirectorySeparatorChar+"MSProfile.bin");
            if (StickMode) HasProfileData = false;

            Spectra = (int)(MSReader.MSScanFileInformation.TotalScansPresent);

            if( Spectra <= 0) 
                return 0;

	        int i, lastfull = 0, total = 0;
            double TotalEsi = 0.0;

	        ms2index = new int[Spectra+2];
            IndexDir = new int[Spectra+2];
            IndexRev = new int[Spectra+2]; 
            RawSpectra = new RawData[Spectra+2];
            for(int j = 0 ; j <= Spectra+1 ; j++){
                RawSpectra[j] = new RawData();
            }
            Buf = new MZData[500000];
            for (i = 0; i < 500000; i++) Buf[i] = new MZData();
            ESICurrents = new double[Spectra+2];
            TimeStamps = new double[Spectra+2];
            TimeCoefs = new double[Spectra+2];

            LowRT = 0.0;
            HighRT = 0.0;

            for(i = 1; i <= Spectra; i++){

                IMSScanRecord ScanRecord =  MSReader.GetScanRecord(i-1);

		        //YL - для спектров ms-only
		        if(ScanRecord.MSScanType == MSScanType.Scan && ScanRecord.MSLevel == MSLevel.MS && ScanRecord.CollisionEnergy == 0.0) { //is a FULL MS

			        TimeStamps[i] = RawSpectra[lastfull].RT;
                    
                    IndexDir[lastfull] = i;
			        IndexRev[i] = lastfull;

			        lastfull = i;
			        ms2index[i] = lastfull;

			        ++total;

                    RawSpectra[i].RT = ScanRecord.RetentionTime;

                    TotalRT = RawSpectra[i].RT;

                    TimeStamps[i] = RawSpectra[i].RT - TimeStamps[i];

		        }  else {
			        ms2index[i] = lastfull;
		        }
	        }
            IndexDir[lastfull] = Spectra +1;
            IndexDir[Spectra +1] = -1;
            IndexRev[Spectra + 1] = lastfull;


            //IndexDir[lastfull] = -1;
            TotalRT = RawSpectra[lastfull].RT;
            AverageTimeStamp = TotalRT/total;

            //пересчитаем временные коэффициэнты 
            for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {

                TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);

                ESICurrents[i] = ESICurrents[i]/(TotalEsi/(double)total);
            }
            TimeCoefs[i] = 1.0;

            //Spectra number 0 has to have RT at the same distance as others
            double FRT = RawSpectra[IndexDir[0]].RT;
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
            RawSpectra[0].RT=Math.Max(0,FRT-(SRT-FRT));
            FRT = RawSpectra[lastfull].RT;
            SRT = RawSpectra[IndexRev[lastfull]].RT;
            //FRT = RawSpectra[IndexRev[lastfull]].RT;
            //SRT = RawSpectra[IndexRev[IndexRev[lastfull]]].RT;
            RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
            RawSpectra[0].Data = new MZData[0];
            RawSpectra[Spectra + 1].Data = new MZData[0];

            PeakFilter = new MsdrPeakFilter();
            PeakFilter.AbsoluteThreshold = 5.0;
            
            return Spectra;
        }

        public override void ReadMS(int Scan){

	        int ArraySize = 0;
            IBDASpecData SpecData;
            try {
                if(!HasProfileData || StickMode){
                    SpecData = MSReader.GetSpectrum(Scan-1,PeakFilter,PeakFilter,DesiredMSStorageType.Peak);
                    RawSpectra[Scan].Data = new MZData[SpecData.TotalDataPoints];
                    for (int k = 0 ; k<SpecData.TotalDataPoints ; k++ ){
                        RawSpectra[Scan].Data[k] = new MZData();
                        RawSpectra[Scan].Data[k].Mass = SpecData.XArray[k];
                        RawSpectra[Scan].Data[k].Intensity = SpecData.YArray[k];
                    }
                    return;
                }else{
                    SpecData = MSReader.GetSpectrum(Scan-1,null,null,/*PeakFilter,PeakFilter,*/DesiredMSStorageType.Profile);
                    if (Buf.GetLength(0) < SpecData.TotalDataPoints) { 
                        Buf = new MZData[SpecData.TotalDataPoints + 100];
                        for (int i = 0 ; i<SpecData.TotalDataPoints + 100 ; i++)
                            Buf[i] = new MZData();
                    }
                    ArraySize = SpecData.TotalDataPoints;
                    for ( int j = 0 ; j<ArraySize ; j++){
                        Buf[j].Mass = SpecData.XArray[j];
                        Buf[j].Intensity =  SpecData.YArray[j];
                    }
                }
            }
            catch{
                Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan-1));
                throw e;
            }
			
            //RawSpectra[Scan].Data = new MZData[ArraySize];

            GC.Collect(2);

            //if (Settings.Default.Centroids){
            //    RawSpectra[Scan].Data = PeakDetect(Buf);
            //}else{
            //    RawSpectra[Scan].Data = Centroid(Buf, ArraySize);
            //}
            RawSpectra[Scan].Data = Centroid(Buf, ArraySize, !HasProfileData);
        }

        public override double GetTIC(int Scan){
            IMSScanRecord ScanRecord =  MSReader.GetScanRecord(Scan);
            return ScanRecord.Tic;
        }


    }

