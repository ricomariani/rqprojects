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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace GameHost
{
    internal class Worker
    {
        private TcpClient client;
        private HybridStreamReader sr;
        private NetworkStream stream;

        static private Dictionary<string, Dict> master;
        static private Dictionary<string, DateTime> serverState;
        static private DateTime lastServerDirty;
        static private DateTime lastPartyWrite;
        static private DateTime lastArchiveWrite;

        private Dictionary<string, DateTime> userState = new Dictionary<string, DateTime>();
        public string myroom = "#gameroom";
        public string nick = "gameaid";

        public Worker(TcpClient client, NetworkStream stream)
        {
            this.client = client;
            this.stream = stream;
            sr = new HybridStreamReader(stream);
            master = GameHost.Program.master;
            serverState = GameHost.Program.serverState;
        }

        public Worker()
        {
            this.client = null;
            this.stream = null;
            sr = null;
            master = GameHost.Program.master;
            serverState = GameHost.Program.serverState;
        }

        public void SendToGameaid(String data)
        {
            if (!data.EndsWith("\n"))
                data = data + "\n";

            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            lock (client)
            {
                try
                {
                    // send the data to the remote device.
                    client.Client.Send(byteData);
                }
                catch
                {
                    // disregard send errors
                }
            }
        }

        public void Send(byte[] byteData)
        {
            try
            {
                // send the data to the remote device.
                client.Client.Send(byteData);
            }
            catch
            {
                // disregard send errors
            }
        }

        internal void DoWork()
        {
            string s;

            lock (master)
            {
                if (master.Count == 0)
                    loadparty();
            }

            for (;;)
            {
                try
                {
                    s = sr.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    break;
                }

                if (s == null)
                    break;

                // Console.WriteLine(s);

                if (s.StartsWith("!!"))
                {
                    int i = s.IndexOf('!', 2);
                    if (i <= 3)
                        continue;

                    var command = s.Substring(i);
                    var roller = s.Substring(2, i - 2);
                    GameBot.Bot bot = new GameBot.Bot(roller, myroom);
                    var result = bot.RunCmd(command);
                    cmdresult(result);
                    continue;
                }

                if (s.StartsWith("!"))
                {
                    GameBot.Bot bot = new GameBot.Bot(">"+nick, myroom);
                    var result = bot.RunCmd(s);
                    cmdresult(result);
                    continue;
                }

                string cmd;
                string args;
                Parse2(s, out cmd, out args);

                switch (cmd)
                {
                    case "help":
                        worker_help(args);
                        break;

                    case "room":
                        if (!args.StartsWith("#"))
                            args = "#" + args;

                        args.Replace(" ", "");
                        myroom = args;
                        break;

                    case "join":
                        if (!args.StartsWith("#"))
                            args = "#" + args;

                        args.Replace(" ", "");

                        GameBot.Bot.Send(String.Format("join {0}\n", args));
                        GameBot.Bot.Send(String.Format("samode {0} +o __\n", args));
                        break;

                    case "nick":
                        nick = args;
                        break;

                    case "quit":
                        return;

                    case "library":
                        evallibrary(args);
                        break;

                    case "icons":
                        evalicons(args, myroom);
                        break;

                    case "eval":
                        evaldir(args);
                        break;

                    case "nn":
                        newnote(args);
                        break;

                    case "n":
                        note(args);
                        break;

                    case "ren":
                        ren(args);
                        break;

                    case "del":
                        del(args);
                        break;

                    case "qdir":
                        qdir(args);
                        break;

                    case "dir":
                        dir(args);
                        break;

                    case "save":
                        save(args);
                        break;

                    case "load":
                        load(args);
                        break;

                    case "q":
                        query(args);
                        break;

                    case "i":
                        info(args);
                        break;

                    case "upload":
                        upload(args);
                        break;

                    case "download":
                        download(args);
                        break;

                    case "download-dir":
                        download_dir(args);
                        break;

                    case "remaining":
                        var hours = GameBot.Bot.GetRemainingHours(args);
                        SendToGameaid(String.Format("hours {0}\r\n", hours));
                        break;
                }
            }
        }

        private void download_dir(string args)
        {
            try
            {
                var files = System.IO.Directory.GetFiles("uploads/"+args);

                StringBuilder b = new StringBuilder();

                b.Append("begin dir download-result\r\n");

                int i = 0;
                foreach(var file in files)
                {
                    var name = System.IO.Path.GetFileName(file);
                    b.AppendFormat("v {0} {1} {2}\r\n", "download-result", i++, name);
                }

                b.AppendFormat("v {0} {1} {2}\r\n", "download-result", "folder", args);

                b.Append("end dir download-result\r\n");
                SendToGameaid(b.ToString());
            }
            catch (Exception e)
            {
                StringBuilder b = new StringBuilder();

                b.Append("begin dir download-result\n");
                b.AppendFormat("v {0} {1} {2}\r\n", "download-result", 0, "No files");
                b.Append("end dir download-result\r\n");

                SendToGameaid(b.ToString());

                Console.WriteLine(e);
            }
        }

        private void download(string args)
        {
            string file;
            string password;

            Parse2(args, out file, out password);

            try
            {
                var bytes = System.IO.File.ReadAllBytes("uploads/"+password+"/"+file);

                SendToGameaid(String.Format("download {0} {1}", file, bytes.Length));
                Send(bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void upload(string args)
        {
            string file;
            string password;
            string size;

            Parse3(args, out file, out password, out size);

            int length;

            if (!Int32.TryParse(size, out length))
                return;           

            if (length > 1<<20)
                return;

            var bytes = new byte[length];

            if (!sr.Read(bytes))
                return;

            try
            {
                if (!System.IO.Directory.Exists("uploads/"+password))
                    System.IO.Directory.CreateDirectory("uploads/" + password);

                var f = System.IO.File.Create("uploads/" + password + "/" + file);
                f.Write(bytes, 0, bytes.Length);
                f.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }

        public static void del_key(string a1, string a2)
        {
            if (!master.ContainsKey(a1))
                return;

            lock (master)
            {
                Dict d = master[a1];

                if (!d.ContainsKey(a2))
                    return;

                d.Remove(a2);
            }

            UpdateServerState(a1);
            // Send(String.Format("del {0} {1}", a1, a2));
        }

        private void ren(string args)
        {
            string a1, a2;
            Parse2(args, out a1, out a2);

            if (a1 == "" || a2 == "" || a1 == "/")
                return;

            StringBuilder b = new StringBuilder(); ;

            lock (master)
            {
                if (!master.ContainsKey(a1))
                    return;

                if (master.ContainsKey(a2))
                    return;

                master[a2] = master[a1];
                master.Remove(a1);
                UpdateServerState(a1);
                UpdateServerState(a2);

                b.AppendFormat("ren {0} {1}\r\n", a1, a2);

                a1 = a1 + "/";
                a2 = a2 + "/";

                var keys = KeysWithPrefix(a1);

                foreach (string key1 in keys)
                {
                    string key2 = a2 + key1.Substring(a1.Length);

                    if (master.ContainsKey(key2))
                        master.Remove(key2);

                    master[key2] = master[key1];
                    master.Remove(key1);

                    UpdateServerState(key1);
                    UpdateServerState(key2);
                    b.AppendFormat("ren {0} {1}\r\n", key1, key2);
                }

                a1 = StripLastPathPart(a1);
                UpdateServerState(a1);
                a2 = StripLastPathPart(a2);
                UpdateServerState(a2);
            }

            SendToGameaid(b.ToString());
        }

        private static string StripLastPathPart(string a1)
        {
            if (a1.EndsWith("/"))
                a1 = a1.Substring(0, a1.Length - 1);

            int slash = a1.LastIndexOf('/');

            if (slash >= 0)
                a1 = a1.Substring(0, slash);
            else
                a1 = "/";

            return a1;
        }

        public static void del_folder(string a1)
        {
            if (a1 == "/")
                return;

            StringBuilder b = new StringBuilder();

            lock (master)
            {
                if (!master.ContainsKey(a1))
                    return;

                master.Remove(a1);
                UpdateServerState(a1);

                b.AppendFormat("del {0}\r\n", a1);

                a1 = a1 + "/";

                var keys = KeysWithPrefix(a1);

                foreach (string key in keys)
                {
                    master.Remove(key);
                    UpdateServerState(key);
                    b.AppendFormat("del {0}\r\n", key);
                }

                a1 = StripLastPathPart(a1);
                UpdateServerState(a1);
            }

            // Send(b.ToString());
        }

        private static List<string> KeysWithPrefix(string a1)
        {
            List<string> keys = new List<string>();
            foreach (string key in master.Keys)
                if (key.StartsWith(a1))
                    keys.Add(key);
            return keys;
        }

        private static void del(string args)
        {
            string a1, a2;
            Parse2(args, out a1, out a2);

            if (a2 == "")
            {
                del_folder(a1);
            }
            else
            {
                del_key(a1, a2);
            }
        }

        private void info(string args)
        {
            StringBuilder b = new StringBuilder();

            b.AppendFormat("begin info {0}\n", args);

            lock (master)
            {
                foreach (string k in master.Keys)
                {
                    Dict d = master[k];

                    if (!d.ContainsKey(args))
                        continue;

                    b.AppendFormat("v {0} {1} {2}\r\n", k, args, d[args]);
                }
            }

            b.AppendFormat("end info {0}\n", args);

            SendToGameaid(b.ToString());
        }

        public void loadparty()
        {
            load("party");
        }

        void load(string args)
        {
            StreamReader sr;

            try
            {
                sr = new StreamReader(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            lock (master)
            {
                master.Clear();
                serverState.Clear();
                lastServerDirty = DateTime.Now;
                lastPartyWrite = DateTime.Now;
                lastArchiveWrite = DateTime.Now;
            }

            string s;

            while (null != (s = sr.ReadLine()))
            {
                string cmd;
                string arg;

                Parse2(s, out cmd, out arg);

                // for some older saved maps where this namespace was different
                arg = arg.Replace("GameAid2008_Controls", "GameAid2008");

                // removed the year suffix
                arg = arg.Replace("namespace:GameAid2008", "namespace:GameAid");
                arg = arg.Replace("assembly=GameAid2008", "assembly=GameAid");

                arg = arg.Replace(" Background=\"{x:Null}\"", "");
                arg = arg.Replace(" Tag=\"\"", "");
                arg = arg.Replace(" ToolTip=\"\"", "");
                arg = arg.Replace(" ToolTip=\"{x:Null}\"", "");
                arg = arg.Replace(" Clip=\"{x:Null}\"", "");
                arg = arg.Replace(" LayoutTransform=\"{x:Null}\"", "");
                arg = arg.Replace(" RenderTransform=\"{x:Null}\"", "");
                arg = arg.Replace(" Background=\"{x:Null}\"", "");
                arg = arg.Replace(" Fill=\"{x:Null}\"", "");
                arg = arg.Replace(" Stroke=\"{x:Null}\"", "");
                arg = arg.Replace(" ContextMenu=\"{x:Null}\"", "");

                // change lines that look like "n _who Foo|001" into "n _who Foo|00001"
                if (arg.StartsWith("_"))
                {
                    int idx = arg.IndexOf('|');
                    if (idx > 0 && arg.Length > idx + 5)
                    {
                        if (System.Char.IsDigit(arg[idx+1]) && System.Char.IsDigit(arg[idx+2]) && System.Char.IsDigit(arg[idx+3]))
                        {
                            if (arg[idx+4] == ' ')
                            {
                                // insert two zeros to make the id's of length 5
                                arg = arg.Substring(0, idx) + "|00" + arg.Substring(idx + 1);
                            }
                        }
                    }
                }

                if (cmd == "n")
                    note(arg);
            }

            sr.Close();

            lastPartyWrite = DateTime.Now;
            lastArchiveWrite = DateTime.Now;
        }

        public static void AutoSave()
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (master.Count == 0)
                    return;
            }

            DateTime now = DateTime.Now;

            if (lastServerDirty > lastPartyWrite && now >= lastPartyWrite.AddMinutes(5))
            {
                SaveParty(now);
            }

            if (lastServerDirty > lastArchiveWrite && now >= lastArchiveWrite.AddMinutes(60))
            {
                SaveArchive(now);
            }
        }

        public static void SaveArchive(DateTime now)
        {
            lastArchiveWrite = now;
            string name = String.Format("party_archive_{0:s}", DateTime.Now).Replace(":", "-");
            save(name);
        }

        public static void SaveParty(DateTime now)
        {
            lastPartyWrite = now;
            save("party");
        }

        public static void save(string args)
        {
            var master = GameHost.Program.master;

            StreamWriter sw;

            try
            {
                sw = new StreamWriter(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }

            lock (master)
            {
                foreach (string k in master.Keys)
                {
                    Dict d = master[k];

                    foreach (string k2 in d.Keys)
                    {
                        sw.WriteLine("n {0} {1} {2}", k, k2, d.GetRaw(k2));
                    }
                }
            }

            sw.Close();
        }

        private void query(string args)
        {
            string a1, a2;
            Parse2(args, out a1, out a2);
            string r;

            lock (master)
            {
                if (!master.ContainsKey(a1))
                    return;

                Dict d = master[a1];

                if (!d.ContainsKey(a2))
                    return;

                r = d[a2];
            }

            SendToGameaid(String.Format("v {0} {1} {2}", a1, a2, r));
        }

        private void newnote(string args)
        {
            string a1, a2, a3;
            Parse3(args, out a1, out a2, out a3);

            if (a1.Length == 0 || a2.Length == 0 || a3.Length == 0)
                return;

            int n;
            if (!Int32.TryParse(a2, out n) || n == 0)
            {
                note(args);
                return;
            }

            Dict d = makekey(a1);

            if (d == null)
                return;

            lock (master)
            {
                for (; ; )
                {
                    // check the odd and even version of this number before using it
                    // locked items are odd, unlocked even

                    string k1 = String.Format("{0:000000}", n );
                    string k2 = String.Format("{0:000000}", (n ^ 1));

                    if (!d.ContainsKey(k1) && !d.ContainsKey(k2))
                    {
                        d[k1] = a3;
                        UpdateServerState(a1);
                        return;
                    }

                    n+=2; 
                }
            }
        }

        public static bool existskey(string key)
        {
            lock (master)
            {
                return master.ContainsKey(key);
            }
        }

        private static void note(string args)
        {
            string a1, a2, a3;
            Parse3(args, out a1, out a2, out a3);

            if (a1.Length == 0 || a2.Length == 0 || a3.Length == 0)
                return;

            note3(a1, a2, a3);
        }

        public static void note3(string a1, string a2, string a3)
        {
            Dict d = makekey(a1);

            // unable to create the dictionary
            if (d == null)
                return;

            lock (master)
            {
                if (d.ContainsKey(a2))
                    d.Remove(a2);

                d[a2] = a3;
            }

            UpdateServerState(a1);
        }

        private static Dict makekey(string key)
        {
            makekey_primitive(key);

            lock (master)
            {
                if (master.ContainsKey(key))
                    return master[key];
                else
                    return null;
            }
        }

        private static void makekey_primitive(string key)
        {
            lock (master)
            {
                for (; ; )
                {
                    if (master.ContainsKey(key))
                        return;

                    master[key] = new Dict(key);

                    UpdateServerState(key);

                    int i = key.LastIndexOf('/');
                    if (i <= 0)
                        return;

                    key = key.Substring(0, i);

                    UpdateServerState(key);
                }
            }
        }

        public static void Parse2Ex(string s, out string a1, out string a2, string ch)
        {
            int iCmd = s.IndexOf(ch);

            if (iCmd < 0)
            {
                a1 = s;
                a2 = "";
            }
            else
            {
                a1 = s.Substring(0, iCmd);
                a2 = s.Substring(iCmd + 1);
            }
        }
        
        public static void Parse2(string s, out string a1, out string a2)
        {
            Parse2Ex(s, out a1, out a2, " ");
        }

        public static void Parse3(string s, out string a1, out string a2, out string a3)
        {
            string t;

            Parse2(s, out a1, out t);
            Parse2(t, out a2, out a3);
        }

        public static void Parse3Ex(string s, out string a1, out string a2, out string a3, string ch)
        {
            string t;

            Parse2Ex(s, out a1, out t, ch);
            Parse2Ex(t, out a2, out a3, ch);
        }

        private static void UpdateServerState(string dir)
        {
            lock (master)
            {
                if (serverState.ContainsKey(dir))
                {
                    serverState.Remove(dir);
                }

                lastServerDirty = serverState[dir] = DateTime.Now;
            }
        }

        private void UpdateUserState(string dir, DateTime now)
        {
            if (userState.ContainsKey(dir))
            {
                userState.Remove(dir);
            }
            userState[dir] = now;
        }

        private void qdir(string args)
        {
            bool doIt = false;

            lock (master)
            {
                if (!serverState.ContainsKey(args))
                {
                    doIt = true;
                    serverState[args] = DateTime.Now;
                }
                else if (!userState.ContainsKey(args))
                {
                    doIt = true;
                }
                else
                {
                    DateTime user = userState[args];
                    DateTime server = serverState[args];

                    doIt = (user < server);
                }
            }

            if (doIt)
                dir(args);
        }

        struct WpnInfo
        {
            public string name;
            public int attack;
            public int parry;
            public int sr;
            public int ap;
            public string dmg;

            public int sortOrder;
        };

        private void worker_help(string args)
        {
            string group, skill;
            Parse2Ex(args, out group, out skill, "/");
            skill = skill.Replace("_", " ");

            var cats = new List<string>();

            if (group.EndsWith("_school"))
            {
                cats.Add("shugenja");
            }
            else if (group == "_runemagic" || group == "_one_use")
            {
                cats.Add("runemagic");
            }
            else if (group == "_spells" || group == "_stored_spells" || group == "_others_spells")
            {
                cats.Add("battlemagic");
                cats.Add("sorcery");
                cats.Add("wizardry");
            }
            else
            {
                cats.Add("skill");
                cats.Add("secret");
            }

            foreach (var cat in cats)
            {
                var entries = HttpResponder.FindHelpEntries(skill, cat);

                if (entries.Count > 0)
                {
                    var b = new StringBuilder();
                    foreach (var e in entries)
                    {
                        b.AppendFormat("{0} {1}\n{2}\n", e.name, e.type, e.desc);
                    }

                    cmdresult(b.ToString());
                    break;
                }
            }
        }

        private void dir(string folder)
        {
            StringBuilder b = new StringBuilder();

            b.AppendFormat("begin dir {0}\n", folder);

            lock (master)
            {
                if (serverState.ContainsKey(folder))
                    UpdateUserState(folder, serverState[folder]);
                else
                    UpdateUserState(folder, DateTime.Now);

                if (folder == "/")
                {
                    UpdateUserState(folder, DateTime.Now);

                    foreach (string k in master.Keys)
                    {
                        if (k.IndexOf('/') > 0)
                            continue;

                        b.AppendFormat("v {0} {1} {2}\r\n", folder, k, "<Folder>");
                    }
                }
                else
                {
                    b.AppendFormat("v {0} {1} {2}\r\n", folder, "..", "<Folder>");

                    if (master.ContainsKey(folder))
                    {
                        Dict d = master[folder];

                        foreach (string k in d.Keys)
                        {
                            b.AppendFormat("v {0} {1} {2}\r\n", folder, k, d[k]);
                        }
                    }

                    foreach (string k in master.Keys)
                    {
                        if (!k.StartsWith(folder))
                            continue;

                        if (k.Length < folder.Length + 2)
                            continue;

                        if (k[folder.Length] != '/')
                            continue;

                        string p = k.Substring(folder.Length + 1);

                        if (p.IndexOf('/') > 0)
                            continue;

                        b.AppendFormat("v {0} {1} {2}\r\n", folder, p, "<Folder>");
                    }

                    if (folder == "_who")
                    {
                        AddSyntheticDossierItems(b);
                    }
                }
            }

            b.AppendFormat("end dir {0}\n", folder);

            SendCompressed(b);
        }

        static void AddSyntheticDossierItems(StringBuilder b)
        {
            string folder = "_who";

            Dict d = master[folder];

            foreach (string k in d.Keys)
            {
                string who, id;
                Parse2Ex(k, out who, out id, "|");
                if (id == "")
                    continue;
            }

            var people = getallplayers();

            foreach (string pc in people)
            {
                AddSyntheticForOnePerson(b, folder, pc);
            }
        }

        public static void AddSyntheticForOnePerson(StringBuilder b, string folder, string pc)
        {
            if (master.ContainsKey(pc))
            {
                Dict dict = master[pc];
                string stats = "";

                stats = AppendStat(dict, stats, "STR");
                stats = AppendStat(dict, stats, "CON");
                stats = AppendStat(dict, stats, "SIZ");
                stats = AppendStat(dict, stats, "INT");
                stats = AppendStat(dict, stats, "POW");
                stats = AppendStat(dict, stats, "DEX");
                stats = AppendStat(dict, stats, "APP");

                if (stats != "")
                {
                    b.AppendFormat("v {0} {1} {2}\r\n", folder, pc + "|abilities", stats);
                }
            }

            string miscFolder = pc + "/_misc";
            if (master.ContainsKey(miscFolder))
            {
                Dict miscDict = master[miscFolder];
                EmitOptional(b, miscDict, folder, pc + "|species", "species");
                EmitOptional(b, miscDict, folder, pc + "|religion", "religion");
                EmitOptional(b, miscDict, folder, pc + "|life", "life_points");
                EmitOptional(b, miscDict, folder, pc + "|presence_limit", "vow_presence");
            }

            string playerFolder = "_gameaid/_players";
            if (master.ContainsKey(playerFolder))
            {
                Dict playerDict = master[playerFolder];
                EmitOptional(b, playerDict, folder, pc + "|player", pc);
            }

            string armorFolder = pc + "/_armor";
            if (master.ContainsKey(armorFolder))
            {
                Dict armorDict = master[armorFolder];

                int count = 0;
                int ap = 0;
                int apTotal = 0;
                foreach (var k in armorDict.Keys)
                {
                    var v = armorDict[k];
                    if (Int32.TryParse(v, out ap))
                    {
                        count++;
                        apTotal += ap * 10;
                    }

                }

                if (count > 0)
                {
                    apTotal /= count;
                    b.AppendFormat("v {0} {1} {2}\r\n", folder, pc + "|average_armor", (apTotal / 10).ToString() + "." + ((apTotal % 10).ToString()));
                }
            }
           
            string manaFolder = pc + "/mana";
            if (master.ContainsKey(manaFolder))
            {
                Dict manaDict = master[manaFolder];
                foreach (var k in manaDict.Keys)
                {
                    b.AppendFormat("v {0} {1}|mana|{2} {3}\r\n", folder, pc, k, manaDict[k]);
                }
            }

            List<WpnInfo> wpnList = new List<WpnInfo>();

            string wpnPrefix = pc + "/_wpn/";
            foreach (var wpnFolder in master.Keys)
            {
                if (!wpnFolder.StartsWith(wpnPrefix))
                    continue;

                Dict wpnDict = master[wpnFolder];

                WpnInfo w = new WpnInfo();
                w.attack = 0;
                w.parry = 0;
                w.name = wpnFolder.Substring(wpnPrefix.Length);
                w.ap = 0;
                w.sr = 10;
                w.dmg = "";

                if (wpnDict.ContainsKey("attack"))
                {
                    Int32.TryParse(wpnDict["attack"], out w.attack);
                }

                if (wpnDict.ContainsKey("parry"))
                {
                    Int32.TryParse(wpnDict["parry"], out w.parry);
                }

                if (wpnDict.ContainsKey("dmg"))
                {
                    w.dmg = wpnDict["dmg"];
                }

                if (wpnDict.ContainsKey("ap"))
                {
                    Int32.TryParse(wpnDict["ap"], out w.ap);
                }

                if (wpnDict.ContainsKey("sr"))
                {
                    Int32.TryParse(wpnDict["sr"], out w.sr);
                }

                w.sortOrder = Math.Max(w.attack, w.parry) * 10 + w.attack + w.parry;

                wpnList.Add(w);
            }

            wpnList.Sort(
                (WpnInfo w1, WpnInfo w2) =>
                {
                    if (w1.sortOrder > w2.sortOrder)
                        return -1;

                    if (w1.sortOrder < w2.sortOrder)
                        return 1;

                    return 0;
                });

            int c = Math.Min(wpnList.Count, 9);

            for (int i = 0; i < c; i++)
            {
                WpnInfo w = wpnList[i];
                b.AppendFormat("v {0} {1}|combat{2} {3} attack:{4} parry:{5} sr:{6} ap:{7} dmg:{8}\r\n", folder, pc, i + 1, w.name.Replace("_", " "), w.attack, w.parry, w.sr, w.ap, w.dmg);
            }

            string agilityFolder = pc + "/agility";
            if (master.ContainsKey(agilityFolder))
            {
                Dict agilityDict = master[agilityFolder];
                EmitOptional(b, agilityDict, folder, pc + "|skill|dodge", "dodge");
            }

            string stealthFolder = pc + "/stealth";
            if (master.ContainsKey(stealthFolder))
            {
                Dict stealthDict = master[stealthFolder];
                EmitOptional(b, stealthDict, folder, pc + "|skill|hide", "hide");
                EmitOptional(b, stealthDict, folder, pc + "|skill|sneak", "sneak");
            }
        }

        private static string AppendStat(Dict dict, string stats, string stat)
        {
            if (dict.ContainsKey(stat))
            {
                if (stats != "")
                    stats += " ";

                stats += stat + ": " + dict[stat];
            }

            return stats;
        }

        static void EmitOptional(StringBuilder b, Dict miscDict, string folder, string logicalKey, string key)
        {
            if (miscDict.ContainsKey(key))
            {
                b.AppendFormat("v {0} {1} {2}\r\n", folder, logicalKey, miscDict[key].Replace("_", " "));
            }
        }

        void cmdresult(string result)
        {
            StringBuilder b = new StringBuilder();

            string args = "command-result";

            b.AppendFormat("begin eval {0}\n", args);

            var ascii = Encoding.ASCII.GetBytes(result);
            var ms = new MemoryStream(ascii);
            var sr = new StreamReader(ms);

            string line;

            int i = 0;
            while ((line = sr.ReadLine()) != null)
            {
                string number = i.ToString();
                b.AppendFormat("v {0} {1} {2}\r\n", args, number, line);
                i++;
            }

            b.AppendFormat("end eval {0}\n", args);

            SendCompressed(b);
        }

        private void evaldir(string args)
        {
            StringBuilder b = new StringBuilder();

            b.AppendFormat("begin eval {0}\n", args);

            lock (master)
            {
                if (master.ContainsKey(args))
                {
                    Dict d = master[args];

                    foreach (string k in d.Keys)
                    {
                        if (k.StartsWith("\\"))
                        {
                            var slash = k.LastIndexOf("\\");
                            var dir = k.Substring(0, slash);

                            if (dir == "")
                            {
                                continue;
                            }
                                
                            dir = dir.Substring(1).Replace('\\', '/');
                            var key = k.Substring(slash + 1);

                            if (dir == "" || key == "" || !master.ContainsKey(dir))
                            {
                                continue;
                            }

                            Dict d2 = master[dir];

                            if (d2.ContainsKey(key))
                            {
                                b.AppendFormat("v {0} {1} {2}\r\n", args, k, d2[key]);
                            }
                        }
                        else 
                        {
                            b.AppendFormat("v {0} {1} {2}\r\n", args, k, d[k]);
                        }
                    }
                }
            }

            b.AppendFormat("end eval {0}\n", args);

            SendCompressed(b);
        }

        private void evalicons(string args, string origin)
        {
            StringBuilder b = new StringBuilder();

            b.AppendFormat("begin eval {0}\n", args);
            b.AppendFormat("v {0} {1} {2}\r\n", args, "purpose", "icons");

            find_party_icons(args, b, origin);

            b.AppendFormat("end eval {0}\n", args);

            SendCompressed(b);
        }

        List<string> filterLibrary(List<string> match, List<string> reject)
        {
            List<string> results = new List<string>();

            using (var sr = new StreamReader("library.txt"))
            {
                for (; ; )
                {
                    string raw = sr.ReadLine();
                    if (raw == null)
                        break;

                    var val = raw.ToLower();

                    int j;
                    for (j = 0; j < match.Count; j++)
                    {
                        var m = match[j];

                        if (!val.Contains(m))
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

                    results.Add(raw);
                }
            }

            return results;
        }

        void evallibrary(string s)
        {
            string args = "library-search";
            StringBuilder b = new StringBuilder();

            b.AppendFormat("begin eval {0}\n", args);
            b.AppendFormat("v {0} {1} {2}\r\n", args, "purpose", args);

            var pattern = s.ToLower().Trim();

            List<string> match = new List<string>();
            List<string> reject = new List<string>();

            string car;
            string ctr = pattern;

            while (ctr.Length > 0)
            {
                Parse2(ctr, out car, out ctr);
                if (car.StartsWith("-"))
                    reject.Add(car.Substring(1));
                else
                    match.Add(car);
            }

            var result = filterLibrary(match, reject);

            int count = Math.Min(1000, result.Count);

            for (int i = 0; i < count; i++)
            {
                b.AppendFormat("v {0} {1} {2}\r\n", args, i.ToString(), result[i]);
            }

            b.AppendFormat("end eval {0}\n", args);

            SendCompressed(b);
        }

        public static string readkey(string dir, string logicalKey)
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey(dir))
                    return null;

                Dict d = master[dir];

                if (!d.ContainsKey(logicalKey))
                    return null;

                return d[logicalKey];
            }
        }

        public static string readkeyRaw(string dir, string logicalKey)
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey(dir))
                    return null;

                Dict d = master[dir];

                if (!d.ContainsKey(logicalKey))
                    return null;

                return d.GetRaw(logicalKey);
            }
        }

        public static List<string> readallkeys(string dir)
        {
            var master = GameHost.Program.master;

            var result = new List<string>();

            lock (master)
            {
                if (!master.ContainsKey(dir))
                    return result;

                Dict d = master[dir];

                foreach (var k in d.Keys)
                {
                    result.Add(k);
                }
            }

            return result;
        }

        public static List<string> readallsubkeys(string dir)
        {
            var master = GameHost.Program.master;

            var result = new List<string>();

            string prefix = dir + "/";

            lock (master)
            {
                foreach (var k in master.Keys)
                {
                    if (!k.StartsWith(prefix))
                        continue;

                    if (k.IndexOf('/', prefix.Length) > 0)
                        continue;

                    result.Add(k);
                }
            }

            result.Sort();

            return result;
        }

        static internal string OriginToPartyKey(string origin)
        {
            if (origin.StartsWith("#") || origin.StartsWith("@"))
                return origin;
            else
                return "@" + origin;
        }

        public static string findpartykey(string origin)
        {
            var party = readkey("_party", OriginToPartyKey(origin));

            if (party == null || party == "")
                party = "none";

            return party;
        }

        public static List<string> getallplayers()
        {
            List<string> list = new List<string>();

            lock (master)
            {
                foreach (string dir in master.Keys)
                {
                    // only root directories
                    if (dir.IndexOf('/') >= 0)
                        continue;

                    Dict d = master[dir];

                    if (d.ContainsKey("STR") &&
                        d.ContainsKey("INT") &&
                        d.ContainsKey("DEX") &&
                        d.ContainsKey("APP") &&
                        d.ContainsKey("POW") &&
                        d.ContainsKey("SIZ") &&
                        d.ContainsKey("CON"))
                        list.Add(dir);
                }
            }

            return list;
        }
        
        public static List<string> getabsent(string origin)
        {
            var party = findpartykey(origin);

            List<string> list = new List<string>();

            lock (master)
            {
                if (!master.ContainsKey(party))
                    return list;

                Dict d = master[party];

                foreach (string k in d.Keys)
                {
                    if (!d[k].StartsWith("y"))
                        list.Add(k);
                }
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static List<string> getpresent(string origin)
        {
            var party = findpartykey(origin);

            List<string> list = new List<string>();

            lock (master)
            {
                if (!master.ContainsKey(party))
                    return list;

                Dict d = master[party];

                foreach (string k in d.Keys)
                {
                    // ignore special keys like "__date"
                    if (k.StartsWith("__"))
                        continue;

                    if (d[k].StartsWith("y"))
                        list.Add(k);
                }
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static void find_party_icons(string args, StringBuilder b, string origin)
        {
            var tooltipEx = new Regex("ToolTip=\"([-0-9A-Za-z ,_]+)\"");
            var marginEx = new Regex("(Margin=\"[0-9., ]+\")");
            var transEx = new Regex("(\\<Border\\.LayoutTransform\\>.*\\<\\/Border\\.LayoutTransform\\>)");
           
            var players = getpresent(origin);

            List<string> p2 = new List<string>();

            foreach (var p in players)
                p2.Add(p.Replace(" ", "_"));

            players = p2;

            lock (master)
            {
                var results = new Dictionary<string, string>();

                foreach (var k in master.Keys)
                {
                    Dict d = master[k];

                    if (!k.StartsWith("_maps/"))
                    {
                        continue;
                    }

                    if (k == "_maps/MapParts" || k == "_maps/Avatars")
                    {
                        continue;
                    }

                    foreach (var dk in d.Keys)
                    {
                        // must have a tooltip and an image and be a border
                        var v = d[dk];

                        if (!v.StartsWith("<Border"))
                        {
                            continue;
                        }

                        if (!v.Contains("ImageSource") && !v.Contains("Image Source="))
                        {
                            continue;
                        }

                        // if there is a transform remove it
                        var transformMatch = transEx.Match(v);
                        if (transformMatch.Success)
                        {
                            int index = transformMatch.Groups[1].Index;
                            int length = transformMatch.Groups[1].Length;

                            v = v.Substring(0, index) + v.Substring(index + length);
                        }

                        var tooltipMatch = tooltipEx.Match(v);
                        if (!tooltipMatch.Success)
                        {
                            continue;
                        }

                        var member = tooltipMatch.Groups[1].ToString().Trim();
                        if (member.Length == 1)
                        {
                            continue;
                        }

                        member = member.Replace(" ", "_");

                        // must have a margin
                        var marginMatch = marginEx.Match(v);
                        if (marginMatch.Success)
                        {
                            int index = marginMatch.Groups[1].Index;
                            int length = marginMatch.Groups[1].Length;
                            v = v.Substring(0, index) + v.Substring(index + length);

                            v = v.Replace(" LayoutTransform=\"{x:Null}\"", "");
                            v = v.Replace(" Tag=\"\"", "");
                            v = v.Replace(" Opacity=\"1\"", "");
                            v = v.Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", "");
                            v = v.Replace("  ", " ");
                            v = v.Replace("  ", " ");

                            foreach (string p in players)
                            {
                                if ((member.Length >= 4 && p.Contains(member)) || (member.Length < 4 && p == member))
                                {
                                    if (!results.ContainsKey(v))
                                    {
                                        results[v] = p + "_from_" + k.Replace(" ", "_").Replace("/", "_");
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                foreach (string r in results.Keys)
                {
                    b.AppendFormat("v {0} {1} {2}\r\n", args, results[r], r);
                }
            }
        }

        private void SendCompressed(StringBuilder b)
        {
            var ms = new MemoryStream();

            var df = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true);
            var ascii = Encoding.ASCII.GetBytes(b.ToString());
            df.Write(ascii, 0, ascii.Length);
            df.Close();
            ms.Position = 0;
            byte[] bytes = new byte[ms.Length];
            ms.Read(bytes, 0, bytes.Length);

            SendToGameaid(String.Format("compressed {0} {1}", bytes.Length, ascii.Length));
            Send(bytes);

            ms.Close();
        }
    }

    class HybridStreamReader
    {
        Stream s;
        byte[] buffer = new byte[8192];
        int cb;
        int ib;

        public HybridStreamReader(Stream s)
        {
            this.s = s;
            ib = 0;
            cb = 0;
        }

        public string ReadLine()
        {
            string line = "";

            try
            {
                for (; ; )
                {
                    int ibStart = ib;
                    while (ib < cb)
                    {
                        if (buffer[ib] == '\n')
                            break;

                        ib++;
                    }

                    if (ib >= 1 && buffer[ib - 1] == (byte)'\r')
                        line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart - 1);
                    else
                        line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart);

                    if (ib < cb)
                    {
                        ib++;
                        return line;
                    }

                    cb = s.Read(buffer, 0, buffer.Length);
                    ib = 0;

                    if (cb == 0)
                        return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public bool Read(byte[] target)
        {
            int offset = 0;
            int len = target.Length;

            try
            {
                for (; ; )
                {
                    if (cb - ib >= len)
                    {
                        Array.Copy(buffer, ib, target, offset, len);
                        ib += len;
                        return true;
                    }

                    if (cb - ib > 0)
                    {
                        Array.Copy(buffer, ib, target, offset, cb - ib);
                        len -= cb - ib;
                        offset += cb - ib;
                    }

                    cb = s.Read(buffer, 0, buffer.Length);
                    ib = 0;

                    if (cb == 0)
                        return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}