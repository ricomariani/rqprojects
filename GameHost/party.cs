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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using GameHost;
using System.Text.RegularExpressions;

namespace GameBot
{
    partial class Bot
    {
        // routine... 
        public void routine_cmd(string routine)
        {
            var master = GameHost.Program.master;

            string key = roller + "/" + "_routines";
            string tasks;

            lock (master)
            {
                if (!master.ContainsKey(key))
                    return;

                Dict dict = master[key];

                if (!dict.ContainsKey(routine))
                    return;

                tasks = dict[routine];
            }

            foreach (string s in tasks.Split(comma))
            {
                string task = s.Trim();
                task_cmd(task);
            }
        }

        public void display_tasks()
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey("_tasks"))
                {
                    sbOut.Append("No tasks have been set in the _tasks folder\n");
                    return;
                }

                Dict dict = master["_tasks"];

                foreach (string k in dict.Keys)
                {
                    sbOut.AppendFormat("{0}: {1}\n", k, dict[k]);
                }
            }
        }

        public void display_weapons(string who)
        {
            var master = GameHost.Program.master;
            var prefix = who + "/_wpn/";
            int len = prefix.Length;

            sbOut.Append("Weapon choices for " + who + "\n");
            lock (master)
            {
                foreach (string k in master.Keys)
                {
                    if (k.StartsWith(prefix))
                    {
                        sbOut.AppendFormat("{0}: {1}\n", who, k.Substring(len));
                    }
                }
            }
        }
        
        public void camp_set_action(string who, string what)
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey(who))
                {
                    sbOut.AppendFormat("{0} does not exist\n", who);
                    return;
                }

                if (!master.ContainsKey("_tasks"))
                {
                    sbOut.AppendFormat("No tasks are defined\n", who);
                    return;
                }

                Dict d = master["_tasks"];
                if (!d.ContainsKey(what))
                {
                    sbOut.AppendFormat("{0} is not a valid task use '!camp tasks' to see the list\n", what);
                    return;
                }
            }

            sbOut.AppendFormat("{0} will now {1}.\n", who, what);
            Worker.note3(who + "/_routines", "camp", what);
            camp_update();
        }

        public void camp_set_weapon(string who, string what)
        {
            var master = GameHost.Program.master;

            var key = who + "/_wpn/" + what;

            lock (master)
            {
                if (!master.ContainsKey(who))
                {
                    sbOut.AppendFormat("{0} does not exist\n", who);
                    return;
                }

                if (!master.ContainsKey(key))
                {
                    sbOut.AppendFormat("{0} is not a valid weapon for {1}.\n", what, who);
                    return;
                }
            }

            sbOut.AppendFormat("{0} will attack with {1}.\n", who, what);
            Worker.note3(who + "/_routines", "shoot", what);
            camp_update();
        }

        public void camp_update()
        {
            var master = GameHost.Program.master;

            var present = getpresent();
            var keylist = new List<String>();

            lock (master)
            {
                if (master.ContainsKey("_camp"))
                {
                    Dict dict = master["_camp"];

                    foreach (var k in dict.Keys) keylist.Add(k);
                }
            }

            foreach (var k in keylist)
            {
                if (!present.Contains(k))
                {
                    GameHost.Worker.del_key("_camp", k);
                }
            }

            foreach (var p in present)
            {
                var task = GameHost.Worker.readkey(p + "/_routines", "camp");
                if (task == null || task == "") task = "none";

                var shoot = GameHost.Worker.readkey(p + "/_routines", "shoot");
                if (shoot == null || shoot == "") shoot = "none";

                var value = String.Format("task:{0} shoot:{1}", task, shoot);

                var current = GameHost.Worker.readkey("_camp", p);

                if (current != value)
                {
                    GameHost.Worker.note3("_camp", p, value);
                }
            }
        }

        public void routine_display(string routine)
        {
            var master = GameHost.Program.master;

            string key = roller + "/" + "_routines";
            string tasks;

            lock (master)
            {
                if (!master.ContainsKey(key))
                {
                    sbOut.Append(roller + ": none\n");
                    return;
                }

                Dict dict = master[key];

                if (!dict.ContainsKey(routine))
                {
                    sbOut.Append(roller + ": none\n");
                    return;
                }

                tasks = dict[routine];
            }

            sbOut.Append(roller +": "+tasks + "\n");
        }

        public List<string> getallparties()
        {
            List<string> list = new List<string>();

            var master = GameHost.Program.master;
            lock (master)
            {
                foreach (string dir in master.Keys)
                {
                    // only party directories
                    if (!dir.StartsWith("_party/"))
                        continue;

                    list.Add(dir);
                }
            }

            return list;
        }

        public string getavailablespellid(string who)
        {
            return getavailableid(who, "_presence", 1);
        }

        public string getavailableshugenjaid(string who)
        {
            return getavailableid(who, "_shugenja", 1);
        }

        public string getavailableid(string who, string dir, int startId)
        {
            var d = new Dictionary<int, int>();

            var master = GameHost.Program.master;
            lock (master)
            {
                if (!master.ContainsKey(dir))
                {
                    return "00001";
                }

                Dict dict = master[dir];

                who = who + "|";

                foreach (var k in dict.Keys)
                {
                    if (!k.StartsWith(who))
                        continue;

                    int n = k.IndexOf('|');
                    if (n <= 0)
                        continue;

                    if (k.Length == n+1)
                        continue;

                    var ss = k.Substring(n+1);
                    int i;
                    if (Int32.TryParse(ss, out i))
                    {
                        d.Add(i, i);
                    }
                }

                int spellId = startId;
                while (d.ContainsKey(spellId)) spellId++;

                var id = spellId.ToString();

                if (id.Length == 1) id = "0000" + id;
                if (id.Length == 2) id = "000" + id;
                if (id.Length == 3) id = "00" + id;
                if (id.Length == 4) id = "0" + id;

                return id;
            }
        }
        
        public string getlootitem(string item)
        {
            var master = GameHost.Program.master;
            lock (master)
            {
                if (!master.ContainsKey("_loot"))
                {
                    return null;
                }

                Dict dict = master["_loot"];

                item = "|" + item;

                foreach (var k in dict.Keys)
                {
                    if (k.EndsWith(item))
                    {
                        return k;
                    }
                }
            }
            return null;
        }

        public List<string> getallplayers()
        {
            return Worker.getallplayers();
        }

        public List<string> getabsent()
        {
            return Worker.getabsent(origin);
        }

        public List<string> getpresent()
        {
            return Worker.getpresent(origin);
        }

        public static string readkey(string dir, string logicalKey)
        {
            return Worker.readkey(dir, logicalKey);
        }

        public string findpartyfolder()
        {
            return Worker.findpartykey(origin);
        }

        public class StringInt
        {
            public string key;
            public int val;
        };

        public class StringPair
        {
            public string key;
            public string val;
        };

        public class StringTriple
        {
            public string dir;
            public string key;
            public string val;
        };

        private List<StringPair> infoAll(string args)
        {
            List<StringPair> list = new List<StringPair>();

            var master = GameHost.Program.master;

            lock (master)
            {
                foreach (string k in master.Keys)
                {
                    Dict d = master[k];

                    if (!d.ContainsKey(args))
                        continue;

                    list.Add(new StringPair { key = k, val = d[args] });
                }
            }
            return list;
        }
        
        private List<StringPair> infoWho(string args, string who)
        {
            List<StringPair> list = new List<StringPair>();

            string prefix = who + "/";

            var master = GameHost.Program.master;

            lock (master)
            {
                foreach (string k in master.Keys)
                {
                    if (k.StartsWith(prefix) || k == who)
                    {

                        Dict d = master[k];

                        if (!d.ContainsKey(args))
                            continue;

                        if (k.Contains("/_forms/"))
                            continue;

                        list.Add(new StringPair { key = k, val = d[args] });
                    }
                }
            }
            return list;
        }

        public class CharBasics
        {
            public int _str;
            public int _con;
            public int _siz;
            public int _int;
            public int _pow;
            public int _dex;
            public int _app;

            public int max_str;
            public int max_con;
            public int max_siz;
            public int max_int;
            public int max_pow;
            public int max_dex;
            public int max_app;

            public int communication;
            public int agility;
            public int manipulation;
            public int stealth;
            public int knowledge;
            public int alchemy;
            public int perception;
            public int magic;
            public int wizardry;
            public int attack;
            public int parry;
            public int encumberence;
            public int fatigue;
            public int enc_less_armor;
            public string species;
        }

        private bool exists(string dir, string key)
        {
            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey(dir))
                    return false;

                Dict d = master[dir];

                return d.ContainsKey(key);
            }
        }

        private int runebonus(string who, string logicalKey)
        {
            var master = GameHost.Program.master;

            string prefix = who + "|";

            if (!logicalKey.StartsWith(prefix))
                return 0;

            logicalKey = logicalKey.Substring(prefix.Length);

            lock (master)
            {
                string path = who + "/_runelevel";
                if (!master.ContainsKey(path))
                    return 0;

                Dict d = master[path];

                if (d.ContainsKey(logicalKey))
                    return 10;
            }

            return 0;
        }

        private CharBasics getbasics(string who, bool suppressBuffs)
        {
            var master = GameHost.Program.master;

            var b = new CharBasics();

            lock (master)
            {
                if (!master.ContainsKey(who))
                    return b;

                GameHost.Dict.suppressBuffs = suppressBuffs;

                Dict d = master[who];

                b._str = Value(d, "STR");
                b._con = Value(d, "CON");
                b._siz = Value(d, "SIZ");
                b._int = Value(d, "INT");
                b._pow = Value(d, "POW");
                b._dex = Value(d, "DEX");
                b._app = Value(d, "APP");

                if (master.ContainsKey(who + "/_misc"))
                {
                    d = master[who + "/_misc"];

                    b.max_str = Value(d, "MAX_STR");
                    b.max_con = Value(d, "MAX_CON");
                    b.max_siz = Value(d, "MAX_SIZ");
                    b.max_int = Value(d, "MAX_INT");
                    b.max_pow = Value(d, "MAX_POW");
                    b.max_dex = Value(d, "MAX_DEX");
                    b.max_app = Value(d, "MAX_APP");

                    b.communication = Value(d, "communication");
                    b.agility       = Value(d, "agility");
                    b.manipulation  = Value(d, "manipulation");
                    b.stealth       = Value(d, "stealth");
                    b.knowledge     = Value(d, "knowledge");
                    b.alchemy       = Value(d, "alchemy");
                    b.perception    = Value(d, "perception");
                    b.magic         = Value(d, "magic");
                    b.wizardry      = Value(d, "wizardry");
                    b.attack        = Value(d, "attack");
                    b.parry         = Value(d, "parry");
                    b.encumberence  = Value(d, "encumberence");
                    b.fatigue       = Value(d, "fatigue");
                    b.enc_less_armor = (int)(DoubleValue(d, "encumberence") - DoubleValue(d, "armor_enc"));
                    b.species       = StringValue(d, "species");
                }
                else
                {
                    b.communication = b._int - 10 + Math.Min((b._pow - 10) / 2, 10) + Math.Min((b._app - 10) / 2, 10);
                    b.agility = b._dex - b._siz + Math.Min((b._str - 10) / 2, 10);
                    b.manipulation = b._dex + b._int + Math.Min((b._str - 10) / 2, 10) - 20;
                    b.stealth = b._dex + 10 - b._siz - b._pow;
                    b.knowledge = b._int - 10;
                    b.alchemy = b._int + b._pow + b._dex - 40;
                    b.perception = b._int - 10 + Math.Min((b._pow - 10) / 2, 10) + Math.Min((b._con - 10) / 2, 10);
                    b.magic = b._int + b._pow - 20 + Math.Min((b._dex - 10) / 2, 10);
                    b.attack = b.manipulation;
                    b.parry = b.agility;
                }

                GameHost.Dict.suppressBuffs = false;
            }

            return b;
        }

        private string StringValue(Dict d, string name)
        {
            if (!d.ContainsKey(name))
                return "";

            return d[name];
        }

        private double DoubleValue(Dict d, string name)
        {
            if (!d.ContainsKey(name))
                return 0.0;

            double val;

            if (!double.TryParse(d[name], out val))
                return 0;

            return val;
        }

        private int Value(Dict d, string name)
        {
            if (!d.ContainsKey(name))
                return 0;

            int val;

            if (!Int32.TryParse(d[name], out val))
                return 0;

            return val;
        }
        private List<StringPair> findspiritmana(string who)
        {
            return findstuff(who, "_spiritmana");
        }

        private List<StringPair> findrunemagic(string who)
        {
            return findstuff(who, "_runemagic");
        }
        
        private List<StringPair> findshugenja(string who)
        {
            return findstuff(who, "_shugenja");
        }
        
        private List<StringPair> findpresence(string who)
        {
            return findstuff(who, "_presence");
        }

        private List<StringPair> findloot(string who)
        {
            return findstuff(who, "_loot");
        }

        private List<StringPair> findbuffs(string who)
        {
            return findstuff(who, "_buffs");
        }

        private List<StringPair> findmana(string who)
        {
            return findstuff(who, "_mana");
        }

        private List<StringPair> findfatigue(string who)
        {
            return findstuff(who, "_fatigue");
        }

        private List<StringPair> findused(string who)
        {
            return findstuff(who, "_used");
        }
        
        private List<StringPair> findwounds(string who)
        {
            return findstuff(who, "_wounds");
        }

        private List<StringPair> findchecks(string who)
        {
            return findstuff(who, "_checks");
        }

        private List<StringPair> findstuff(string who, string stuff)
        {
            // stuff is a folder like "_checks"

            var results = new List<StringPair>();
            var groupList = new List<String>();

            var master = GameHost.Program.master;

            lock (master)
            {

                // if there is no root level folder with this kind of stuff, then forget the whole thing
                // return the empty result
                if (!master.ContainsKey(stuff))
                    return results;

                // now we're going to look at the folder, it's checks, buffs, notes, whatever
                Dict d = master[stuff];

                // if the generic party who is specified redirect it to the current party
                if (who == "party")
                {
                    var folder = findpartyfolder();
                    if (master.ContainsKey(folder))
                    {
                        who = folder;
                    }
                }

                // a Dict that is not a person potentially a party, so in that case set up a group list
                if (master.ContainsKey(who))
                {
                    Dict group = master[who];

                    // check if it's a person
                    if (!group.ContainsKey("INT") ||
                        !group.ContainsKey("POW") ||
                        !group.ContainsKey("SIZ"))
                    {
                        // each key in the group node is a party member who may or may not be present
                        foreach (string k in group.Keys)
                        {
                            if (group[k].StartsWith("y"))
                                groupList.Add(k + "|");
                        }
                    }
                }

                string prefix = null;
                
                if (who != null && who != "all")
                    prefix = who + "|";

                var keys = new string[d.Keys.Count];
                int i = 0;
                foreach (var k in d.Keys)
                {
                    keys[i++] = k;
                }

                Array.Sort(keys);

                foreach (var k in keys)
                {
                    if (groupList.Count > 0)
                    {
                        bool found = false;
                        foreach (var member in groupList)
                        {
                            if (k.StartsWith(member))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            continue;
                    }
                    else if (prefix != null && !k.StartsWith(prefix))
                    {
                        continue;
                    }

                    results.Add(new StringPair { key = k, val = d[k] });
                }
            }

            return results;
        }

        private List<StringTriple> findkeysgeneral(string who, string[] args)
        {
            var people = new List<string>();
            var results = new List<StringTriple>();

            if (who == "all")
            {
                people = getallplayers();
            }
            else if (who == "party")
            {
                people = getpresent();
            }
            else
            {
                who = FindWho(who, false);
                if (who == null)
                {
                    return results;
                }
                people.Add(who);
            }

            List<string> argsRequire = new List<string>();
            List<string> argsReject = new List<string>();

            foreach (var s in args)
            {
                if (s.StartsWith("-"))
                    argsReject.Add(s.Substring(1));
                else
                    argsRequire.Add(s);
            }

            foreach (string p in people)
            {
                findkeysgeneral(results, p, argsRequire, argsReject);
            }

            return results;
        }

        private void findkeysgeneral(List<StringTriple> results, string who, List<string> argsRequire, List<string> argsReject)
        {
            var master = GameHost.Program.master;

            string whoSlash = who + "/";

            bool fStat = false;

            if (argsRequire.Count == 1)
            {
                switch (argsRequire[0].ToLower())
                {
                    case "str":
                    case "siz":
                    case "con":
                    case "int":
                    case "pow":
                    case "dex":
                    case "app":
                        fStat = true;
                        break;
                }
            }

            bool fSkipMisc = true;

            foreach (var a in argsRequire)
            {
                if (a =="_" | a.StartsWith("/_"))
                {
                    fSkipMisc = false;
                    break;
                }
            }

            var reRequire = ConvertToRegex(argsRequire);
            var reReject = ConvertToRegex(argsReject);

            lock (master)
            {
                foreach (string dir in master.Keys)
                {
                    if (dir != who && !dir.StartsWith(whoSlash))
                        continue;

                    // no sub directories for "STR" "POW" etc. don't even look
                    if (fStat && dir != who)
                        continue;

                    if (fSkipMisc && dir.Contains("/_"))
                    {
                        // descend into standard spells types and weapons 
                        if (!dir.Contains("_spells") &&
                            !dir.Contains("_wpn") &&
                            !dir.Contains("_stored_spells") &&
                            !dir.Contains("_herocast") &&
                            !dir.Contains("_runemagic") &&
                            !dir.Contains("_one_use") &&
                            !dir.Contains("_battlemagic") &&
                            !dir.Contains("_school"))
                        {
                            continue;
                        }
                    }

                    Dict d = master[dir];

                    foreach (string key in d.Keys)
                    {
                        var keylow = dir + "/" + key + "/";

                        int i = 0;
                        for (i = 0; i < argsReject.Count; i++)
                        {
                            if (reReject[i].IsMatch(keylow))
                                break;
                        }

                        if (i < argsReject.Count)
                        {
                            continue;
                        }

                        for (i = 0; i < argsRequire.Count; i++)
                        {
                            if (!reRequire[i].IsMatch(keylow))
                                break;
                        }

                        if (i == argsRequire.Count)
                        {
                            results.Add(new StringTriple { dir = dir, key = key, val = d[key] });
                        }
                    }
                }
            }
        }
        
        private List<StringTriple> findkeys(string who, string[] args, bool suppressBuffs)
        {
            var results = new List<StringTriple>();

            var master = GameHost.Program.master;

            string whoSlash = who+"/";

            bool fStat = false;

            if (args.Length == 1)
            {
                switch (args[0].ToLower())
                {
                    case "str":
                    case "siz":
                    case "con":
                    case "int":
                    case "pow":
                    case "dex":
                    case "app":
                        fStat = true;
                        break;
                }
            }

            lock (master)
            {
                GameHost.Dict.suppressBuffs = suppressBuffs;

                foreach (string dir in master.Keys)
                {
                    if (dir != who && !dir.StartsWith(whoSlash))
                        continue;

                    // no sub directories for "STR" "POW" etc. don't even look
                    if (fStat && dir != who)
                        continue;

                    // checks only allowed on weapons and standard spells
                    // not runemagic, not one_use, not battlemagic, not shugenja school
                    // certainly not armor, or other misc things.
                    // hit location is needed because the wound command uses this path too
                    if (dir.Contains("/_"))
                    {
                        if (!dir.Contains("/_wpn") &&
                            !dir.Contains("/_spells") &&
                            !dir.Contains("/_hit_location") &&
                            !dir.Contains("/_herocast") &&
                            !dir.Contains("/_stored_spells"))
                            continue;
                    }

                    Dict d = master[dir];

                    string dirLower = dir.ToLower();

                    foreach (string key in d.Keys)
                    {
                        var keylow = dirLower + "/" + key.ToLower() + "/";

                        int i = 0;
                        for (i = 0; i < args.Length; i++)
                        {
                            if (keylow.IndexOf(args[i]) < 0)
                                break;
                        }

                        if (i == args.Length)
                        {
                            results.Add(new StringTriple { dir = dir, key = key, val = d[key] });
                        }
                    }
                }

                GameHost.Dict.suppressBuffs = false;
            }

            return results;
        }        
        
        private Dictionary<string, int> gvalues(string args)
        {
            List<StringPair> list = infoAll(args);
            List<String> party = getpresent();
            Dictionary<string, int> result = new Dictionary<string, int>();

            foreach (string k in party)
            {
                string prefix = k + "/";

                foreach (StringPair p in list)
                {
                    if (p.key.StartsWith(prefix) || p.key == k)
                    {
                        int val;

                        if (Int32.TryParse(p.val, out val))
                            result[k] = val;

                        break;
                    }
                }
            }

            return result;
        }

        // group routine... all those present perform the routine
        public void groutine_cmd(string routine)
        {
            foreach (string s in getpresent())
            {
                roller = s;
                routine_cmd(routine);
            }
        }

        // group routine display... all those present dump the routine
        public void groutine_display(string routine)
        {
            foreach (string s in getpresent())
            {
                roller = s;
                routine_display(routine);
            }
        }
        //--------------------------------------------------------------
        // group best roll... roll the three highest of those present
        //
        void gbest_cmd(string args)
        {
            if (args == "")
            {
                dumpCommandHelp();
                return;
            }

            var present = getpresent();

            var rollers = new List<StringInt>();

            foreach (string s in present)
            {
                roller = s;

                try
                {
                    int val = eval_roll(args);

                    var p = new StringInt();
                    p.key = s;
                    p.val = val;

                    if (val <= 0)
                        continue;

                    rollers.Add(p);
                }
                catch (ParseException)
                {
                }
            }

            if (rollers.Count < 1)
            {
                sbOut.Append("Nobody has a skill that is positive\n");
                return;
            }

            string roll = args + "%";

            rollers.Sort((t1, t2) =>
                {
                    if (t1.val < t2.val) return 1;  // sort bigget to smallest
                    if (t1.val > t2.val) return -1;
                    return 0;
                });

            for (int i = 0; i < 3 && i < rollers.Count; i++)
            {
                roller = rollers[i].key;

                try
                {
                    int result = eval_roll(roll);
                    sbOut.AppendFormat("{0}: {1}\n", roller, sb2.ToString());
                }
                catch (ParseException)
                {
                }
            }
        }

        //--------------------------------------------------------------
        // group pct roll... all those present plus extras make the indicated roll
        //
        void gpct_cmd(string args)
        {
            ParseToMap(args);

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            string me = roller;

            string roll = listArgs[0];
            listArgs.RemoveAt(0);

            while (listArgs.Count > 0)
            {
                var str = listArgs[0];
                if (str.StartsWith("+") || str.StartsWith("-"))
                {
                    roll += str;
                    listArgs.RemoveAt(0);
                    continue;
                }

                break;
            }

            List<String> present;
            string master;

            switch (listArgs.Count)
            {
                case 0:
                    master = "";
                    present = getpresent();
                    break;

                case 1:
                    present = getpresent();
                    master = listArgs[0];
                    break;

                default:
                    master = listArgs[0];
                    present = listArgs;
                    break;
            }
            
            int rollers = 0;

            int pBest = -1;
            string rBest = "";

            foreach (string s in present)
            {
                SetRoller(s, me);

                try
                {
                    int val = eval_roll(roll);

                    rollers++;

                    if (val > pBest)
                    {
                        pBest = val;
                        rBest = s;
                    }
                }
                catch (ParseException)
                {
                }
            }

            if (master == "")
            {
                master = rBest;
            }

            if (rollers == 0)
            {
                sbOut.Append("Nobody has a skill that is positive\n");
                return;
            }

            master = SetRoller(master, me);

            var masterroll = String.Format("({0}+{1})%", roll, rollers - 1);

            List<string> specials = new List<string>();

            roll = roll + "%";

            foreach (string s in present)
            {
                SetRoller(s, me);

                if (roller == master)
                    continue;

                try
                {
                    int result = eval_roll(roll);
                    
                    if (!fTerse)
                    {
                        sbOut.AppendFormat("Helper: {0} {1}\n", roller, sb2.ToString());
                    }

                    if (result == 2)
                    {
                        specials.Add(roller);
                    }
                    else if (result == 3)
                    {
                        specials.Add(roller + " (crit)");
                    }
                }                  
                catch (ParseException)
                {
                }
            }

            int overall = 0;
            try
            {
                roller = master;
                overall = eval_roll(masterroll);

                sbOut.AppendFormat("Master: {0} {1}\n", roller, sb2.ToString());
            }
            catch (ParseException)
            {
                sbOut.AppendFormat("Master: {0} {1}\n", roller, "roll failed!");
            }

            if (overall > 0)
            {
                for (int i = 0; i < specials.Count; i++)
                {
                    if (i == 0)
                    {
                        sbOut.Append("_\nHelper Checks: ");
                    }
                    if (i > 0)
                    {
                        sbOut.Append(", ");
                    }
                    sbOut.Append(specials[i]);
                }
            }
        }

        //--------------------------------------------------------------
        // group roll... all those present plus extras make the indicated roll
        //
        void groll_cmd(string roll)
        {
            if (roll == "")
            {
                dumpCommandHelp();
                return;
            }

            foreach (string s in getpresent())
            {
                roller = s;

                try
                {
                    eval_roll(roll);
                }
                catch (ParseException)
                {
                    sb1.Length = 0;
                    sb2.Length = 0;
                    sb2.AppendFormat("roll '{0}' failed", roll);
                }

                sbOut.Append(roller);
                sbOut.Append(": ");
                sbOut.Append(sb2.Replace("( ", "(").Replace(" )", ")").Replace("% roll", "%"));
                sbOut.Append("\n");
            }
        }

        bool lookup_statroll_invisibly(string roller, string table, out int result)
        {
            StringBuilder sbT1 = sb1;
            StringBuilder sbT2 = sb2;
            bool fSuppressTotalT = fSuppressTotal;

            fSuppressTotal = true;

            sb1 = new StringBuilder();
            sb2 = new StringBuilder();

            bool fFound = lookup_statroll(roller, table, out result);
            
            sb1 = sbT1;
            sb2 = sbT2;
            fSuppressTotal = fSuppressTotalT;
            
            return fFound;
        }

        bool lookup_statroll(string roller, string table, out int result)
        {
            result = 0;

            List<StringPair> list = infoWho(table, roller);

            foreach (StringPair p in list)
            {
                result = perform_nested_roll(table, p.val);
                return true;
            }

            Dict d = null;

            string key = roller + "/_wpn/" + table;

            var master = GameHost.Program.master;

            lock (master)
            {
                if (!master.ContainsKey(key))
                    return false;

                d = master[key];
                if (!d.ContainsKey("attack"))
                   return false;

                key = d["attack"];
            }

            result = perform_nested_roll(table, key);
            return true;
        }

        void task_cmd(string args)
        {
            ParseToMap(args);
            string task;
            string who;

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }
            else if (listArgs.Count == 1)
            {
                who = "me";
                task = listArgs[0];
            }
            else
            {
                who = listArgs[0];
                task = listArgs[1];
            }

            SetRoller(who);

            var master = GameHost.Program.master;
            string roll;

            lock (master)
            {
                if (!master.ContainsKey("_tasks"))
                {
                    sbOut.Append("No tasks have been set in the _tasks folder\n");
                    return;
                }

                if (!master["_tasks"].ContainsKey(task))
                {
                    sbOut.AppendFormat("The {0} task is not set in the _tasks folder\n", task);
                    return;
                }

                sbOut.Append( roller + " " + task + ": ");

                roll = master["_tasks"][task];
            }

            try
            {
                eval_roll(roll);
            }
            catch (ParseException)
            {
                sb1.Length = 0;
                sb2.Length = 0;
                sb2.AppendFormat("roll '{0}' failed", roll);
            }

            sbOut.Append(sb2.Replace("( ","(").Replace(" )",")").Replace("% roll", "%"));
            sbOut.Append("\n");
        }

        void camp_cmd(string args)
        {
            ParseToMap(args);
            string verb;

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            if (listArgs.Count == 1)
            {
                verb = listArgs[0];
                switch (verb)
                {
                    case "now":
                        camp_now();
                        return;
                    case "list":
                        camp_list();
                        return;
                    case "tasks":
                        camp_tasks();
                        return;
                    case "shoot":
                        camp_show_shoot();
                        return;
                    default:
                        dumpCommandHelp();
                        return;
                }
            }

            if (listArgs.Count == 2)
            {
                if (listArgs[0] == "shoot")
                {
                    SetRoller(listArgs[1]);
                    display_weapons(roller);
                    return;
                }

                SetRoller(listArgs[0]);

                verb = listArgs[1];
                camp_set_action(roller, verb);
                return;
            }

            if (listArgs.Count == 3)
            {
                if (listArgs[0] != "shoot")
                {
                    dumpCommandHelp();
                    return;
                }

                SetRoller(listArgs[1]);

                var weapon = listArgs[2];
                camp_set_weapon(roller, weapon);
                return;
            }
        }

        private void camp_tasks()
        {
            display_tasks();
        }

        private void camp_show_shoot()
        {
            sbOut.Append("Missile weapon selection for the current party:\n");
            groutine_display("shoot");
        }

        private void camp_list()
        {
            sbOut.Append("Camping tasks for the current party:\n");
            groutine_display("camp");
        }

        void camp_now()
        {    
            sbOut.Append("Preparing Camp!\n");
            sbOut.Append(" \nSpotting Location (Battle Savvy)...\n");
            gpct_cmd("battle_savvy");
            sbOut.Append(" \nPersonal Actions...\n");
            groutine_cmd("camp");
            sbOut.Append(" \nAll Party Members Hide Self...\n");
            groll_cmd("hide%");
        }
    }
}