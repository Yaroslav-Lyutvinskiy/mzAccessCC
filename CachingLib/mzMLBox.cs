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
using System.Xml;
using System.Linq;
using System.Text;
using Ionic.Zlib;

class ProfileException : Exception {
    public ProfileException(string Message) : base(Message){
    }
} 

class mzMLBox : FileBox {

    class mzMLSpectrum {
        public int ScanNumber;
        public double RT;
        public int MSOrder;
        public double ParentMass;
        public long Offset;
        public long mzBinaryOffset;
        public long mzBinaryLen;
        public long intBinaryOffset;
        public long intBinaryLen;
        public bool Profile;
        public bool mzBit64;
        public bool mzGziped;
        public bool intBit64;
        public bool intGziped;
        public string ID;
        public double TIC;

        public mzMLSpectrum() { }

        public mzMLSpectrum(int SN, double RT) {
            this.ScanNumber = SN;
            this.RT = RT;
        }

        public class byRT : IComparer<mzMLSpectrum> {
            public int Compare(mzMLSpectrum x, mzMLSpectrum y) {
                if(x.RT > y.RT) { return 1; }
                if(x.RT < y.RT) { return -1; }
                return 0;
            }
        }
        public class bySN : IComparer<mzMLSpectrum> {
            public int Compare(mzMLSpectrum x, mzMLSpectrum y) {
                if(x.ScanNumber > y.ScanNumber) { return 1; }
                if(x.ScanNumber < y.ScanNumber) { return -1; }
                return 0;
            }
        }
    }

    List<mzMLSpectrum> MLSpectra = new List<mzMLSpectrum>();
    XmlTextReader Reader;
    FileStream MLStream;

    double LowestMass = double.PositiveInfinity;
    double HighestMass = 0.0;
    bool AllProfile = true;
    bool AllCentroided = true;

