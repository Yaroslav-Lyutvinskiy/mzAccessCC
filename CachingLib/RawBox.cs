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

    public class MZData {
        public double Mass;
        public double Intensity;
        public int Scan;
        public bool Counted = false;
        public object Group = null;
        //public int Scan{
        //    get{
        //        return RawFile.RawSpectra[SpectraIndex].Scan;
        //    }
        //}
        public double RT{
            get{
                return RawFile.RawSpectra[Scan].RT;
            }
        }

        static FileBox RawFile;
        public static void SetRawFile(FileBox FB){
            RawFile = FB;
        }

        public static MZData CreateZero(int Scan){
            MZData Res = new MZData();
            Res.Mass = 0.0;
            Res.Intensity = 0.0;
            Res.Scan = Scan;
            return Res;
        }

    }

    public class RawData {
        public MZData[] Data;
        public double RT;
        public int Scan;
        //для MSX спектров
        public string Filter = "";

        public double MZPlusPPM(double MZ, double ppm) {
            return MZ * ((1000000.0 + ppm) / 1000000.0);
        }
    }



    public abstract class FileBox{
        public RawData[] RawSpectra; 
        public int Spectra; 
        public string RawFileName;

        public int[] ms2index; //для каждого спектра дает номер скана последнего full-MS спектра
        //заполнены только сканов соответствующих full-MS спектрам
        public int[] IndexDir; //указывает на номер скана следующего full-MS спектра
        public int[] IndexRev; //указывает на номер скана предидущего full-MS спектра

        public bool RTCorrection;

        protected MZData[] Buf;
        protected double LowRT;
        protected double HighRT;
        protected double TotalRT;
        public bool StickMode;
        public bool RawLabel;
        public int Mode; //+1 - positive, -1 - negative

        //поменять на делегата
        public delegate void Progress(int Perc);
        public static Progress RepProgress;

        public FileBox(){
            StickMode = true;
            RawLabel = true;
        }

        public int ScanNumFromRT(double RT){
            for( int i = 0 ; i < RawSpectra.GetLength(0) ; i++){
                if (RawSpectra[i].RT >= RT) return i; 
            }
            return RawSpectra.Length-1;
        }

        public void LoadInterval(double MinRT, double MaxRT)
        {
            int Index = 0;
            //по границам плюс один спектр 
            while (RawSpectra[IndexDir[Index]].RT<MinRT){
                RawSpectra[Index].Data = null;
                Index = IndexDir[Index];
            }
            while (RawSpectra[IndexRev[Index]].RT<MaxRT){
                if(IndexDir[Index] == -1) {
                    break;
                }
                if (RawSpectra[Index].Data == null) {
                    ReadMS(Index);
                    for (int i = 0 ; i < RawSpectra[Index].Data.Length ; i++){
                        if (RawSpectra[Index].Data[i] != null) {
                            RawSpectra[Index].Data[i].Scan = Index;
                        }
                        RawSpectra[Index].Scan = Index;
                    }
                }
                Index = IndexDir[Index];
            }
            //while (RawSpectra[Index].RT<TotalRT){
            //    RawSpectra[Index].Data = null;
            //    if (IndexDir[Index] == -1) break;
            //    Index = IndexDir[Index];
            //}
            LowRT = MinRT;
            HighRT = MaxRT;
        }

        public MZData[] Centroid(MZData[] Data, int Len, bool StickMode /* former "in" */)
        {
	        int total = 0, u;
	        int o = 0, i = 0, count = Len;
	        double sumIi, sumI, last = 0.0;
            double du = 0.0;
	        bool goingdown = false;
            MZData[] OutData;

            if (StickMode) {
                //считаем пока не начнутся нули или пока следующий не станет меньше помассе 
                for ( i = 1 ; i<count ; i++){
                    if (Data[i].Mass < Data[i-1].Mass || Data[i].Mass == 0){
                        break;
                    }
                }
                OutData = new MZData[i];
                count = i;
                for (i=0; i<count ; i++){
                    OutData[i] = new MZData();
                    OutData[i].Intensity = Data[i].Intensity;
                    OutData[i].Mass = Data[i].Mass;
                }
                return OutData;
            }


            //пропуск начальных нулей
	        while(i < count && Data[i].Intensity == 0.0) ++i;

	        //считает области больше нуля 
	        while(i < count)
	        {
		        while(i < count && Data[i].Intensity != 0.0)
		        {
			        if(last > Data[i].Intensity) {
                        goingdown = true;
                    }else{
                        if(goingdown) {
				            ++total;
				            goingdown = false;
    			        }
                    }

			        last = Data[i].Intensity;
			        ++i;
		        }

		        last = 0.0;
		        goingdown = false;

		        while(i < count && Data[i].Intensity == 0.0) 
                    i++;

		        total++;
	        }

	        //запасает память на подсчитанные области 
	        OutData = new MZData[total];
            for (i = 0; i < total; i++) OutData[i] = new MZData();
	        i = 0; o = 0; total = 0; last = 0.0; goingdown = false;

	        while(i < count && Data[i].Intensity == 0.0) i++;

	        while(i < count)
	        {
		        sumIi = sumI = 0.0;
		        o = i -1;
		        while(i < count && Data[i].Intensity != 0.0){

			        //если пошло на спад
			        if(last > Data[i].Intensity) {
                        goingdown = true;
                    }else{
                        if(goingdown) {
				            u = Convert.ToInt32((sumIi / sumI));
				            OutData[total].Intensity = sumI;
				            OutData[total].Mass = Data[o+u].Mass;
				            ++total;

				            sumIi = sumI = 0.0;
				            o = i -1;
				            goingdown = false;
    			        }
                    }

			        sumIi += Data[i].Intensity*(i-o);
			        sumI += Data[i].Intensity;

			        last = Data[i].Intensity;
			        i++;
		        }

		        u = Convert.ToInt32((sumIi / sumI) /*+0.5*/ );
                du = sumIi / sumI - (double)u;
		        //интенсивность по интегралу 
		        OutData[total].Intensity = sumI;
		        //сентроид - по апексу 
		        //OutData[total].Mass = Data[o+u].Mass;
                //центроид по центру
                OutData[total].Mass = Data[o+u].Mass*(1-du) + Data[o+u+1].Mass*du;

		        last = 0.0;
		        goingdown = false;

		        while(i < count && Data[i].Intensity == 0.0) i++;

                //if (OutData[total].Intensity > 3.0) 
                    total++;
	        }
            return OutData;
        }

        //data format dependent
        abstract public void ReadMS(int Scan);
        abstract public int LoadIndex(string FileName);
        abstract public double GetTIC(int Scan);
    }

