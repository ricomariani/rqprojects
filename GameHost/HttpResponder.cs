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
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Linq;

namespace GameHost
{
    class HttpResponder
    {
        static HttpResponder responder;

        Dictionary<string, Dict> master = GameHost.Program.master;
        HttpListener listener;
        HttpListenerRequest request;
        HttpListenerResponse response;
        HttpListenerContext context;
        List<SkillRecord> skills;
        Dictionary<string, bool> commonSkills;

        public static void StartHttpListener()
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }

            responder = new HttpResponder();
        }

        public HttpResponder()
        {
            commonSkills = new Dictionary<string, bool>();

            foreach (var s in commonSkillStrings)
                commonSkills.Add(s, true);

            // Create a listener.
            listener = new HttpListener();

            // These prefixes allow the bot to run in the debugger not elevated listening locally with no privs
            // uncomment this stuff and comment out the others if you are doing local repro work not on the deployment.
            //
            // listener.Prefixes.Add("http://localhost:8080/sheet/");
            // listener.Prefixes.Add("http://localhost:8080/calendar/");


            // these will require that the bot run elevated, if they fail, elevate
            listener.Prefixes.Add("http://*:80/sheet/");
            listener.Prefixes.Add("http://*:80/calendar/");
            listener.Prefixes.Add("http://*:80/todos/");
            listener.Prefixes.Add("http://*:80/notes/");
            listener.Prefixes.Add("http://*:80/who/");
            listener.Prefixes.Add("http://*:80/bot/");

            try
            {

                listener.Start();
                Console.WriteLine("Listening for Http...");

                listener.BeginGetContext(new AsyncCallback(ProcessHttpRequest), listener);

                // listener.Stop();
            }
            catch (Exception)
            {
                Console.WriteLine("Can't listen for Http...");
            }
        }

        void ProcessHttpRequest(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            // Call EndGetContext to complete the asynchronous operation.
            context = listener.EndGetContext(result);
            request = context.Request;
            response = context.Response;

            var url = request.RawUrl;
            var host = request.UserHostName;

            Console.WriteLine(url);
            Console.WriteLine(host);

            url = Uri.UnescapeDataString(url);

            if (url.StartsWith("/bot/"))
            {
                string[] sp = new string[] {";"};
                var cmd = url.Substring(5);
                var cmds = cmd.Split(sp, StringSplitOptions.RemoveEmptyEntries);

                var roller = "roller";
                var origin = "#gameroom";

                StringBuilder sb = new StringBuilder();
                foreach (var x in cmds)
                {
                    if (x.StartsWith("!"))
                    {
                        sb.AppendLine("&lt;" + roller + "&gt; " + x);
                        var bot = new GameBot.Bot(roller, x.StartsWith("!help") ? "#null" : origin);
                        bot.SendBufferToIrc("<WebRoller_" + roller + "> " + x);
                        sb.Append(bot.RunCmd(x));
                        sb.AppendLine("---");
                        bot = null;
                    }
                    else if (x.StartsWith("@"))
                    {
                        origin = "#" + x.Substring(1);
                    }
                    else
                    {
                        roller = x;
                    }
                }
                var res = sb.ToString();
                res = res.Replace("<", "&lt;").Replace(">", "&gt;");
                res = res.Replace("\n", "\n<br>");
                WriteSimpleOutput(res);
            }
            else if (url.StartsWith("/notes/"))
            {
                var args = url.Substring(7);
                if (args.Length > 0)
                {
                    FormatMetaAsHtml(args, GameBot.Bot.NoteTypeNote());
                }
            }
            else if (url.StartsWith("/todos/"))
            {
                var args = url.Substring(7);
                if (args.Length > 0)
                {
                    FormatMetaAsHtml(args, GameBot.Bot.NoteTypeTodo());
                }
            }
            else if (url.StartsWith("/who/"))
            {
                var args = url.Substring(5);
                if (args.Length > 0)
                {
                    FormatMetaAsHtml(args, GameBot.Bot.NoteTypeWho());
                }
            }
            else if (url == "/calendar")
            {
                FormatCalendarChoicesHtml();
            }
            else if (url.StartsWith("/calendar/"))
            {
                var args = url.Substring(10);
                if (args.Length > 0)
                {
                    FormatCalendarAsHtml(args);
                }
                else
                {
                    FormatCalendarChoicesHtml();
                }
            }
            else if (url.StartsWith("/sheet/"))
            {
                var pc = url.Substring(7);
                if (pc.Length > 0)
                {
                    FormatPcAsHtml(pc);
                }
                else
                {
                    WriteSimpleOutput("No PC specified");
                }
            }
            else if (host.StartsWith("sheet."))
            {
                var pc = url.Substring(1);
                if (pc.Length > 0)
                {
                    FormatPcAsHtml(pc);
                }
                else
                {
                    WriteSimpleOutput("No PC specified");
                }
            }
            else
            {
                WriteSimpleOutput("Unknown request.");
            }

            // start another one
            listener.BeginGetContext(new AsyncCallback(ProcessHttpRequest), listener);
        }

        void FormatMetaAsHtml(string args, GameBot.Bot.NoteMeta n)
        {
            var bot = new GameBot.Bot("roller", "roller");
            var results = bot.FindNotes(args, n);

            StringBuilder b = new StringBuilder();
            b.AppendLine("<html>");
            b.AppendFormat("<head><title>{0}</title></head>", n.Title);
            b.AppendLine("<body>");
            b.AppendLine("<style>tr {vertical-align:text-top;} </style>");
            b.AppendLine("<center>");
            b.AppendLine("<table style=\"font-family: arial, sans-serif; font-size:8pt; max-width:800px;\">");
            b.AppendFormat("<tr style=\"text-align:center\"><td colspan=\"2\"><h1>{0}<br><br></h1></td></tr>\r\n", n.Title);

            string personlast = "";

            foreach (var r in results)
            {
                var k = r.key;
                var v = r.val;

                string person, id;

                GameHost.Worker.Parse2Ex(k, out person, out id, "|");

                if (person == personlast)
                {
                    b.AppendFormat("<tr><td></td><td>{0}</td></tr>", v);
                }
                else
                {
                    // b.AppendLine("<tr><td>&nbsp;</td></tr>");
                    b.AppendFormat("<tr><td><a href=\"/who/@{0}\">{0}</a></td><td>{1}</td></tr>", person, v);
                }

                personlast = person;
            }

            if (n.whoPc != null && n.whoPc != "all" && n.Folder == "_who")
            {
                var x = GameBot.Bot.FormatDossier(n);
                b.AppendLine("<tr><td></td><td>");
                b.AppendLine(x.Replace(n.whoPc, "<br>"));
                b.AppendFormat("<br><br>Sheet: <a href=\"/sheet/{0}\">{0}</a>", n.whoPc);
                b.AppendLine("</td></tr>");
            }


            b.AppendLine("</table>");
            b.AppendLine("</center>");
            b.AppendLine("</body>");
            b.AppendLine("</html>");

            WriteRawOutput(b.ToString());       
        }

        static string[] seasons = new string[] { "Sea", "Fire", "Earth", "Dark", "Storm", "Holy"};
        static string[] weeks = new string[] {"Disorder", "Harmony", "Death", "Fertility", "Stasis", "Movement", "Illusion", "Truth" };
        static string[] hweeks = new string[] { "Luck", "Fate" };

        void FormatCalendarChoicesHtml()
        {
            var bot = new GameBot.Bot("roller", "roller");
            var results = bot.FindEvents(".");

            StringBuilder b = new StringBuilder();
            b.AppendLine("<html>");
            b.AppendFormat("<head><title>Calendar</title></head>");
            b.AppendLine("<body>");
            b.AppendLine("<center>");
            b.AppendLine("<div style=\"font-family: arial, sans-serif; font-size:8pt; max-width:800px;\">");
            b.AppendLine("<br>The following tags are in use in the events calendar:<br><br>");

            var dict = new Dictionary<string, int>();

            foreach (var r in results)
            {
                var v = r.val;

                string date;
                string desc;
                string mm, dd, year;
                string tag;
                string x;

                GameHost.Worker.Parse2(v, out date, out desc);
                GameHost.Worker.Parse3Ex(date, out mm, out dd, out year, "/");

                GameHost.Worker.Parse2Ex(desc, out tag, out x, ":");

                if (!dict.ContainsKey(year))
                {
                    dict[year] = 1;
                }

                if (tag.Length > 0 && x != null && x.Length > 0)
                {
                    if (!dict.ContainsKey(tag))
                        dict[tag] = 1;
                }
            }

            var tags = dict.Keys.ToList();
            tags.Sort();


            bool fFirst = true;
            bool fFirstNonYear = true;

            foreach (var r in tags)
            {

                if (Char.IsDigit(r[0]))
                {
                    if (!fFirst)
                    {
                        b.Append("&nbsp; * &nbsp;");
                    }

                    b.AppendFormat("<a href=\"/calendar//{0}\">{0}</a> \r\n", r);
                }
                else
                {
                    if (fFirstNonYear)
                    {
                        fFirstNonYear = false;
                        b.AppendLine("<br><br>");
                    }
                    else
                    {
                        b.Append("&nbsp; * &nbsp;");
                    }

                    b.AppendFormat("<a href=\"/calendar/{0}:\">{0}</a> \r\n", r);
                }

                fFirst = false;
            }

            b.AppendLine("</div>");
            b.AppendLine("</center>");
            b.AppendLine("</body>");
            b.AppendLine("</html>");
            WriteRawOutput(b.ToString());
        }

        void FormatCalendarAsHtml(string args)
        {
            var bot = new GameBot.Bot("roller", "roller");
            var results = bot.FindEvents(args);

            string yearlast = "";
            string seasonlast = "";
            string weeklast = "";
            string daylast = "";
            string taglast = "";

            StringBuilder b = new StringBuilder();
            b.AppendLine("<html>");
            b.AppendFormat("<head><title>Calendar</title></head>");
            b.AppendLine("<body>"); 
            b.AppendLine("<style>tr {vertical-align:text-top;} </style>");
            b.AppendLine("<center>");
            b.AppendLine("<table style=\"font-family: arial, sans-serif; font-size:8pt; max-width:800px;\">");

            if (results == null || results.Count == 0)
            {
                WriteSimpleOutput(String.Format("Events for '{0}' not found", args));
                return;
            }

            foreach (var p in results)
            {
                var v = p.val;
                string date;
                string desc;
                string mm, dd, year;
                string tag, x;

                GameHost.Worker.Parse2(v, out date, out desc);
                GameHost.Worker.Parse3Ex(date, out mm, out dd, out year, "/");
                GameHost.Worker.Parse2Ex(desc, out tag, out x, ":");

                if (x == "") 
                {
                    desc = tag;
                    tag = "";
                }
                else
                {
                    desc = x;
                }

                int m = 0;
                int d = 0;
                Int32.TryParse(mm, out m);
                Int32.TryParse(dd, out d);

                if (m < 1 || m > 6) continue;
                if (d < 1 || d > 56) continue;
                if (m == 6 && d > 14) continue;

                string season = seasons[m - 1];
                string week = m == 6 ? hweeks[(d - 1) / 7] : weeks[(d - 1) / 7];

                if (yearlast != year)
                {
                    if (yearlast != "") b.AppendLine("<tr><td style=\"width:3em;\">&nbsp;</td></tr>");
                    b.AppendFormat("<tr style=\"text-align:center\"><td colspan=\"3\"><h1><a href=\"/calendar//{0}\">{0}</a><br></h1></td></tr>\r\n", year);
                    seasonlast = "";
                }

                if (seasonlast != season)
                {
                    if (seasonlast != "") b.AppendLine("<tr><td>&nbsp;</td></tr>");
                    b.AppendFormat("<tr style=\"text-align:center\"><td colspan=\"3\"><h2><a href=\"/calendar/{0}/[0-9]+/{1}\">{2} Season</a><br></h2></td></tr>\r\n", m, year, season);
                    weeklast = "";
                }

                if (weeklast != week)
                {
                    b.AppendFormat("<tr><td colspan=\"2\"><h3>{0} Week<br></h3></td></tr>\r\n", week);
                    daylast = "";
                    taglast = "";
                }

                string day = m + "/" + d;

                string dprint = (day == daylast) ? "" : day;

                if (dprint != "")
                {
                    dprint = String.Format("<a href=\"/calendar/{0}/\">{0}</a>", dprint);
                }

                if (tag == taglast)
                {
                    b.AppendFormat("<tr><td style=\"text-align:right\">{0}</td><td style=\"text-align:right\">*</td><td>{1}</td></tr>\r\n", dprint, desc.Replace("<", "&lt;").Replace(">", "&gt;"));
                }
                else
                {
                    b.AppendFormat("<tr><td style=\"text-align:right\">{0}</td><td style=\"text-align:right\"><a href=\"/calendar/{1}:\">{1}:</a></td><td>{2}</td></tr>\r\n", dprint, tag, desc.Replace("<", "&lt;").Replace(">", "&gt;"));
                }

                seasonlast = season;
                yearlast = year;
                weeklast = week;
                daylast = day;
                taglast = tag;
            }
            b.AppendLine("</table>");
            b.AppendLine("</center>");
            b.AppendLine("</body>");
            b.AppendLine("</html>");

            WriteRawOutput(b.ToString());
        }

        void FormatPcAsHtml(string pcRaw)
        {
            StringBuilder col1 = NewTable();
            StringBuilder col2 = NewTable();
            StringBuilder col3 = NewTable();
            StringBuilder col4 = NewTable();
            StringBuilder summary = NewTable();
            string pc = null;

            lock (master)
            {
                skills = new List<SkillRecord>();
                pc = FindWho(pcRaw);

                if (pc == null)
                {
                    WriteSimpleOutput(String.Format("PC '{0}' not found", pcRaw));
                    return;
                }

                string[] bonuses = new string[] {   
                    "agility",
                    "attack",
                    "communication",
                    "knowledge",
                    "alchemy",
                    "magic",
                    "manipulation",
                    "parry",
                    "perception",
                    "stealth"
                };

                string[] miscitems = new string[] {
                    "dex_srm",
                    "armor_enc",
                    "battlemagic_enc",
                    "damage_bonus",
                    "encumberence",
                    "endurance",
                    "free_con", 
                    "free_int", 
                    "life_points",
                    "melee_srm",
                    "movement",
                    "vow_presence",
                    "casting_presence",
                    "siz_srm"
                };

                Dict dict = master[pc];
                Dict dictMisc = null;

                if (master.ContainsKey(pc+"/_misc"))
                    dictMisc = master[pc+"/_misc"];

                AppendTwoCols("<b>Name:</b>", pc, summary);
                AppendMajorItem(dictMisc, "Species:", "species", summary);
                AppendMajorItem(dictMisc, "Religion:", "religion", summary);
                AppendBlank(summary);


                AppendHeader("Stats:", col1);

                AppendStat(dict, dictMisc, "STR", col1);
                AppendStat(dict, dictMisc, "CON", col1);
                AppendStat(dict, dictMisc, "SIZ", col1);
                AppendStat(dict, dictMisc, "INT", col1);
                AppendStat(dict, dictMisc, "POW", col1);
                AppendStat(dict, dictMisc, "DEX", col1);
                AppendStat(dict, dictMisc, "APP", col1);

                AppendCategory("Magic:", "/magic", col1, pc);
                AppendCategory("Perception:", "/perception", col1, pc);
                AppendExplicit("Bonuses:", dictMisc, bonuses, col1);
                AppendExplicit("Misc:", dictMisc, miscitems, col1);

                AppendCategory("Agility:", "/agility", col2, pc);
                AppendCategory("Manipulation:", "/manipulation", col2, pc);
                AppendCategory("Stealth:", "/stealth", col2, pc);
                AppendCategory("Knowledge:", "/knowledge", col2, pc);
                AppendCategory("Alchemy:", "/alchemy", col2, pc);

                AppendCategory("Communication:", "/communication", col3, pc);
                AppendCategory("Battlemagic:", "/_battlemagic", col3, pc);
                AppendCategory("Spells:", "/_spells", col3, pc);
                AppendCategory("Stored Spells:", "/_stored_spells", col3, pc);
                AppendCategory("Runemagic:", "/_runemagic", col3, pc);
                AppendCategory("Herocast:", "/_herocast", col3, pc);
                AppendCategory("Wizardry:", "/_wizardry", col3, pc);
                AppendCategory("Music:", "/_music", col3, pc);
                AppendCategory("One Use:", "/_one_use", col3, pc);
                AppendCategory("Others Spells:", "/_others_spells", col3, pc);
                AppendAllSchools(col3, pc);

                EndTable(col1);
                EndTable(col2);
                EndTable(col3);
                EndTable(summary);

                col4.Append(WriteWeapons(pc));

                var c4 = NewTable();
                AppendBlank(c4);
                WriteLocations(pc, c4);
                AppendBlank(c4);
                WriteMana(pc, c4);
                AppendBlank(c4);
                WriteFatigue(pc, c4);
                EndTable(c4);
                col4.Append(c4.ToString());

                WriteSpirits(pc, col4);
                   
                col4.Append(WriteEquipment(pc));
            }


            // we don't need the lock anymore

            StringBuilder final = new StringBuilder();
            final.AppendFormat("<HTML><HEAD><TITLE>{0}</TITLE></HEAD><BODY>", pc);
            final.Append(summary);
            final.Append("<TABLE><TR><TD style=\"vertical-align: top\">");
            final.Append(col1);
            final.Append("</TD><TD>&nbsp;&nbsp;</TD><TD style=\"vertical-align: top\">");
            final.Append(col2);
            final.Append("</TD><TD>&nbsp;&nbsp;</TD><TD style=\"vertical-align: top\">");
            final.Append(col3);
            final.Append("</TD><TD>&nbsp;&nbsp;</TD><TD style=\"vertical-align: top\">");
            final.Append(col4);
            final.Append("</TD><TR></TABLE>");

            final.Append("<DIV style=\"font-family: arial, sans-serif; font-size:8pt\">");

            bool b = false;

            foreach (var sk in skills)
            {
                if (commonSkills.ContainsKey(sk.name))
                    continue;

                if (sk.category.EndsWith("_school"))
                {
                    AppendHelp(sk.name, "shugenja", final);
                }
                else switch (sk.category)
                {
                    case "/_runemagic":
                    case "/_one_use":
                        AppendHelp(sk.name, "runemagic", final);
                        break;

                    case "/_spells":
                    case "/_stored_spells":
                    case "/_others_spells":
                        b = AppendHelp(sk.name, "battlemagic", final) ||
                            AppendHelp(sk.name, "sorcery", final) ||
                            AppendHelp(sk.name, "wizardry", final);
                        break;

                    default:
                        b = AppendHelp(sk.name, "skill", final) ||
                            AppendHelp(sk.name, "secret", final);
;
                        break;
                }
            }

            skills = null;

            final.Append("</DIV>");
            final.Append("</BODY></HTML>");

            WriteRawOutput(final.ToString());
        }

        private bool AppendHelp(string key, string category, StringBuilder b)
        {
            var entries = FindHelpEntries(key, category);

            if (entries.Count != 1)
                return false;

            b.AppendFormat("<br><b>{0} {1}</b><br>\n{2}<br>\n", entries[0].name, entries[0].type, entries[0].desc);
            return true;
        }

        public static List<GameBot.Bot.Entry> FindHelpEntries(string key, string category)
        {
            var listMatch = new List<string>();
            var listReject = new List<string>();

            string k = key;
            while (k.Contains(" "))
            {
                string car, ctr;
                GameHost.Worker.Parse2(k, out car, out ctr);
                listMatch.Add(car);
                k = ctr;
            }

            k = k.Trim();
            if (k.Length > 0)
                listMatch.Add(k);

            if (category != null && category != "")
            {
                listMatch.Add("//" + category);
            }

            var entries = GameBot.Bot.filterHelp(listMatch, listReject, fMatchDesc: false, fMatchType: true);
            return entries;
        }

        string[] commonSkillStrings = 
        {
            "scan", "search", "ceremony", "enchant", "summon", "smell", "feel", "taste", "sneak", "hide",
            "magic_points", "mysticism", "spirit combat", "track", "listen", "courtesan", "perception", 
            "evaluate", "bargain", "conceal", "devise", "drive", "sleight", "first aid", "mineral", "human", 
            "world", "plant", "treat disease", "treat poison", "butchery", "sing", "orate", "fast talk", "tradetalk", 
            "boat", "climb", "dance", "dodge", "jump", "ride", "swim", "throw", "animal", "leather" 
        };

        private void AppendAllSchools(StringBuilder b, string pc)
        {
            var prefix = pc + "/";
            foreach (var k in master.Keys)
            {
                if (!k.StartsWith(prefix))
                    continue;

                var school = k.Substring(prefix.Length);

                if (!school.StartsWith("_"))
                    continue;

                if (!school.EndsWith("_school"))
                    continue;

                if (school.Contains("/"))
                    continue;

                if (school.Contains("spare"))
                    continue;

                var formal = school.Substring(1).Replace("_", " ");
                formal = formal[0].ToString().ToUpper() + formal.Substring(1) + ":";
                
                AppendCategory(formal, "/"+school, b, pc);
            }
        }

        private char[] WriteLocations(string pc)
        {
            throw new NotImplementedException();
        }

        private static void EndTable(StringBuilder b)
        {
            b.Append("</TABLE>");
        }

        static string tableInit = "<TABLE style=\"font-family: arial, sans-serif; font-size:8pt\">";

        private static StringBuilder NewTable()
        {
            StringBuilder b = new StringBuilder();
            b.Append(tableInit);
            return b;
        }

        private void AppendExplicit(string catname, Dict dict, string[] keys, StringBuilder b)
        {
            if (b.Length > tableInit.Length)
                AppendBlank(b);

            AppendHeader(catname, b);

            Array.Sort(keys);

            foreach (var k in keys)
            {
                if (dict.ContainsKey(k))
                    AppendKeyValueRow(dict, k, b);
            }
        }

        void AppendBlank(StringBuilder b)
        {
            b.Append("<TR><TD>&nbsp;</TD></TR>");
        }

        void AppendHeader(string h, StringBuilder b)
        {
            b.Append("<TR><TD COLSPAN=2><B>");
            b.Append(h);
            b.Append("</B></TD></TR>");
        }

        class SkillRecord
        {
            public string name;
            public string category;
        }

        void AppendCategory(string catFormal, string cat, StringBuilder b, string pc)
        {
            if (!master.ContainsKey(pc + cat))
                return;

            if (b.Length > tableInit.Length)
                AppendBlank(b);

            AppendHeader(catFormal, b);

            Dict dict = master[pc+cat];

            var keyC = dict.Keys;
            
            var keys = new string[keyC.Count];
            
            keyC.CopyTo(keys, 0);

            Array.Sort(keys);

            foreach (var k in keys)
            {
                if (k.StartsWith("spare_"))
                    continue;

                bool isZero = dict.ContainsKey(k) && dict[k] == "0";

                if (isZero && catFormal == "Magic:")
                    continue;

                AppendKeyValueRow(dict, k, b);

                var sk = new SkillRecord();
                sk.name = k
                    .Replace("_0", "")
                    .Replace("_1", "")
                    .Replace("_2", "")
                    .Replace("_3", "")
                    .Replace("_4", "")
                    .Replace("_5", "")
                    .Replace("_6", "")
                    .Replace("_7", "")
                    .Replace("_8", "")
                    .Replace("_9", "")
                    .Replace("_10", "")
                    .Replace("_", " ");

                sk.category = cat;

                if (!isZero)
                    skills.Add(sk);
            }
        }

        static void AppendTwoCols(string c1, string c2, StringBuilder b)
        {
            b.Append("<TR><TD>");
            b.Append(c1);
            b.Append("</TD><TD>");
            b.Append(c2);
            b.Append("</TD></TR>");
        }

        static void AppendStat(Dict dictMain, Dict dictMisc, string k, StringBuilder b)
        {
            string v = dictMain.ContainsKey(k) ? dictMain[k] : "-";

            if (dictMisc != null && dictMisc.ContainsKey("MAX_" + k))
                v += "/" + dictMisc["MAX_" + k];
            else
                v += "/-";

            AppendTwoCols(k, v, b);
        }

        static void AppendMajorItem(Dict dict, string desc, string k, StringBuilder b)
        {
            string v = (dict != null && dict.ContainsKey(k)) ? dict[k] : "-";
            AppendTwoCols("<b>"+desc+"</b>", v, b);
        }

        static void AppendKeyValueRow(Dict dict, string k, StringBuilder b)
        {
            string v = (dict != null && dict.ContainsKey(k)) ? dict[k] : "-";
            AppendTwoCols(k, v, b);
        }

        void WriteSimpleOutput(string msg)
        {
            string responseString = "<HTML><BODY>" + msg + "</BODY></HTML>";
            WriteRawOutput(responseString);
        }

        void WriteRawOutput(string responseString)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
        }

        public class EquipRecord
        {
            public string code;
            public string name;
            public string locs;
        }

        public class WeaponRecord
        {
            public string name;
            public int attack;
            public int parry;
            public int ap;
            public int sr;
            public string dmg;
            public int goodness;
        }

        string GetStr(Dict dict, string k)
        {
            return dict.ContainsKey(k) ? dict[k] : "-";
        }

        int GetInt(Dict dict, string k)
        {
            var val = dict.ContainsKey(k) ? dict[k] : "-";
            int v = 0;

            Int32.TryParse(val, out v);

            return v;
        }

        string WriteWeapons(string pc)
        {
            string prefix = pc + "/_wpn/";
            List<WeaponRecord> wpns = new List<WeaponRecord>();

            foreach (var k in master.Keys)
            {
                if (!k.StartsWith(prefix))
                    continue;

                Dict d = master[k];

                var w = new WeaponRecord();
                w.name = k.Substring(prefix.Length);
                w.sr = GetInt(d, "sr");
                w.parry = GetInt(d, "parry");
                w.attack = GetInt(d, "attack");
                w.ap = GetInt(d, "ap");
                w.dmg = GetStr(d, "dmg");
                w.goodness = w.parry + w.attack;

                wpns.Add(w);
            }

            wpns.Sort((w1, w2) => {
                    if (w1.goodness > w2.goodness) return -1;
                    if (w1.goodness < w2.goodness) return 1;
                    return String.Compare(w1.name, w2.name);
                });

            var b = NewTable();

            b.Append("<TR>" + 
                "<TD><B>Name</B></TD>" +
                "<TD><B>Attack</B></TD>" +
                "<TD><B>Parry</B></TD>" +
                "<TD><B>Damage</B></TD>" +
                "<TD><B>AP</B></TD>" +
                "<TD><B>SR</B></TD>"+
                "</TR>");

            foreach (var w in wpns)
            {
                b.Append("<TR><TD>");
                b.Append(w.name);
                b.Append("</TD><TD>");
                b.Append(w.attack);
                b.Append("</TD><TD>");
                b.Append(w.parry);
                b.Append("</TD><TD>");
                b.Append(w.dmg);
                b.Append("</TD><TD>");
                b.Append(w.ap);
                b.Append("</TD><TD>");
                b.Append(w.sr);
                b.Append("</TD></TR>");
            }

            EndTable(b);
            return b.ToString();
        }

        void WriteSpirits(string pc, StringBuilder col)
        {
            var spirits = pc + "/_spirits";

            if (!master.ContainsKey(spirits))
                return;

            Dict dict = master[spirits];

            var keyC = dict.Keys;

            var keys = new string[keyC.Count];

            keyC.CopyTo(keys, 0);

            Array.Sort(keys);

            var b = NewTable();          
            b.Append("<TR><TD>&nbsp;</TD></TR>");
            b.Append("<TR>" +
                "<TD><B>Spirit ID</B></TD>" +
                "<TD><B>POW</B></TD>" +
                "<TD><B>Used</B></TD>" +
                "<TD><B>SC%</B></TD>" +
                "<TD><B>Stored</B></TD>" +
                "</TR>");


            Dict spiritManaDict = master["_spiritmana"];

            foreach (var k in keys)
            {
                var value = dict[k];

                string sc;
                string pow;
                string stored;
                GameHost.Dict.ExtractSpiritInfoParts(value, out sc, out pow, out stored);

                var skey = pc + "|" + k;
                string used = "0";

                if (spiritManaDict.ContainsKey(skey))
                {
                    used = spiritManaDict.GetRaw(skey);
                }

                b.Append("<TR><TD>");
                b.Append(k);
                b.Append("</TD><TD>");
                b.Append(pow);
                b.Append("</TD><TD>");
                b.Append(used);
                b.Append("</TD><TD>");
                b.Append(sc);
                b.Append("</TD><TD>");
                b.Append(stored);               
                b.Append("</TD></TR>");
            }

            EndTable(b);
            col.Append(b);
        }

        void WriteLocations(string pc, StringBuilder b)
        {
            var hitloc = pc + "/_hit_location";
            var armor = pc + "/_armor";

            if (!master.ContainsKey(hitloc))
                return;

            Dict dict = master[hitloc];

            var keyC = dict.Keys;

            var keys = new string[keyC.Count];

            keyC.CopyTo(keys, 0);

            Array.Sort(keys);

            Dict armorDict = null;
            Dict woundsDict = null;

            if (master.ContainsKey(armor))
                armorDict = master[armor];

            if (master.ContainsKey("_wounds"))
                woundsDict = master["_wounds"];

            b.Append("<TR>" +
                "<TD><B>Location</B></TD>" +
                "<TD><B>Full</B></TD>" +
                "<TD><B>Wounds</B></TD>" +
                "<TD><B>Armor</B></TD>" +
                "</TR>");

            foreach (var k in keys)
            {
                b.Append("<TR><TD>");
                b.Append(k);
                b.Append("</TD><TD>");
                b.Append(dict[k]);
                b.Append("</TD><TD>");
                b.Append(GetWounds(woundsDict, pc, k));
                b.Append("</TD><TD>");
                b.Append((armorDict != null && armorDict.ContainsKey(k)) ? armorDict[k] : "-");
                b.Append("</TD></TR>");
            }
        }

        private string GetWounds(Dict woundsDict, string pc, string loc)
        {
            if (woundsDict == null)
                return "0";

            var key = pc + "|" + loc;

            if (!woundsDict.ContainsKey(key))
                return "0";

            var v = woundsDict[key];

            string dmg;
            string max;
            GameHost.Worker.Parse2(v, out dmg, out max);

            string d;
            string w;
            GameHost.Worker.Parse2Ex(dmg, out d, out w, ":");

            return w;
        }

        void WriteMana(string pc, StringBuilder b)
        {
            if (!master.ContainsKey(pc + "/mana"))
                return;

            b.Append("<TR>" +
                "<TD><B>Mana Type</B></TD>" +
                "<TD><B>Full</B></TD>" +
                "<TD><B>Used</B></TD>" +
                "</TR>");

            Dict manaDict = master[pc + "/mana"];

            Dict usedMana = null;

            if (master.ContainsKey("_mana"))
                usedMana = master["_mana"];

            if (manaDict.ContainsKey("total_magic_points"))
            {
                var m = manaDict["total_magic_points"];
                b.Append("<TR>" +
                    "<TD>"+
                    "normal"+
                    "</TD><TD>" +
                    m +
                    "</TD><TD>" +
                    GetUsedMana(usedMana, pc, "mana") +
                    "</TR>");
            }

            var keyC = manaDict.Keys;

            var keys = new string[keyC.Count];

            keyC.CopyTo(keys, 0);

            Array.Sort(keys);

            foreach (var k in keys)
            {
                if (k == "mpts_per_day" || k == "total_magic_points" || k.StartsWith("spare_"))
                    continue;

                var m = manaDict[k];

                b.Append("<TR>" +
                    "<TD>" +
                    k +
                    "</TD><TD>" +
                    m +
                    "</TD><TD>" +
                    GetUsedMana(usedMana, pc, "mana|"+k) +
                    "</TR>");
            }
        }

        string GetUsedMana(Dict usedMana, string pc, string manaCode)
        {
            if (usedMana == null)
                return "0";

            var k = pc + "|" + manaCode;

            if (!usedMana.ContainsKey(k))
                return "0";

            var v = usedMana[k];

            string used;
            string max;
            GameHost.Worker.Parse2(v, out used, out max);

            string u;
            string m;
            GameHost.Worker.Parse2Ex(used, out u, out m, ":");

            return m;
        }

        void WriteFatigue(string pc, StringBuilder b)
        {
            if (!master.ContainsKey(pc + "/_misc"))
                return;

            b.Append("<TR>" +
                "<TD><B>Fatigue Type</B></TD>" +
                "<TD><B>Full</B></TD>" +
                "<TD><B>Used</B></TD>" +
                "</TR>");

            Dict miscDict = master[pc + "/_misc"];

            Dict usedFatigue = null;

            if (master.ContainsKey("_fatigue"))
                usedFatigue = master["_fatigue"];

            if (miscDict.ContainsKey("fatigue"))
            {
                var m = miscDict["fatigue"];
                b.Append("<TR>" +
                    "<TD>" +
                    "normal" +
                    "</TD><TD>" +
                    m +
                    "</TD><TD>" +
                    GetUsedFatigue(usedFatigue, pc, "normal") +
                    "</TR>");

                b.Append("<TR>" +
                    "<TD>" +
                    "hard" +
                    "</TD><TD>" +
                    m +
                    "</TD><TD>" +
                    GetUsedFatigue(usedFatigue, pc, "hard") +
                    "</TR>");
            }
        }

        string GetUsedFatigue(Dict usedFatigue, string pc, string fatigueCode)
        {
            if (usedFatigue == null)
                return "0";

            var k = pc + "|" + fatigueCode;

            if (!usedFatigue.ContainsKey(k))
                return "0";

            var v = usedFatigue[k];

            string used;
            string max;
            GameHost.Worker.Parse2(v, out used, out max);

            string u;
            string m;
            GameHost.Worker.Parse2Ex(used, out u, out m, ":");

            return m;
        }

        string FindWho(string who)
        {
            List<string> allPlayers = Worker.getallplayers();

            foreach (var member in allPlayers)
            {
                if (member.ToLower() == who.ToLower())
                    return member;
            }

            bool found = false;
            string mem = "";

            var whoLower = who.ToLower();

            foreach (var member in allPlayers)
            {
                var memLower = member.ToLower();

                if (memLower.EndsWith("_were") && !whoLower.EndsWith("_were"))
                    continue;

                if (memLower.Contains(whoLower))
                {
                    if (found)
                    {
                        return null;
                    }

                    found = true;
                    mem = member;
                }
            }

            if (found)
            {
                return mem;
            }

            return null;
        }

        string WriteEquipment(string pc)
        {
            string prefix = pc + "/_equipment";
            List<EquipRecord> eq= new List<EquipRecord>();

            if (!master.ContainsKey(prefix))
                return "";

            Dict dict = master[prefix];

            var b = NewTable();

            foreach (var k in dict.Keys)
            {
                var e = new EquipRecord();

                string code;
                string locs;
                string name;
                string ctr;

                Worker.Parse2Ex(k, out code, out ctr, ":");
                Worker.Parse2Ex(ctr, out name, out locs, ".");

                e.code = code;
                e.name = name.Replace("_", " ");
                e.locs = locs;

                eq.Add(e);
            }

            eq.Sort((e1, e2) => String.Compare(e1.code, e2.code));

            AppendBlank(b);
            AppendHeader("Equipped Weapons:", b);
            foreach (var e in eq)
            {
                if (!e.code.StartsWith("W"))
                    continue;

                b.Append("<TR><TD>");
                b.Append(e.name);
                b.Append("</TD><TR>");
            }

            AppendBlank(b);
            AppendHeader("Equipped Armor:", b);
            foreach (var e in eq)
            {
                if (!e.code.StartsWith("A"))
                    continue;

                b.Append("<TR><TD>");
                b.Append(e.name);
                b.Append("</TD><TD>");
                b.Append(e.locs);
                b.Append("</TD><TR>");
            }

            AppendBlank(b);
            AppendHeader("Equipped Items:", b);
            foreach (var e in eq)
            {
                if (!e.code.StartsWith("G"))
                    continue;

                b.Append("<TR><TD>");
                b.Append(e.name);
                b.Append("</TD><TD>");
                b.Append(e.locs);
                b.Append("</TD><TR>");
            }

            EndTable(b);

            return b.ToString();
        }

    }
}
