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
using System.Threading.Tasks;
using System.IO;

namespace CachingConsole {
    class Program {

        static void Log(string Message) {
            Console.Write(Message);
        }

        static void Main(string[] args) {
            //path is to be provided 
            int Files = 0;
            int Folders = 0;
            Cashing.Log = Log;
            Cashing.TraverseTreePreview(args[0], ref Files, ref Folders);
            int Counter;
            if (args.Length > 1) {
                Counter = Convert.ToInt32(args[1]);
            } else {
                Counter = 10;
            }
            do {
                Cashing.TraverseTree(args[0], false);
                Counter--;
            } while(Cashing.Success == false && Counter > 0);
        }
    }
}
