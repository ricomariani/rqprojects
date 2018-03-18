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
            var reqd = new List<Regex>();
            var anti = new List<Regex>();
            var files = new List<string>();
            bool matchIfNone = false;
            bool matchIfAny = false;

            if (args.Length == 0)
            {
                Console.WriteLine("SheetGrep [+match] [-reject] [--any] [--none] files");
                Console.WriteLine("e.g. SheetGrep --any +fruited *.xlsx  // this will find anyone who's been fruited");
                Console.WriteLine("e.g. SheetGrep --none +fruited *.xlsx  // this will find anyone who's not been fruited");
                Console.WriteLine("e.g. SheetGrep +religion *.xlsx  // print the religion line of the indicated sheets");
                Console.WriteLine("e.g. SheetGrep +magical *.xlsx  // this will print any lines with a cell that has 'magical' in it");
                Console.WriteLine("e.g. SheetGrep +magical -squid *.xlsx  // as above unless it is a magical squid");

                return;
            }

            foreach (var s in args)
            {
                if (s == "--none")
                {
                    matchIfNone = true;
                }
                else if (s == "--any")
                {
                    matchIfAny = true;
                }
                else if (s.StartsWith("+"))
                {
                    reqd.Add(new Regex(s.Substring(1), RegexOptions.IgnoreCase));
                }
                else if (s.StartsWith("-"))
                {
                    anti.Add(new Regex(s.Substring(1), RegexOptions.IgnoreCase));
                }
                else
                {
                    if (s.Contains("*") || s.Contains("?"))
                    {
                        var matches = Directory.GetFiles(".", s);

                        foreach (var match in matches)
                            files.Add(match);
                    }
                    else
                        files.Add(s);
                }
            }

            files.Sort();

            foreach (var file in files)
            {
                var xl = new ExcelReader();
                var errors = new StringBuilder();

                xl.ImportSheet(file, errors);
                if (errors.Length > 0)
                {
                    Console.WriteLine(errors);
                    continue;
                }

                var values = xl.Values();

                var keys = values.Keys.ToList();

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

                var rows = new Dictionary<int, int>();

                foreach (var key in keys)
                {
                    var v = values[key].Replace(",", ";");
                    var row = GetRow(key);

                    // already found row
                    if (rows.ContainsKey(row))
                        continue;

                    bool found = true;

                    foreach (var re in reqd)
                    {
                        if (!re.IsMatch(v))
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        foreach (var re in anti)
                        {
                            if (re.IsMatch(v))
                            {
                                found = false;
                                break;
                            }
                        }
                    }

                    if (found)
                    {
                        rows[row] = 1;

                        if (matchIfAny || matchIfNone)
                            break;
                    }
                }

                if (matchIfNone)
                {
                    if (rows.Count == 0)
                    {
                        Console.WriteLine(file);
                    }
                }
                else if (matchIfAny)
                {
                    if (rows.Count > 0)
                    {
                        Console.WriteLine(file);
                    }

                }
                else
                {
                    int rowLast = -1;
                    var buffer = new StringBuilder();

                    foreach (var key in keys)
                    {
                        var v = values[key].Replace(",", ";");
                        var row = GetRow(key);

                        if (row != rowLast)
                        {
                            if (buffer.Length > 0)
                            {
                                Console.WriteLine(buffer);
                                buffer.Length = 0;
                            }

                            rowLast = row;
                        }

                        // skip rows we did not match
                        if (!rows.ContainsKey(row))
                            continue;

                        if (buffer.Length == 0)
                        {
                            buffer.Append(file);
                            buffer.Append("(");
                            buffer.Append(row);
                            buffer.Append("),");
                            buffer.Append(v);
                        }
                        else
                        {
                            buffer.Append(",");
                            buffer.Append(v);
                        }
                    }

                    if (buffer.Length > 0)
                        Console.WriteLine(buffer);
                }
            }


        }

        static int GetRow(string s)
        {
            int row = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '0' && s[i] <='9')
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