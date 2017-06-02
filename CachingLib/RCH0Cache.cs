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



    public partial class Cashing {

        static public int RCH0Create(string FileName, double DataThres) {
            FileService RawFileService = new FileService(FileName);
            BinaryWriter sw = null;
            string OutFileName = Path.ChangeExtension(FileName, "rch");
            FileStream fs = new FileStream(OutFileName, FileMode.Create, FileAccess.ReadWrite);
            sw = new BinaryWriter(fs);
            //signature
            sw.Write("RCH0");
            RawFileService.BuildMZMap(DataThres);
            //Записать сканы
            //число сканов
            int ScanCount = 1;
            int i;
            for(i = 0 ; RawFileService.RawFile.IndexDir[i] == 0 ; i++)
                ;
            while(RawFileService.RawFile.IndexDir[i] != -1) {
                i = RawFileService.RawFile.IndexDir[i];
                ScanCount++;
            }
            ScanCount--;
            sw.Write(ScanCount);
            //пары [scan,RT]
            for(i = 0 ; RawFileService.RawFile.IndexDir[i] == 0 ; i++)
                ;
            do {
                sw.Write(i);
                sw.Write((float)RawFileService.RawFile.RawSpectra[i].RT);
                i = RawFileService.RawFile.IndexDir[i];
            } while(RawFileService.RawFile.IndexDir[i] != -1);
            //записать индекс масс (64кб на страницу, 16 байт на точку, 4096 точек на страницу)
            //число страниц
            int Len = RawFileService.DataMap.Count;
            if(Len % 4096 == 0) {
                Len = Len / 4096;
            } else {
                Len = Len / 4096 + 1;
            }
            sw.Write(Len);
            //записать индекс масс
            for(i = 0 ; i < Len ; i++) {
                sw.Write(RawFileService.DataMap[i * 4096].Mass);
            }
            //записать данные
            for(i = 0 ; i < RawFileService.DataMap.Count ; i++) {
                sw.Write(RawFileService.DataMap[i].Mass);
                sw.Write((float)RawFileService.DataMap[i].Intensity);
                sw.Write((float)RawFileService.DataMap[i].Scan);
            }
            sw.Close();
            return 0;
        }

        //check for completeness
        static bool RCH0Complete(string FileName) {
            try {
                BinaryReader sr = null;
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                try {
                    sr = new BinaryReader(fs);
                    string sig = sr.ReadString();
                    int SpectraCount = sr.ReadInt32();
                    fs.Seek(SpectraCount * 8, SeekOrigin.Current);
                    int PageCount = sr.ReadInt32();
                    fs.Seek(PageCount * 8, SeekOrigin.Current);
                    long FileLen = fs.Length;
                    if((fs.Length - fs.Position) % 65536 == 0) {
                        return (fs.Length - fs.Position) >> 16 == PageCount;
                    }
                    return (fs.Length - fs.Position) >> 16 == PageCount - 1;
                }
                finally {
                    fs.Close();
                }
            }catch(Exception e) {
                return false;
            }
        }

    }