    public override int LoadIndex(string FileName) {
        this.RawFileName = FileName;
        MLStream = File.OpenRead(FileName);
        Reader = new XmlTextReader(FileName);
        Reader.WhitespaceHandling = System.Xml.WhitespaceHandling.None;
        bool Indexed = false;
        int SpectraCount = 0;
        string str = null;
        long IndexOffset = 0;

        //Check for index and Number of spectra 
        while(Reader.Read()) {
            if(Reader.Name == null)
                continue;
            if(Reader.Name == "indexedmzML") {
                Indexed = true;
                break;
            }
            if(Reader.Name == "spectrumList" ) { //break condition
                break;
            }
        }
        //if indexed - trying to read index
        if(Indexed) {
            Byte[] ReadBuffer = new Byte[512];
            MLStream.Seek(-512, SeekOrigin.End);
            MemoryStream MStream = new MemoryStream();
            MLStream.Read(ReadBuffer, 0, 512);
            MStream.Write(ReadBuffer, 0, 512);
            MStream.Position = 0;
            StreamReader MReader = new StreamReader(MStream);
            do { //does it make sence here to use temporary XMLReader?
                str = MReader.ReadLine();
                if(str.Contains("<indexListOffset>")) {
                    str = str.Substring(str.IndexOf(">") + 1);
                    str = str.Substring(0, str.IndexOf("<"));
                    IndexOffset = long.Parse(str);
                    break;
                }
            } while(!MReader.EndOfStream);
            MLStream.Seek(IndexOffset, SeekOrigin.Begin);
            ReadBuffer = new Byte[MLStream.Length - IndexOffset];
            MLStream.Read(ReadBuffer, 0, Convert.ToInt32(MLStream.Length - IndexOffset));
            MStream = new MemoryStream();
            MStream.Write(ReadBuffer, 0, ReadBuffer.Length);
            MStream.Position = 0;
            MReader = new StreamReader(MStream);
            MStream.Position = 0;
            Reader = new XmlTextReader(MStream);
            Reader.Read();
            bool IndexList = false;
            bool SpectrumIndex = false;
            mzMLSpectrum S = null;
            do {
                if(Reader.NodeType == XmlNodeType.Element) {
                    if(Reader.Name == "indexList") {
                        IndexList = true;
                    }
                    if(IndexList && Reader.Name == "index") {
                        if(Reader.GetAttribute("name") == "spectrum") {
                            SpectrumIndex = true;
                        } else {
                            SpectrumIndex = false;
                        }
                    }
                    if(IndexList && SpectrumIndex && Reader.Name == "offset") {
                        S = new mzMLSpectrum();
                        S.ID = Reader.GetAttribute("idRef");
                    }
                }
                if(Reader.NodeType == XmlNodeType.Text && S != null) {
                    S.Offset = long.Parse(Reader.Value);
                }
                if (Reader.NodeType == XmlNodeType.EndElement) {
                    if(S != null) {
                        MLSpectra.Add(S);
                        S = null;
                    }
                    if(Reader.Name == "indexList")
                        break;
                }
            } while(Reader.Read());
            SpectraCount = MLSpectra.Count;
        }

        if(!Indexed) {
            MLStream.Seek(0, SeekOrigin.Begin);
            long Pos = 0;
            while(MLSpectra.Count < SpectraCount) {
                mzMLSpectrum S = new mzMLSpectrum();
                S.Offset = NextTagPosition(MLStream, "<spectrum", Pos);
                Pos = S.Offset;
                if(Pos < 0)
                    throw (new Exception("Inconsistant mzML"));
                MLSpectra.Add(S);
            }
        }

	    ms2index = new int[MLSpectra.Count+2];
        IndexDir = new int[MLSpectra.Count+2];
        IndexRev = new int[MLSpectra.Count+2];
        RawSpectra = new RawData[MLSpectra.Count+2];
        for(int j = 0 ; j <= MLSpectra.Count+1 ; j++){
            RawSpectra[j] = new RawData();
        }
        int lastfull = 0, total = 0;


        MLStream.Seek(0, SeekOrigin.Begin);
        for (int i = 0 ; i < SpectraCount ; i++) {
            // Read chunk of XML
            byte[] Buffer = null; 
            MLStream.Seek(MLSpectra[i].Offset, SeekOrigin.Begin);
            int Len = Convert.ToInt32((i + 1 < SpectraCount) ? MLSpectra[i + 1].Offset - MLSpectra[i].Offset : IndexOffset - MLSpectra[i].Offset);
            Buffer = new byte[Len];
            MLStream.Read(Buffer, 0, Len);
            //Make XML reader of this chunk
            MemoryStream MStream = new MemoryStream();
            MStream.Write(Buffer, 0, Buffer.Length);
            MStream.Position = 0;
            MemoryStream MBStream = new MemoryStream();
            MBStream.Write(Buffer, 0, Buffer.Length);
            MBStream.Position = 0;
            Reader = new XmlTextReader(MStream);
            bool DataArrayFlag = false;
            bool Gzipped = false;
            bool Bit64 = false;
            bool intArray = false;
            bool mzArray = false;
            long arrayOffset = 0;

            while(Reader.Read()) {
                if(Reader.NodeType == XmlNodeType.Element && Reader.Name == "spectrum") {
                    MLSpectra[i].ScanNumber = Convert.ToInt32(Reader.GetAttribute("index"));
                    MLSpectra[i].ID = Reader.GetAttribute("id");
                }
                if(Reader.NodeType == XmlNodeType.Element && Reader.Name == "binaryDataArray") {
                    DataArrayFlag = true;
                    arrayOffset = NextTagPosition(MBStream,"<binary>",arrayOffset);
                }
                if(Reader.NodeType == XmlNodeType.EndElement && Reader.Name == "binaryDataArray") {
                    DataArrayFlag = false;
                    long StartOffset = arrayOffset;
                    arrayOffset = NextTagPosition(MBStream,"</binary>",arrayOffset);
                    if(!intArray && !mzArray)
                        continue;
                    //read array boundaries here 
                    if(intArray) {
                        MLSpectra[i].intGziped = Gzipped;
                        MLSpectra[i].intBit64 = Bit64;
                        MLSpectra[i].intBinaryOffset = MLSpectra[i].Offset + StartOffset + 8;
                        MLSpectra[i].intBinaryLen = arrayOffset - StartOffset - 8;
                        Gzipped = false; Bit64 = false; intArray = false;
                    }
                    if(mzArray) {
                        MLSpectra[i].mzGziped = Gzipped;
                        MLSpectra[i].mzBit64 = Bit64;
                        MLSpectra[i].mzBinaryOffset = MLSpectra[i].Offset + StartOffset + 8;
                        MLSpectra[i].mzBinaryLen = arrayOffset - StartOffset - 8;
                        Gzipped = false; Bit64 = false; mzArray = false;
                    }
                }
                if(Reader.NodeType == XmlNodeType.Element && Reader.Name == "cvParam") {
                    switch(Reader.GetAttribute("accession")) {
                    case "MS:1000579": { //Ms-only spectrum
                            MLSpectra[i].MSOrder = 1;
                            break;
                        }
                    case "MS:1000511": { //MS Level
                            MLSpectra[i].MSOrder = Convert.ToInt32(Reader.GetAttribute("value"));
                            break;
                        }
                    case "MS:1000128": { //Profile scan
                            MLSpectra[i].Profile = true;
                            break;
                        }
                    case "MS:1000127": { //Centroided scan
                            MLSpectra[i].Profile = false;
                            break;
                        }
                    case "MS:1000016": { //Retention Time
                            if(Reader.GetAttribute("unitName") == "minute") {
                                MLSpectra[i].RT = Convert.ToDouble(Reader.GetAttribute("value"));
                                break;
                            }
                            if(Reader.GetAttribute("unitName") == "second") {
                                MLSpectra[i].RT = Convert.ToDouble(Reader.GetAttribute("value"))/60.0;
                                break;
                            }
                            throw (new Exception(String.Format("Inconsistant mzML. Unknown unit \"{0}\" for retention time",Reader.GetAttribute("unitName"))));
                        }
                    case "MS:1000744": { //precusor mass
                            MLSpectra[i].ParentMass = Convert.ToDouble(Reader.GetAttribute("value"));
                            break;
                        }
                    case "MS:1000521": { //32-bit data array 
                            if(DataArrayFlag) {
                                Bit64 = false;
                            }
                            break;
                        }
                    case "MS:1000523": { //64-bit data array 
                            if(DataArrayFlag) {
                                Bit64 = true;
                            }
                            break;
                        }
                    case "MS:1000574": { //zlib compression
                            if(DataArrayFlag) {
                                Gzipped = true;
                            }
                            break;
                        }
                    case "MS:1000576": { //no compression
                            if(DataArrayFlag) {
                                Gzipped = false;
                            }
                            break;
                        }
                    case "MS:1000514": { //mz binary array
                            if(DataArrayFlag) {
                                mzArray = true;
                            }
                            break;
                        }
                    case "MS:1000515": { //intensity binary array
                            if(DataArrayFlag) {
                                intArray = true;
                            }
                            break;
                        }
                    }
                }
                if(Reader.NodeType == XmlNodeType.EndElement && Reader.Name == "spectrum") {
                    if(MLSpectra[i].MSOrder == 1) {
                        AllProfile &= MLSpectra[i].Profile;
                        AllCentroided &= !MLSpectra[i].Profile;
                        if (!AllCentroided) {
                            IndexDir[0] = -1;
                            throw new ProfileException(String.Format("File {0} contains spectra that could not be centroided",FileName));
                        }
                        IndexDir[lastfull] = i+1;
			            IndexRev[i+1] = lastfull;
			            lastfull = i+1;
			            ms2index[i+1] = lastfull;
                        RawSpectra[i+1].RT = MLSpectra[i].RT;
                    }else {
                        MLSpectra[i] = null;
                    }
                    break;
                }
            }
        }

        if (lastfull == 0) {
            IndexDir[0] = -1;
            throw new ProfileException(String.Format("File {0} contains no spectra that could be cached",FileName));
        }
        IndexDir[lastfull] = IndexDir.Length - 1;
        IndexDir[IndexDir.Length - 1] = -1;
        IndexRev[IndexDir.Length - 1] = lastfull;
        TotalRT = RawSpectra[lastfull].RT;
        double FRT = RawSpectra[IndexDir[0]].RT;
        double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
        RawSpectra[0].RT = Math.Max(0, FRT - (SRT - FRT));
        RawSpectra[0].Data = new MZData[0];
        FRT = RawSpectra[lastfull].RT;
        SRT = RawSpectra[IndexRev[lastfull]].RT;
        Spectra = MLSpectra.Count;
        RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
        RawSpectra[Spectra + 1].Data = new MZData[0];

        return MLSpectra.Count;

    }

