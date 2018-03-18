// Copyright (c) 2007-2018 Rico Mariani
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;


namespace TestApp
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("SheetDiff file1 file2");
                return;
            }

            Console.WriteLine("diff of '{0}' and '{1}'", args[0], args[1]);

            var xl1 = ReadExcelFile(args[0]);
            var xl2 = ReadExcelFile(args[1]);

            if (xl1 == null || xl2 == null)
                return;

            var v1 = xl1.Values();
            var v2 = xl2.Values();

            var allkeys = new Dictionary<string, int>();

            foreach (var s in v1.Keys)
            {
                allkeys[s] = 1;
            }

            foreach (var s in v2.Keys)
            {
                allkeys[s] = 1;
            }


            var keys = allkeys.Keys.ToList();

            keys.Sort((l, r) =>
            {
                var rl = GetRow(l);
                var rr = GetRow(r);

                if (rl < rr) return -1;
                if (rl > rr) return 1;

                var cl = GetCol(l);
                var cr = GetCol(r);

                if (cl < cr) return -1;
                if (cl > cr) return 1;

                return 0;

            });

            foreach (var key in keys)
            {
                var b1 = v1.ContainsKey(key) && !String.IsNullOrEmpty(v1[key]);
                var b2 = v2.ContainsKey(key) && !String.IsNullOrEmpty(v2[key]);

                if (!b1 && !b2)
                {
                    continue;
                }
                if (b1 && !b2)
                {
                    Console.WriteLine("{0}<{1}", key, v1[key]);
                }
                else if (!b1 && b2)
                {
                    Console.WriteLine("{0}>{1}", key, v2[key]);
                }
                else if (String.CompareOrdinal(v1[key], v2[key]) != 0)
                {
                    Console.WriteLine("{0}<{1}", key, v1[key]);
                    Console.WriteLine("{0}>{1}", key, v2[key]);
                }
            }
        }

        static ExcelReader ReadExcelFile(string file)
        {
            var xl = new ExcelReader();
            var errors = new StringBuilder();

            xl.ImportSheet(file, errors);
            if (errors.Length > 0)
            {
                Console.WriteLine(errors);
                return null;
            }

            return xl;
        }

        static int GetRow(string s)
        {
            int row = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '0' && s[i] <= '9')
                    row = row * 10 + s[i] - '0';
            }

            return row;
        }

        static int GetCol(string s)
        {
            int col = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= 'A' && s[i] <= 'Z')
                    col = col * 27 + s[i] - '@';
            }

            return col;

        }
    }
}