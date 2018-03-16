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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;

namespace GameBot
{
    internal partial class Bot
    {
        public struct Entry
        {
            public string name;
            public string type;
            public string desc;

            public Entry(string n, string t, string d)
            {
                name = n;
                type = t;
                desc = d;
            }
        }

        public static Entry[] entries = ReadEntries();

        public static Entry[] ReadEntries()
        {
            var sr = new StreamReader("help.txt");
            var list = new List<Entry>();

            for (;;)
            {
                var name = sr.ReadLine();
                if (name == null) break;
                if (name == "") continue;

                var type = sr.ReadLine();

                var desc = new StringBuilder();

                for (; ; )
                {
                    var line = sr.ReadLine();
                    if (line == null || line == ".")
                        break;

                    desc.AppendLine(line);
                }

                Entry e = new Entry(name, type, desc.ToString());
                list.Add(e);
            }

            sr.Close();

            return list.ToArray();
        }

        Entry getExactHelp(string name, string type)
        {
            foreach (Entry entry in entries)
            {
                if (entry.name == name && entry.type == type)
                    return entry;
            }

            return new Entry(name, type, "No help available.");
        }

        void dumpCommandHelp()
        {
            Entry entry = getExactHelp(currentCmd, "Bot command");
            SendBufferWrapped(String.Format("{0}\n{1}\n", entry.name, entry.desc));
        }

        public static List<Entry> filterHelp(List<string> match, List<string> reject, bool fMatchDesc, bool fMatchType)
        {
            List<Entry> results = new List<Entry>();

            var mLeft = new List<string>();
            var mRight = new List<string>();
            var mMid = new List<string>();

            foreach (var m in match)
            {
                mLeft.Add(m + " ");
                mRight.Add(" " + m);
                mMid.Add(" " + m + " ");
            }

            foreach (Entry entry in entries)
            {
                string val;

                if (fMatchDesc)
                    val = (entry.desc + " " + entry.type + " " + entry.name).ToLower();
                else if (fMatchType)
                    val = (entry.name + " //" + entry.type).ToLower();
                else
                    val = entry.name.ToLower();

                int j;
                for (j = 0; j < match.Count; j++)
                {
                    var m = match[j];

                    if (!fMatchDesc && m.Length >= 2 && m.StartsWith("!"))
                    {
                        // exact match on !cmd
                        if (val != m)
                            break;
                    }

                    // match whole word
                    if (val == m ||
                        val.StartsWith(mLeft[j]) ||
                        val.EndsWith(mRight[j]) ||
                        val.Contains(mMid[j]))
                        continue;

                    break;
                }

                if (j < match.Count)
                {
                    continue;
                }

                for (j = 0; j < reject.Count; j++)
                {
                    if (val.Contains(reject[j]))
                        break;
                }

                if (j < reject.Count)
                {
                    continue;
                }

                results.Add(entry);
            }

            if (results.Count > 1 && fMatchType && match.Count == 2)
            {
                var mainKey = match[0].ToLower();
                foreach (var r in results)
                {
                    if (r.name.ToLower() == mainKey)
                    {
                        results.Clear();
                        results.Add(r);
                        return results;
                    }
                }
            }

            return results;
        }

        void exec_help(string s)
        {
            if (s == null || s.Trim() == "")
            {
                dumpCommandHelp();
                return;
            }

            var pattern = s.ToLower().Trim();

            int choice = pattern.IndexOf(" #");
            if (choice > 0)
            {
                string number = pattern.Substring(choice + 2);
                pattern = pattern.Substring(0, choice);
                choice = -1;
                Int32.TryParse(number, out choice);
            } 
            
            ParseToMap(pattern);

            List<string> match = new List<string>();
            List<string> reject = new List<string>();
            listArgs.Insert(0, "@"); // add dummy item
            getMatchAndReject(match, reject);

            bool fMatchDesc = BoolArg("desc");

            var result = filterHelp(match, reject, fMatchDesc, fMatchType: false);

            StringBuilder b = new StringBuilder();

            if (result.Count == 0)
            {
                SendBufferWrapped("Nothing found");
            }
            else if (result.Count == 1)
            {
                var entry = result[0];
                SendBufferWrapped(String.Format("{0} {1}\n{2}\n", entry.name, entry.type, entry.desc));
            }
            else
            {
                if (choice > 0 && choice <= result.Count)
                {
                    var entry = result[choice-1];
                    SendBufferWrapped(String.Format("{0} {1}\n{2}\n", entry.name, entry.type, entry.desc));
                    return;
                }

                if (result.Count > 100)
                {
                    SendBufferWrapped("Far too many matches, try something more specific.\n");
                }
                else if (result.Count <= 5)
                {
                    for (int i = 0; i < result.Count; i++)
                    {
                        var entry = result[i];
                        string desc = entry.desc;
                        int n = desc.IndexOf('\n');
                        if (n > 0)
                        {
                            desc = desc.Substring(0, n);
                        }

                        SendBufferWrapped(String.Format("#{0,-2} {1} {2}\n    {3}\n", i + 1, entry.name, entry.type, desc));
                        SendBufferWrapped(" \n");
                    }
                }
                else
                {
                    for (int i = 0; i < result.Count; i++)
                    {
                        var entry = result[i];
                        SendBufferWrapped(String.Format("#{0,-2} {1} {2}\n", i + 1, entry.name, entry.type));
                        SendBufferWrapped(" \n");
                    }
                }
            }
        }

        internal List<StringPair> FindEvents(string args)
        {
            ParseToMap(args, s_simpleArgs);

            if (listArgs.Count == 0)
            {
                return null;
            }

            // insert a dummy for the "who" arg that isn't present in this case
            listArgs.Insert(0, "slug");

            List<StringPair> results = findstuff("general", "_event");

            sortEvents(results);
  
            List<string> match = new List<string>();
            List<string> reject = new List<string>();
            getMatchAndReject(match, reject);

            filterResults(results, match, reject, NoteTypeEvent());

            return results;
        }

        internal List<StringPair> FindNotes(string args, NoteMeta n)
        {
            ParseToMap(args, s_simpleArgs);

            if (!GetAtStyleWho(n))
            {
                return new List<StringPair>();
            }

            List<StringPair> results = findstuff(n.who, n.Folder);

            List<string> match = new List<string>();
            List<string> reject = new List<string>();
            getMatchAndReject(match, reject);

            filterResults(results, match, reject, n);

            return results;
        }  
    }
}