    public override void ReadMS(int Scan) {
        if (Scan == 0 || Scan > MLSpectra.Count) {
            return;
        }
        mzMLSpectrum S = MLSpectra[Scan-1];
        //!!Get mzMLSpectrum S from Scan

        MLStream.Seek(S.mzBinaryOffset,SeekOrigin.Begin);
        Byte[] mzBuffer = new Byte[S.mzBinaryLen];
        MLStream.Read(mzBuffer, 0, Convert.ToInt32(S.mzBinaryLen));
        //convert base 64 to binary bytes array
        mzBuffer = Convert.FromBase64String(Encoding.UTF8.GetString(mzBuffer));
        //stream will address binary numbers
        Stream mzStream = null;
        MemoryStream mmzStream = new MemoryStream(mzBuffer);
        mmzStream.Position = 0;
        if(S.mzGziped) {
            //it can be gzip stream based on memory stream
            mzStream = new ZlibStream(mmzStream,CompressionMode.Decompress);
        } else {
            //or just a memory stream
            mzStream = mmzStream;
        }

        //Convert base64 string (optionally gzipped) from mzML to binary stream of floating point numbers (for intensity values)
        MLStream.Seek(S.intBinaryOffset,SeekOrigin.Begin);
        Byte[] intBuffer = new Byte[S.intBinaryLen];
        MLStream.Read(intBuffer, 0, Convert.ToInt32(S.intBinaryLen));
        intBuffer = Convert.FromBase64String(Encoding.UTF8.GetString(intBuffer));
        Stream intStream = null;
        MemoryStream mintStream = new MemoryStream(intBuffer);
        mintStream.Position = 0;
        if(S.intGziped) {
            intStream = new ZlibStream(mintStream,CompressionMode.Decompress);
        } else {
            intStream = mintStream;
        }

        List<MZData> Res = new List<MZData>();
        Byte[] buf = new Byte[8];
        int mzLen = S.mzBit64 ? 8 : 4;
        int intLen = S.intBit64 ? 8 : 4;
        while(true) {
            if(mzStream.Read(buf, 0, mzLen) < mzLen) break;
            double MZ = S.mzBit64 ? BitConverter.ToDouble(buf, 0) : BitConverter.ToSingle(buf, 0);
            if(intStream.Read(buf, 0, intLen) < intLen) break;
            double Int = S.intBit64 ? BitConverter.ToDouble(buf, 0) : BitConverter.ToSingle(buf, 0);
            MZData DP = new MZData();
            DP.Mass = MZ;
            DP.Intensity = (float)Int;
            Res.Add(DP);
        }
        RawSpectra[Scan].Data = Res.ToArray();
    }

    public override double GetTIC(int Scan) {
        return 0.0;

    }

    public long NextTagPosition(Stream S, string Tag, long Offset) {
        S.Position = Offset;
        int Counter = 0;
        int OffsetLen = Tag.Length;
        while(Counter<OffsetLen) {
            int b = S.ReadByte();
            if(b < 0)
                return -1;
            char ch = Convert.ToChar(b);
            if(ch == Tag[Counter])
                Counter++;
            else
                Counter = 0;
        }
        return S.Position - Counter;
    }


}
