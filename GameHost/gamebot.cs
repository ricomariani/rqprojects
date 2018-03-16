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
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameBot
{
    internal partial class Bot
    {
        static System.Diagnostics.Stopwatch stopwatch;
        static DateTime s_wakeUp;
        static Dictionary<string, bool> s_simpleArgs;

        static Bot()
        {
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            s_wakeUp = DateTime.UtcNow;

            s_simpleArgs = new Dictionary<string, bool>();
            s_simpleArgs.Add("id", true);
            s_simpleArgs.Add("remove", true);
        }

        public static void Start(Object parm)
        {
            for (; ; )
            {
                // Connect to a remote device.
                try
                {
                    Console.WriteLine("Connecting to localhost on port 6667");

                    client = new System.Net.Sockets.TcpClient("localhost", 6667);
                    MainLoop();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Connection Failed\r\n");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public void ConsoleLoop()
        {           
            for (;;)
            {
                sbOut = new StringBuilder();
                Console.Write("gamebot> ");
                string cmd = Console.ReadLine();
                if (cmd == null)
                    return;

                if (cmd.Length == 0)
                    continue;

                if (cmd[0] != '!')
                    cmd = "!roll " + cmd;

                RunCmdConsole(cmd);
            }
        }

        public Bot(string roller, string origin)
        {
            this.roller = roller;
            this.origin = origin;

            fTerse = false;
            sbOut = new StringBuilder();
        }

        static TcpClient client = null;
        static char[] newline = { '\n' };
        static char[] space = { ' ' };
        static char[] comma = { ',' };
        static char[] equal = { '=' };
        static Hashtable hashHoursRemaining = new Hashtable();

        static void MainLoop()
        {
            NetworkStream stream = client.GetStream();
            StreamReader sr = new StreamReader(stream, Encoding.ASCII);

            Send("user mudbot mudbot mudbot mudbot\n");
            Send("nick __\n");
            Send("join #gameroom\n");
            Send("join #gameroom2\n");
            Send("join #mystery\n");
	    // add other comamands to make you an operator here whatever they are
            Send("samode #gameroom +o __\n");
            Send("samode #gameroom2 +o __\n");

            for (; ; )
            {
                try
                {
                    string line = sr.ReadLine();

                    if (line == null)
                        return;

                    ProcessLine(line);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return;
                }
            }
        }

        public static void ProcessLine(string cmd)
        {
            if (cmd.StartsWith("PING "))
            {
                Send("PONG :__\n");
                SaveIfNeeded();
                return;
            }

            string roller = null;
            string origin = null;

            Console.WriteLine(cmd);

            int sp = cmd.IndexOf(' ');

            if (sp > 0)
            {
                if (String.Compare(cmd, sp + 1, "PRIVMSG ", 0, 8, true) == 0)
                {
                    int iBang = -1;

                    if (cmd[0] == ':')
                        iBang = cmd.IndexOf('!');

                    if (iBang > 1)
                        roller = cmd.Substring(1, iBang - 1);
                    else
                        roller = "Player";

                    int colon = cmd.IndexOf(':', sp + 9);
                    if (colon > 0)
                    {
                        origin = cmd.Substring(sp + 9, colon - sp - 10);
                        if (origin == "__")
                            origin = roller;
                        cmd = cmd.Substring(colon + 1);

                        Bot bot = new Bot(roller, origin);
                        
                        bot.RunCmd(cmd);
                    }
                }
            }
        }

        static void SaveIfNeeded()
        {
            GameHost.Worker.AutoSave();
        }

        internal void RunCmdConsole(String cmd)
        {
            try
            {
                ParseBotCommand(cmd);
            }
            catch (Exception e)
            {
                sbOut = new StringBuilder();
                sbOut.AppendFormat("gamebot: error processing command {0}\ngamebot: {1}\n", cmd, e.Message);
                Console.WriteLine(e.ToString());
            }
            Console.WriteLine(sbOut.ToString());
        }

        internal string RunCmd(String cmd)
        {
            try
            {
                ParseBotCommand(cmd);
            }
            catch (Exception e)
            {
                sbOut = new StringBuilder();
                sbOut.AppendFormat("gamebot: error processing command {0}\ngamebot: {1}\n", cmd, e.Message);
                Console.WriteLine(e.ToString());
            }
            var result = sbOut.ToString().Replace("\r", "");
            SendBufferToIrc(result);

            if (fScanForOwnage)
            {
                var killcounts = GameHost.Program.killcounts;

                lock (killcounts)
                {
                    int killcount = 0;

                    if (killcounts.ContainsKey(roller))
                    {
                        killcount = killcounts[roller];
                    }

                    if (killcount > 0 && result.Contains(" Special!"))
                    {
                        killcount++;
                        GameHost.Program.Broadcast(origin, String.Format("audio ownage {0}", killcount));
                    }
                    else if (result.Contains(" Critical!"))
                    {
                        killcount++;
                        GameHost.Program.Broadcast(origin, String.Format("audio ownage {0}", killcount));
                    }
                    else if (result.Contains(" Fumble!"))
                    {
                        killcount = 0;
                        GameHost.Program.Broadcast(origin, "audio fumble 0");
                    }
                    else
                    {
                        if (killcount > 0)
                            killcount--;
                    }

                    killcounts.Remove(roller);
                    killcounts.Add(roller, killcount);
                }
            }

            return result;
        }

        public static void Send(String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // broadcast could come from another thread
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

        public void SendBufferToIrc(String buf)
        {
            if (origin == "#null")
                return;

            String[] lines = buf.Split(newline);

            for (int i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && lines[i].Length == 0)
                    continue;

                Send(":__ PRIVMSG " + origin + " :\x0001ACTION " + lines[i] + "\x0001\n");
            }
        }

        public void SendBufferWrapped(String buf)
        {
            const int lineMax = 100;

            String[] lines = buf.Replace("\r", "").Split(newline);

            int lastLine = lines.Length;
            while (--lastLine >= 0)
            {
                var line = lines[lastLine];

                if (line == "" || line == ".")
                    continue;

                break;
            }

            for (int i = 0; i <= lastLine; i++)
            {
                var line = lines[i];

                if (line.Length == 0)
                {
                    sbOut.AppendLine();
                    continue;
                }

                for (;;)
                {
                    if (line.Length == 0)
                    {
                        break;
                    }

                    if (line.Length <= lineMax)
                    {
                        sbOut.AppendLine(line);
                        break;
                    }

                    int j = lineMax;
                    for (; j >= 25; j--)
                    {
                        if (line[j] == ' ')
                        {
                            break;
                        }
                    }

                    if (j <= 25)
                    {
                        j = line.IndexOf(' ', 25);
                        if (j < 0 || j > 160)
                        {
                            sbOut.AppendLine(line.Substring(0, lineMax));
                            break;
                        }
                    }

                    sbOut.AppendLine(line.Substring(0, j));
                    line = line.Substring(j+1);
                }
            }
        }

        
        class ParseException : Exception
        {
            public ParseException()
                : base("Syntax Error")
            {
            }

            public ParseException(String s)
                : base(s)
            {
            }
        }

        enum Evalmode { normal, min, max };

        Random r = new Random(unchecked((int)(stopwatch.ElapsedTicks ^ (stopwatch.ElapsedTicks >> 32))));
        string rollText;
        int idx;
        String peekToken;
        StringBuilder sb1 = new StringBuilder();
        StringBuilder sb2 = new StringBuilder();
        bool fSuppressTotal = false;
        String totalName = "Total";
        String roller = "Player";
        String origin = "#gameroom";
        bool fTerse = false;
        Evalmode eval_mode;
        StringBuilder sbOut = null;
        Dictionary<string,string> hashArgs;
        List<string> listArgs;
        bool fScanForOwnage = false;
        string currentCmd;

        void ParseBotCommand(String s)
        {
            string args = null;
            fScanForOwnage = false;

            int i = s.IndexOf(' ');
            if (i <= 0)
            {
                currentCmd = s;
                args = "";
            }
            else
            {
                currentCmd = s.Substring(0, i);
                args = s.Substring(i + 1);
            }

            if (args == null)
                args = "";

            if (currentCmd[0] != '!')
                return;

            if (DateTime.UtcNow < s_wakeUp && currentCmd != "!sleep")
            {
                sbOut.AppendFormat("Sorry, I'm sleeping for {0} more seconds.\n", (s_wakeUp.Ticks - DateTime.UtcNow.Ticks)/10000000);
                return;
            }

            switch (currentCmd)
            {
                case "!login":
                    sbOut.AppendLine("You don't have to log in, the bot is always listening.");
                    return;

                case "!echo":
                    sbOut.AppendLine(args);
                    return;

                case "!savenow":
                    ForceSave();
                    sbOut.AppendLine("Party saved and archived.");
                    return;

                case "!rebootnow":
                    ForceSave();

                    System.Diagnostics.Process.Start("shutdown", "/r /t 0");
                    sbOut.AppendLine("Party saved and archived.  Restarting.");
                    return;

                case "!sleep":
                    DoSleep(args);
                    sbOut.AppendFormat("Sleeping for {0} seconds.\n", (s_wakeUp.Ticks - DateTime.UtcNow.Ticks) / 10000000);
                    return;

                case "!help":
                    exec_help(args);
                    return;

                case "!box":
                    exec_box(args);
                    return;

                case "!form":
                case "!forms":
                    exec_form(args);
                    return;

                case "!roll":
                    fScanForOwnage = true;
                    PrintRoll(args);
                    return;

                case "!atk":
                    fScanForOwnage = true;
                    DoAttack(args);
                    return;

                case "!loc":
                    if (args.Length == 0)
                        PrintRoll("loc");
                    else
                        PrintRoll("loc_" + args);
                    return;

                case "!fumble":
                    if (args.Length == 0)
                        PrintRoll("fumble");
                    else
                        PrintRoll("fumble_" + args);
                    return;

                case "!":
                    sbOut.AppendLine("OMG!!!");
                    return;

                case "!d5":
                    dumpCommandHelp();
                    return;

                case "!d100":
                    dumpCommandHelp();
                    return;

                case "!nick":
                    GameHost.Program.Broadcast(origin, "audio fumble 0");
                    dumpCommandHelp();
                    return;

                case "!pct":
                    fScanForOwnage = true;
                    PrintRoll(args + "%");
                    return;

                case "!cpct":
                    fScanForOwnage = true;
                    PrintRoll(args + "$");
                    return;

                case "!Gpct":
                    fTerse = true;
                    gpct_cmd(args);
                    return;

                case "!gpct":
                    gpct_cmd(args);
                    return;

                case "!gbest":
                    gbest_cmd(args);
                    return;

                case "!groll":
                    groll_cmd(args);
                    return;

                case "!routine":
                    routine_cmd(args);
                    return;

                case "!task":
                    task_cmd(args);
                    return;

                case "!groutine":
                    groutine_cmd(args);
                    return;

                case "!camp":
                    camp_cmd(args);
                    return;

                case "!chk":
                    exec_chk(args);
                    return;

                case "!pow":
                    fScanForOwnage = true;
                    PrintRoll(args + "@");
                    return;

                case "!train":
                    exec_train(args);
                    return;

                case "!Train":
                    fTerse = true;
                    exec_train(args);
                    return;

                case "!Trn":
                    fTerse = true;
                    train_skill(args);
                    return;

                case "!trn":
                    train_skill(args);
                    return;

                case "!Ktrn":
                    fTerse = true;
                    train_ki(args);
                    return;

                case "!ktrn":
                    train_ki(args);
                    return;

                case "!Mtrn":
                    fTerse = true;
                    train_mysticism(args);
                    return;

                case "!mtrn":
                    train_mysticism(args);
                    return;

                case "!hours":
                    SetRemainingHours(eval_roll(args), roller);
                    return;

                case "!Atrn":
                    fTerse = true;
                    train_stat(args);
                    return;

                case "!atrn":
                    train_stat(args);
                    return;

                case "!Rpt":
                    fTerse = true;
                    exec_rpt(args);
                    return;

                case "!rpt":
                    exec_rpt(args);
                    return;

                case "!Sell":
                    fTerse = true;
                    exec_sell(args);
                    return;

                case "!sell":
                    exec_sell(args);
                    return;

                case "!Buy":
                    fTerse = true;
                    exec_buy(args);
                    return;

                case "!buy":
                    exec_buy(args);
                    return;

                case "!Gcmd":
                    fTerse = true;
                    exec_gcmd(args);
                    return;

                case "!gcmd":
                    exec_gcmd(args);
                    return;

                case "!audio":
                    GameHost.Program.Broadcast("#gameroom", String.Format("audio {0}", args));
                    return;

                case "!insult":
                    exec_insult();
                    return;

                case "!jezra":
                    exec_jezra();
                    return;

                case "!wwes":
                    exec_wwes();
                    return;
              
                case "!etrigan":
                    exec_etrigan(false, args);
                    return;

                case "!Etrigan":
                    exec_etrigan(true, args);
                    return;

                case "!Rcmd":
                    fTerse = true;
                    exec_rcmd(args);
                    return;

                case "!rcmd":
                    exec_rcmd(args);
                    return;

                case "!summon":
                    exec_summon(args);
                    return;

                case "!Summon":
                    fTerse = true;
                    exec_summon(args);
                    return;

                case "!Sc":
                    fTerse = true;
                    exec_spirit_combat(args);
                    return;

                case "!sc":
                    exec_spirit_combat(args);
                    return;

                case "!ssc":
                    exec_spirit_combat_one_attack(args);
                    return;

                case "!choose":
                    fTerse = true;
                    exec_victim(args);
                    return;

                case "!suckstobeyou":
                case "!victim":
                    exec_victim(args);
                    return;

                case "!f":
                    if (args == "")
                    {
                        dumpCommandHelp();
                        return;
                    }

                    exec_find("top:5 party "+args);
                    return;

                case "!find":
                    exec_find(args);
                    return;

                case "!icons":
                    var b = new StringBuilder();
                    GameHost.Worker.find_party_icons("foo", b, origin);
                    return;

                case "!speak":
                    // GameHost.Program.Broadcast(origin, String.Format("audio speak {0}", args));
                    return;

                case "!party":
                    exec_party(args);
                    return;

                case "!check":
                    exec_check(args);
                    return;

                case "!checks":
                    exec_checks(args);
                    return;

                case "!wound":
                case "!wounds":
                    exec_wound(args);
                    return;

                case "!mana":
                    exec_mana(args);
                    return;

                case "!fat":
                case "!fatigue":
                    exec_fatigue(args);
                    return;

                case "!used":
                    exec_use("!use", args);
                    return;

                case "!use":
                case "!gain":
                    exec_use(currentCmd, args);
                    return;

                case "!pressence":
                    sbOut.AppendLine("There is only one 's' in presence.");
                    return;

                case "!round":
                    exec_round(args);
                    return;

                case "!sr0":  case "!sr1":  case "!sr2":  case "!sr3":  case "!sr4":  case "!sr5":  
                case "!sr6":  case "!sr7":  case "!sr8":  case "!sr9":  case "!sr10":
                    args = currentCmd.Substring(3);
                    currentCmd = "sr";
                    exec_sr(args);
                    return;

                case "!sr":
                    exec_sr(args);
                    return;

                case "!gameaid":
                    exec_gameaid(args);
                    return;

                case "!who":
                    exec_who(args);
                    return;

                case "!pc":
                    exec_pc(args);
                    return;

                case "!npc":
                    exec_npc(args);
                    return;

                case "!buffs":
                    exec_buffs(args);
                    return;

                case "!Buff":
                    fTerse = true;
                    exec_buff(args);
                    return;
                
                case "!buff":
                    exec_buff(args);
                    return;

                case "!sheetbuff":
                    exec_sheetbuff(args);
                    return;

                case "!clearsheetbuffs":
                    ClearSheetBuffs(args);
                    return;

                case "!gbuff":
                    exec_gbuff(args);
                    return;

                case "!events":
                    exec_events(args);
                    return;

                case "!event":
                    exec_event(args);
                    return;

                case "!notes":
                    exec_notes(args);
                    return;

                case "!note":
                    exec_note(args);
                    return;

                case "!todos":
                    exec_todos(args);
                    return;

                case "!todo":
                    exec_todo(args);
                    return;

                case "!shugenja":
                    exec_shugenja(args);
                    return;

                case "!runemagic":
                    exec_runemagic(args);
                    return;

                case "!spiritmana":
                    exec_spiritmana(args);
                    return;

                case "!presence":
                    exec_presence(args);
                    return;

                case "!loot":
                case "!loots":
                    exec_loot(args);
                    return;

                case "!remaining":
                    sbOut.AppendFormat("remaining hours for {0}: {1}", roller, GetRemainingHours(roller));
                    return;
            }

            sbOut.AppendFormat("gamebot: {0} unknown command\n", currentCmd);
        }

        private void exec_insult()
        {
            string r1, r2, r3;
            insult(out r1, out r2, out r3);

            string result = String.Format("{0} {1} {2}", r1, r2, r3);

            sbOut.AppendLine(result);
        }

        string pick_one(string s)
        {
            string[] a1 = s.Split(comma);
            return a1[eval_roll("1d" + a1.Length.ToString() + "-1")];
        }

        private void insult(out string r1, out string r2, out string r3)
        {
            pick_three(
                "Atomic,Steamy,Rusty,Witless,Lumpy,Shitty,Moist,Chunky,Lousy,Bulbous,Trashy,Dumbass,Nerdy,Dotarded,Crusty,Brainless",
                "knob,bum, turd,prick,bulge,ass,chut,shit,rod,chode,fuck,weiner,jizz,panty,cock,dong",
                "vacuum,general,gremlin,pixie,spasm,fiend,fungus,tunnel,corporal,raider,demon,buccaneer,tyrant,juggler,magician,fiddle",
                out r1, out r2, out r3);
        }

        private void pick_three(string s1, string s2, string s3, out string r1, out string r2, out string r3)
        {
            r1 = pick_one(s1);
            r2 = pick_one(s2);
            r3 = pick_one(s3);            
        }

        private void exec_jezra()
        {
            string r1, r2, r3;
            insult(out r1, out r2, out r3);

            int r = eval_roll("1d3");

            if (r2 != "fuck") switch (r)
            {
                case 1: r1 = "Fucking"; break;
                case 2: r2 = "fuck"; break;
                case 3: r3 = "fuck"; break;
            }

            string result = r1 + " " + r2 + " " + r3;

            sbOut.AppendLine(result);
        }

        private void exec_wwes()
        {
            wwes_helper(true, roller);
        }

        private void exec_etrigan(bool emote,string args)
        {
            ParseToMap(args);

            if (!hashArgs.ContainsKey("cocky")) 
            {
                wwes_helper(emote, "Etrigan");
            }
            else
            {
                string r1 = pick_one("I end you,Be purged,Suffer your fate,I cleanse you");
                string r2 = pick_one("in,by,with");
                string r3 = pick_one("name,fire,flames,power");
                string r4 = pick_one("slime,servant,abomination,demon,devil,profanity");
                string r5 = pick_one("Chaos,The One Foe,The True Enemey");

                string result = String.Format("Etrigan says, \"{0} {1} the {2} of Oakfed {3} of {4}!\"", r1, r2, r3, r4, r5);
                sbOut.AppendLine(result);
            }
        }

        private void wwes_helper(bool emote, string who)
        {
            string r1, r2, r3;
            pick_three("Great,Mighty,Tremendous,Immense,Amazing,Astounding,Colossal,Formidable,Marvelous,Monumental",
                "Herald,Emperor,Defender,Overlord,Spirit,Kami,Lord,Sultan,Prophet,Guardian,Champion",
                "Peace,Order,the Meek,Justice,Wisdom,Understanding,Truth,Retribution,Wrath,Vengeance",
                out r1, out r2, out r3);

            string result;

            if (emote)
            {
                string r0 = pick_one("kneels,prostrates himself,bows,grovels,bows humbly,humbles himself,abases himself,cowers,bows down");
                result = string.Format("{4} {0} and says, \"O {1} {2} of {3}\"", r0, r1, r2, r3, who);
            }
            else
            {
                result = string.Format("{3} says, \"O {0} {1} of {2}\"", r1, r2, r3, who);
            }

            sbOut.AppendLine(result);
        }


        private void exec_form(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 1 || listArgs.Count >2)
            {
                dumpCommandHelp();
                return;
            }

            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            string folder = who + "/_forms";

            var forms = GameHost.Worker.readallsubkeys(folder);

            for (int i = 0; i < forms.Count; i++ )
            {
                forms[i] = forms[i].Substring(folder.Length + 1);
            }

            if (listArgs.Count == 1)
            {
                var current = GameHost.Worker.readkey(folder, "current");

                if (current != null)
                {
                    sbOut.AppendFormat("{0} current form: {1}\n", who, current);
                }
                else
                {
                    sbOut.AppendFormat("{0} current form: {1}\n", who, "default");
                }

                sbOut.AppendFormat("available forms:\n");

                foreach (var form in forms)
                {
                    sbOut.AppendFormat("- {0}\n", form);
                }

                if (forms.Count == 0)
                {
                    sbOut.AppendFormat("- no alternate forms\n");
                }
            }

            if (listArgs.Count == 2)
            {
                var form = listArgs[1];

                if (!forms.Contains(form) && form != "none")
                {
                    sbOut.AppendFormat("{0} does not have form {1}\n", who, form);
                    return;
                }

                ClearFormBuffs(who);

                if (form == "none")
                    return;

                int buff_number = 99000;

                var dir = who + "/_forms/" + form;

                buff_number = EmitFormBuffs(who, buff_number, form, "");
                buff_number = EmitFormBuffs(who, buff_number, form, "agility");
                buff_number = EmitFormBuffs(who, buff_number, form, "communication");
                buff_number = EmitFormBuffs(who, buff_number, form, "knowledge");
                buff_number = EmitFormBuffs(who, buff_number, form, "alchemy");
                buff_number = EmitFormBuffs(who, buff_number, form, "magic");
                buff_number = EmitFormBuffs(who, buff_number, form, "manipulation");
                buff_number = EmitFormBuffs(who, buff_number, form, "perception");
                buff_number = EmitFormBuffs(who, buff_number, form, "stealth");
                buff_number = EmitFormBuffs(who, buff_number, form, "attack");
                buff_number = EmitFormBuffs(who, buff_number, form, "parry");

                GameHost.Worker.note3(who + "/_forms", "current", form);

                sbOut.AppendFormat("{0} is now in {1} form.", who, form);
            }
            
        }

        int EmitFormBuffs(string who, int buff_number, string form, string subdir)
        {
            string buffSource = who + "/_forms/" + form;

            if (subdir != "")
                buffSource += "/" + subdir;

            string buffCategory = subdir;
            string buffType = "";

            if (subdir == "attack" || subdir == "parry")
            {
                buffCategory = "_wpn";
                buffType = "/" + subdir;
            }

            if (buffCategory != "")
                buffCategory += "/";


            var buffs = GameHost.Worker.readallkeys(buffSource);

            foreach (var buff in buffs)
            {
                string amount = GameHost.Worker.readkey(buffSource, buff);

                if (amount == "0")
                    continue;

                string buffkey = String.Format("{0}|{1}", who, buff_number++);

                if (!amount.StartsWith("+") && !amount.StartsWith("-"))
                    amount = "+" + amount;

                if (subdir != "")
                {
                    string dir = who + "/" + subdir;
                    string key = buff;

                    if (subdir == "attack" || subdir == "parry")
                    {
                        dir = who + "/_wpn/" + buff;
                        key = subdir;
                    }

                    var val = GameHost.Worker.readkey(dir, key);
                    if (val == null || val == "")
                    {
                        GameHost.Worker.note3(dir, key, "0");
                    }
                }

                GameHost.Worker.note3("_buffs", buffkey, buffCategory + buff + buffType + amount);

                sbOut.AppendFormat("buff {0} {1}\n", buffkey, buffCategory + buff + buffType + amount);               
            }

            return buff_number;
        }

        void ClearSheetBuffs(string who)
        {
            var buffs = findstuff(who, "_buffs");

            var prefix = who + "|98";

            foreach (var buff in buffs)
            {
                if (buff.key.StartsWith(prefix))
                {
                    GameHost.Worker.del_key("_buffs", buff.key);
                }
            }
        }

        void ClearFormBuffs(string who)
        {

            var buffs = findstuff(who, "_buffs");

            var prefix = who + "|99";

            foreach (var buff in buffs)
            {
                if (buff.key.StartsWith(prefix))
                {
                    GameHost.Worker.del_key("_buffs", buff.key);

                    var effectiveKey = who + "/" + buff.val;
                    int index = effectiveKey.IndexOfAny(GameHost.Dict.plusminus);
                    if (index > 0)
                    {
                        effectiveKey = effectiveKey.Substring(0, index);

                        int last = effectiveKey.LastIndexOf('/');
                        var dir = effectiveKey.Substring(0, last);
                        var key = effectiveKey.Substring(last + 1);

                        string amount = GameHost.Worker.readkeyRaw(dir, key);

                        if (amount == "0")
                        {
                            GameHost.Worker.del_key(dir, key);
                        }
                    }
                }
            }

            GameHost.Worker.del_key(who + "/_forms", "current");
        }

        private static void ForceSave()
        {
            DateTime now = DateTime.Now;
            GameHost.Worker.SaveParty(now);
            GameHost.Worker.SaveArchive(now);
        }

        void exec_box(string s)
        {
            ParseToMap(s);

            if (listArgs.Count != 3)
            {
                dumpCommandHelp();
                return;
            }

            double d1, d2, d3;

            if (!Double.TryParse(listArgs[0], out d1) || !Double.TryParse(listArgs[1], out d2) || !Double.TryParse(listArgs[2], out d3))
            {
                dumpCommandHelp();
                return;
            }

            double area =  d1 * d2 +  d2 * d3 + d1 * d3;
            area *= 2;

            double magicratio = 4.266666667;

            sbOut.AppendFormat("Input dimensions: {0} by {1} by {2} (in feet).\r\nBox surface area: {3} square feet.\r\nEquivalent SIZ {4:f2}.", d1, d2, d3, area, area / magicratio);
        }

        void exec_round(string args)
        {
            if (args == null || args.Length == 0)
            {
                dumpCommandHelp();
                return;
            }

            GameHost.Worker.note3("_gameaid/_remote", origin + "|round", args);
            sbOut.AppendLine("Round " + args + "!");
            exec_sr("0");
        }

        void exec_sr(string args)
        {
            switch (args)
            {
                case "0":
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9":
                case "10":
                    GameHost.Worker.note3("_gameaid/_remote", origin + "|sr", args);
                    sbOut.AppendLine("It's now SR" + args);
                    break;

                default:
                    dumpCommandHelp();
                    return;
            }
        }

        void exec_chk(string args)
        {
            string[] arga = args.Split(comma);

            if (arga.Length != 2)
            {
                dumpCommandHelp();
                return;
            }

            eval_roll(arga[0] + "^" + arga[1]);
            sbOut.AppendFormat("{0}: {1}\n", roller, sb2);
        }

        void DoSleep(string args)
        {
            int result;
            Int32.TryParse(args, out result);

            if (result > 0)
            {
                s_wakeUp = DateTime.UtcNow.AddSeconds(result);
            }
        }

        void DoAttack(string args)
        {
            ParseToMap(args);

            var pct = StringArg("pct");
            var dmg = StringArg("dmg");
            var idmg = StringArg("idmg");
            var wpn = StringArg("wpn");
            var loc = StringArg("loc");

            if (pct == "" || dmg == "" || idmg == "" || wpn == "" || loc == "")
            {
                dumpCommandHelp();
                return;
            }

            int result = PrintRoll(pct + "%");
            if (result <= 0)
                return;

            if (result > 1)
                PrintRoll(idmg);
            else
                PrintRoll(dmg);

            PrintRoll("loc_" + loc);
        }

        void exec_gameaid(string s)
        {
            ParseToMap(s);

            if (listArgs.Count != 2)
            {
                dumpCommandHelp();
                return;
            }

            if (listArgs[0] == "loc" || listArgs[0] == "map")
            {
                GameHost.Worker.note3("_gameaid/_remote", origin + "|" + listArgs[0], listArgs[1]);
            }
        }

        internal class NoteMeta
        {
            public string Title;
            public string Folder;
            public string Command;
            public string DefaultWho = "general";
            public bool AcceptAnyWho = false;
            public int StartId = 1;

            public string who = null;
            public string whoPc = null;
        }

        void exec_note(string s)
        {
            exec_genericNote(s, NoteTypeNote());
        }

        void exec_notes(string s)
        {
            exec_genericNoteList(s, NoteTypeNote());
        }

        void exec_pc(string s)
        {
            exec_genericNote(s, NoteTypePc());
        }

        void exec_npc(string s)
        {
            exec_genericNote(s, NoteTypeNpc());
        }

        void exec_who(string s)
        {
            var n = NoteTypeWho();
            exec_genericNoteList(s, n);

            if (n.whoPc != null)
            {
                var x = FormatDossier(n);

                sbOut.AppendLine(x);
            }
        }

        internal static string FormatDossier(NoteMeta n)
        {
            StringBuilder b = new StringBuilder();
            GameHost.Worker.AddSyntheticForOnePerson(b, "", n.whoPc);
            var x = b.ToString();

            x = x.Replace("v  " + n.whoPc, n.whoPc + " ");
            x = x.Replace("|skill|", "");
            x = x.Replace("|mana|", "Mana: ");
            x = x.Replace("|religion", "Religion:");
            x = x.Replace("|abilities", "Abilities:");
            x = x.Replace("|life", "Life:");
            x = x.Replace("|species", "Species:");
            x = x.Replace("|player", "Player:");
            x = x.Replace("|combat1 ", "");
            x = x.Replace("|combat2 ", "");
            x = x.Replace("|combat3 ", "");
            x = x.Replace("|combat4 ", "");
            x = x.Replace("|combat5 ", "");
            x = x.Replace("|combat6 ", "");
            x = x.Replace("|combat7 ", "");
            x = x.Replace("|combat8 ", "");
            x = x.Replace("|combat9 ", "");
            x = x.Replace("|average_armor", "Average Armor:");
            return x;
        }

        void exec_gbuff(string s)
        {
            foreach (string p in getpresent())
            {
                roller = p;

                try
                {
                    exec_genericNote("@"+p + " " + s, NoteTypeBuff());
                }
                catch 
                {

                }
            }
        }

        void exec_buff(string s)
        {
            exec_genericNote(s, NoteTypeBuff());
        }

        void exec_sheetbuff(string s)
        {
            fTerse = true;
            exec_genericNote(s, NoteTypeSheetBuff());
        }
        
        void exec_buffs(string s)
        {
            exec_genericNoteList(s, NoteTypeBuff());
        }

        void exec_event(string s)
        {
            if (s.StartsWith("party "))
            {
                s = getpartydate() + " " + getpartytag() + s.Substring(6);
            }

            exec_genericNote(s, NoteTypeEvent());
        }

        void exec_events(string s)
        {
            exec_genericNoteList(s, NoteTypeEvent());
        }

        void exec_todo(string s)
        {
            exec_genericNote(s, NoteTypeTodo());
        }

        void exec_todos(string s)
        {
            exec_genericNoteList(s, NoteTypeTodo());
        }
        internal static NoteMeta NoteTypeNote()
        {
            var n = new NoteMeta();
            n.Command = "note";
            n.Folder = "_note";
            n.Title = "Notes";
            return n;
        }

        internal static NoteMeta NoteTypeTodo()
        {
            var n = new NoteMeta();
            n.Command = "todo";
            n.Folder = "_todo";
            n.Title = "Todos";
            return n;
        }

        internal static NoteMeta NoteTypeEvent()
        {
            var n = new NoteMeta();
            n.Command = "event";
            n.Folder = "_event";
            n.Title = "Events";
            return n;
        }

        internal static NoteMeta NoteTypeBuff()
        {
            var n = new NoteMeta();
            n.Command = "buff";
            n.Folder = "_buffs";
            n.DefaultWho = "me";
            n.Title = "Buffs";
            return n;
        }

        internal static NoteMeta NoteTypeSheetBuff()
        {
            var n = new NoteMeta();
            n.Command = "buff";
            n.Folder = "_buffs";
            n.DefaultWho = "me";
            n.Title = "Buffs";
            n.StartId = 98000;
            return n;
        }

        internal static NoteMeta NoteTypePc()
        {
            var n = new NoteMeta();
            n.Command = "pc";
            n.Folder = "_who";
            n.DefaultWho = null;
            n.Title = "PC Info";

            return n;
        }

        internal static NoteMeta NoteTypeNpc()
        {
            var n = new NoteMeta();
            n.Command = "npc";
            n.Folder = "_who";
            n.AcceptAnyWho = true;
            n.DefaultWho = null;
            n.Title = "NPC Info";

            return n;
        }

        internal static NoteMeta NoteTypeWho()
        {
            var n = new NoteMeta();
            n.Command = "who";
            n.Folder = "_who";
            n.AcceptAnyWho = true;
            n.DefaultWho = "all";
            n.Title = "PC and NPC Information";

            return n;
        }

        void exec_genericNoteList(string s, NoteMeta n)
        {
            ParseToMap(s, s_simpleArgs);

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            if (!GetAtStyleWho(n))
            {
                dumpCommandHelp();
                return;
            }

            List<StringPair> results = findstuff(n.who, n.Folder);

            if (n.Command == "event")
            {
                sortEvents(results);
            }

            List<string> match = new List<string>();
            List<string> reject = new List<string>();
            getMatchAndReject(match, reject);

            filterResults(results, match, reject, n);

            printResults(results);
        }       
        
        static void filterResults(List<StringPair> results, List<string> match, List<string> reject, NoteMeta n)
        {
            Regex[] reMatch;
            Regex[] reReject;
            
            try
            {
                // there could be syntax issues with the regular expression, if there are, then 
                // we will treat it as though there are no matches
                reMatch = ConvertToRegex(match);
                reReject = ConvertToRegex(reject);
            }
            catch
            {
                results.Clear();
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                var val = results[i].val;

                if (n.Folder == "_who")
                {
                    val = results[i].key + " " + val;
                }

                int j;
                for (j = 0; j < match.Count; j++)
                {
                    if (!reMatch[j].IsMatch(val))
                        break;
                }

                if (j < match.Count)
                {
                    results.RemoveAt(i);
                    i--;
                    continue;
                }

                for (j = 0; j < reject.Count; j++)
                {
                    if (reReject[j].IsMatch(val))
                        break;
                }

                if (j < reject.Count)
                {
                    results.RemoveAt(i);
                    i--;
                    continue;
                }
            }
        }

        static Regex[] ConvertToRegex(List<string> strings)
        {
            Regex[] exprs = new Regex[strings.Count];
            for (int i = 0; i < strings.Count; i++)
            {
                exprs[i] = new Regex(strings[i], RegexOptions.IgnoreCase);
            }

            return exprs;
        }

        void getMatchAndReject(List<string> match, List<string> reject)
        {
            for (int i = 1; i < listArgs.Count; i++)
            {
                if (listArgs[i].StartsWith("-"))
                    reject.Add(listArgs[i].Substring(1));
                else
                    match.Add(listArgs[i]);
            }
        }

        bool GetAtStyleWho(NoteMeta n)
        {
            string who = null;

            if (!listArgs[0].StartsWith("@"))
            {
                if (n.DefaultWho == null)
                {
                    n.who = null;
                    return false;
                }

                // add the arg so the rest of the code is uniform
                listArgs.Insert(0, "@" + n.DefaultWho);
                who = n.DefaultWho;
            }

            string whoArg = listArgs[0].Substring(1);
            who = FindWho(whoArg, fSilent: n.AcceptAnyWho);

            if (who != null)
                n.whoPc = who;

            if (who == null && n.AcceptAnyWho)
                who = whoArg;

            n.who = who;
            return who != null;
        }

        bool IsValidDate(string p)
        {
            int day = 0;
            int month = 0;
            int year = 0;
            int slashes = 0;
            foreach (char c in p)
            {
                if (c == '/')
                {
                    slashes++;
                    if (slashes > 2)
                        return false;

                    if (slashes == 1 && (month < 1 || month > 6))
                        return false;

                    if (slashes == 2 && (day < 1 || day > 56))
                        return false;
                }
                else if (c >= '0' && c <= '9')
                {
                    switch (slashes)
                    {
                        case 0:
                            month = month * 10 + c - '0';
                            break;
                        case 1:
                            day = day * 10 + c - '0';
                            break;
                        case 2:
                            year = year * 10 + c - '0';
                            break;
                    }
                }
                else
                    return false;
            }

            if (slashes < 2)
                return false;

            if (year != 0)
                if (year < 1500 || year > 1650)
                    return false;

            if (month == 6 && day > 14)
                return false;

            if (year == 0)
                listArgs[1] = String.Format("{0}/{1}/0000", month, day);
            else
                listArgs[1] = String.Format("{0}/{1}/{2}", month, day, year);
            return true;
        }

        void printResults(List<StringPair> results)
        {
            var prevroller = "";

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        sbOut.AppendFormat("====================\n");
                    }
                }

                prevroller = roller;
                sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " id:"), result.val);
            }
        }

        void sortEvents(List<StringPair> results)
        {
            results.Sort((StringPair p1, StringPair p2) =>
            {
                string ch1, id1;
                string ch2, id2;
                GameHost.Worker.Parse2Ex(p1.key, out ch1, out id1, "|");
                GameHost.Worker.Parse2Ex(p2.key, out ch2, out id2, "|");

                int icmp = String.Compare(ch1, ch2, true);
                if (icmp != 0) return icmp;

                string d1, d2, t;
                GameHost.Worker.Parse2(p1.val, out d1, out t);
                GameHost.Worker.Parse2(p2.val, out d2, out t);

                string day1, m1, y1, day2, m2, y2;
                GameHost.Worker.Parse3Ex(d1, out m1, out day1, out y1, "/");
                GameHost.Worker.Parse3Ex(d2, out m2, out day2, out y2, "/");

                int v1, v2;

                v1 = v2 = 0;
                Int32.TryParse(y1, out v1);
                Int32.TryParse(y2, out v2);
                if (v1 < v2) return -1;
                if (v1 > v2) return 1;

                v1 = v2 = 0;
                Int32.TryParse(m1, out v1);
                Int32.TryParse(m2, out v2);
                if (v1 < v2) return -1;
                if (v1 > v2) return 1;

                v1 = v2 = 0;
                Int32.TryParse(day1, out v1);
                Int32.TryParse(day2, out v2);
                if (v1 < v2) return -1;
                if (v1 > v2) return 1;

                return String.Compare(id1, id2);
            });           
        }
        
        void exec_genericNote(string s, NoteMeta n)
        {
            ParseToMap(s, s_simpleArgs);

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");

            if (!GetAtStyleWho(n))
            {
                dumpCommandHelp();
                return;
            }

            string id = StringArg("id");

            if (fRemove == true && id == "" && listArgs.Count > 1)
            {
                List<StringPair> results = findstuff(n.who, n.Folder);
                foreach (var r in results)
                {
                    if (r.val == listArgs[1] || listArgs[1] == "all" || 
                        (n.Command == "buff" && !listArgs[1].Contains("-") && !listArgs[1].Contains("+") && r.val.StartsWith(listArgs[1])))
                            genericNote_remove(n.who, r.key, r.val, n);
                }
            }
            else if (id != "")
            {
                genericNote_update(fRemove, n.who, id, n);
            }
            else if (listArgs.Count == 1)
            {
                exec_genericNoteList(listArgs[0], n);
            }
            else
            {
                genericNote_new(n.who, fRemove, n);
            }
        }

        void genericNote_new(string who, bool fRemove, NoteMeta n)
        {
            if (fRemove)
            {
                sbOut.AppendFormat("Can't remove without specifying id:xxx.");
                return;
            }

            if (n.Command == "event")
            {
                if (listArgs.Count < 3)
                {
                    sbOut.AppendFormat("Must specify event date and description");
                    return;
                }

                if (!IsValidDate(listArgs[1]))
                {
                    sbOut.AppendFormat("{0} does not look like a date", listArgs[1]);
                    return;
                }
            }

            string id = getavailableid(who, n.Folder, n.StartId);

            string logicalKey = who + "|" + id;
            string value = AppendLooseArgs().ToString().Trim();

            if (value == "")
            {
                sbOut.AppendFormat("Some {0} text is required.", n.Command);
                return;
            }

            if (!fTerse)
            {
                sbOut.AppendFormat("{0}: @{1} id:{2} {3}\n", n.Command, who, id, value);
            }
            GameHost.Worker.note3(n.Folder, logicalKey, value);
        }

        void genericNote_update(bool fRemove, string who, string id, NoteMeta n)
        {
            var logicalKey = who + "|" + id;

            var oldValue = readkey(n.Folder, logicalKey);

            if (oldValue == null)
            {
                sbOut.AppendFormat("No such {0} was found.", n.Command);
                return;
            }

            if (fRemove)
            {
                if (listArgs.Count > 1)
                {
                    sbOut.AppendFormat("Do not include a description with remove:yes.");
                    return;
                }

                genericNote_remove(who, logicalKey, oldValue, n);
                return;
            }

            genericNote_update(who, logicalKey, oldValue, n);
        }

        void genericNote_update(string who, string logicalKey, string oldValue, NoteMeta n)
        {
            if (n.Command == "event")
            {
                if (!IsValidDate(listArgs[1]))
                {
                    sbOut.AppendFormat("{0} does not look like a date", listArgs[1]);
                    return;
                }
            }
            
            string newItem = AppendLooseArgs().ToString();

            GameHost.Worker.note3(n.Folder, logicalKey, newItem);
            if (!fTerse)
            {
                sbOut.AppendFormat("{0} updated: {1} {2}\n", n.Command, logicalKey.Replace("|", " id:"), newItem);
            }
        }

        void genericNote_remove(string who, string logicalKey, string oldValue, NoteMeta n)
        {
            GameHost.Worker.del_key(n.Folder, logicalKey);
            if (!fTerse)
            {
                sbOut.AppendFormat("{0} removed: {1} {2}\n", n.Command, logicalKey.Replace("|", " id:"), oldValue);
            }
        }

        void exec_presence(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0 && hashArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");
            string spell = StringArg("spell");

            if (spell != "")
            {
                presence_update(fRemove, spell);
            }
            else if (listArgs.Count == 1)
            {
                presence_list(fRemove);
            }
            else
            {
                presence_new_spell(fRemove);
            }
        }

        void presence_new_spell(bool fRemove)
        {
            if (fRemove)
            {
                sbOut.AppendFormat("Can't remove without specifying spell:xxx.");
                return;
            }

            string who = FindWho(listArgs[0], fSilent:false);
            if (who == null)
                return;

            string spellId = getavailablespellid(who);

            string cost = StringArg("cost");
            if (cost == "")
            {
                sbOut.Append("cost: is required.");
                return;
            }

            string logicalKey = who + "|" + spellId;
            string effect = String.Format("cost:{0} {1}", cost, AppendLooseArgs());
            sbOut.AppendFormat("presence:\n{0} spell:{1} {2}\n", who, spellId, effect);
            GameHost.Worker.note3("_presence", logicalKey, effect.ToString());
            presence_list_helper(who, false, true);
        }

        void presence_update(bool fRemove, string spellId)
        {
            string who = FindWhoOrGroup(listArgs[0]);

            var logicalKey = who + "|" + spellId;

            var oldValue = readkey("_presence", logicalKey);

            if (oldValue == null)
            {
                sbOut.AppendFormat("No such spell was found.");
                return;
            }

            if (fRemove)
            {
                if (listArgs.Count > 1)
                {
                    sbOut.AppendFormat("Do not include an effect description with remove:yes.");
                    return;
                }

                presence_remove(who, logicalKey, oldValue);
                return;
            }

            presence_update(who, logicalKey, oldValue);
        }

        void presence_remove(string who, string logicalKey, string oldValue)
        {
            GameHost.Worker.del_key("_presence", logicalKey);
            sbOut.AppendFormat("presence Removed:\n{0} {1}\n", logicalKey.Replace("|", " spell:"), oldValue);
            presence_list_helper(who, false, true);
        }

        void presence_list(bool fRemove)
        {
            if (!(hashArgs.Count == 0 || (hashArgs.Count == 1 && fRemove == true)))
            {
                sbOut.Append("You can only specify remove:yes or nothing without an item.");
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            presence_list_helper(who, fRemove, false);
        }

        void presence_list_helper(string who, bool fRemove, bool fSummary)
        {
            var results = findpresence(who);

            var prevroller = "";

            int totalCost = 0;

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        report_presence_total(prevroller, totalCost);
                        sbOut.AppendFormat("====================\n");
                        totalCost = 0;
                    }
                }

                prevroller = roller;
                report_one_presence(fRemove, fSummary, result, ref totalCost);
            }
            report_presence_total(prevroller, totalCost);
        }

        void report_presence_total(string prevroller, int totalCost)
        {
            var presence = readkey(prevroller + "/_misc", "casting_presence");
            if (presence == null || presence == "")
                presence = "0";

            sbOut.AppendFormat("{0} used:{1} max:{2}\n", prevroller, totalCost, presence);
        }

        void report_one_presence(bool fRemove, bool fSummary, StringPair result, ref int totalCost)
        {
            var val = readkey("_presence", result.key);

            string effect;
            string cost;
            GetPresenceParts(val, out effect, out cost);

            int nCost = 0;
            if (Int32.TryParse(cost, out nCost))
            {
                totalCost += nCost;
            }

            if (!fSummary)
            {
                sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " spell:"), val);
            }

            if (fRemove)
            {
                GameHost.Worker.del_key("_presence", result.key);
            }
        }

        void presence_update(string who, string logicalKey, string oldValue)
        {
            string cost, effect;
            GetPresenceParts(oldValue, out effect, out cost);

            string costArg = StringArg("cost");
            if (costArg != "") cost = costArg;

            string newEffect = String.Format("cost:{0} {1}", cost, AppendLooseArgs());

            GameHost.Worker.note3("_presence", logicalKey, newEffect);
            sbOut.AppendFormat("presence Updated:\n{0} {1}\n", logicalKey.Replace("|", " spell:"), newEffect);
            presence_list_helper(who, false, true);
        }

        static void GetPresenceParts(string desc, out string effect, out string cost)
        {
            const string costStr = "cost:";

            if (desc.StartsWith(costStr))
            {
                int iSpace = desc.IndexOf(' ', costStr.Length);
                if (iSpace > 0 && iSpace < desc.Length - 2)
                {
                    cost = desc.Substring(costStr.Length, iSpace - costStr.Length);
                    effect = desc.Substring(iSpace + 1);
                    return;
                }
            }

            effect = desc;
            cost = "0";
        }


        void exec_spiritmana(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0 && hashArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");
            string spell = "";

            if (listArgs.Count >= 2)
            {
                spell = listArgs[1];
            }

            if (spell != "")
            {
                spiritmana_update(fRemove, spell);
            }
            else if (listArgs.Count == 1)
            {
                spiritmana_list(fRemove);
            }
        }

        void spiritmana_update(bool fRemove, string spellId)
        {
            string who = FindWhoOrGroup(listArgs[0]);

            var logicalKey = who + "|" + spellId;

            var oldValue = readkey("_spiritmana", logicalKey);

            if (oldValue == null)
            {
                oldValue = "0";
            }

            if (fRemove)
            {
                spiritmana_remove(who, logicalKey, oldValue);
                return;
            }

            spiritmana_update(who, logicalKey, oldValue);
        }

        void spiritmana_update(string who, string logicalKey, string oldValue)
        {
            string used = oldValue;
            int usedArg = IntArg("used", 0);
            used = usedArg.ToString();

            if (usedArg != 0)
            {
                GameHost.Worker.note3("_spiritmana", logicalKey, used);
                sbOut.AppendFormat("spiritmana usage updated:\n{0} {1}\n", logicalKey.Replace("|", " "), used);
            }
            else
            {
                spiritmana_remove(who, logicalKey, oldValue);
            }
        }


        void spiritmana_remove(string who, string logicalKey, string oldValue)
        {
            GameHost.Worker.del_key("_spiritmana", logicalKey);
            sbOut.AppendFormat("spiritmana usage removed:\n{0} {1}\n", logicalKey.Replace("|", " "), oldValue);
        }

        void spiritmana_list(bool fRemove)
        {
            if (!(hashArgs.Count == 0 || (hashArgs.Count == 1 && fRemove == true)))
            {
                sbOut.Append("You can only specify remove:yes or nothing without an item.");
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            spiritmana_list_helper(who, fRemove);
        }

        void spiritmana_list_helper(string who, bool fRemove)
        {
            var results = findspiritmana(who);

            var prevroller = "";

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        sbOut.AppendFormat("====================\n");
                    }
                }

                prevroller = roller;
                report_one_spiritmana(fRemove, result);
            }
        }

        void report_one_spiritmana(bool fRemove, StringPair result)
        {
            var val = readkey("_spiritmana", result.key);

            string toon;
            string id;
            GameHost.Worker.Parse2Ex(result.key, out toon, out id, "|");

            sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " "), val);

            if (fRemove)
            {
                GameHost.Worker.del_key("_spiritmana", result.key);
            }
        }

        void exec_runemagic(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0 && hashArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");
            string spell = "";

            if (listArgs.Count >= 2)
            {
                spell = listArgs[1];
            }

            if (spell != "")
            {
                runemagic_update(fRemove, spell);
            }
            else if (listArgs.Count == 1)
            {
                runemagic_list(fRemove);
            }
        }

        void runemagic_update(bool fRemove, string spellId)
        {
            string who = FindWhoOrGroup(listArgs[0]);

            var logicalKey = who + "|" + spellId;

            var oldValue = readkey("_runemagic", logicalKey);

            if (oldValue == null)
            {
                oldValue = "0";
            }

            if (fRemove)
            {
                runemagic_remove(who, logicalKey, oldValue);
                return;
            }

            runemagic_update(who, logicalKey, oldValue);
        }

        void runemagic_update(string who, string logicalKey, string oldValue)
        {
            string used = oldValue;
            int usedArg = IntArg("used", 0);
            used = usedArg.ToString();

            if (usedArg != 0)
            {
                GameHost.Worker.note3("_runemagic", logicalKey, used);
                sbOut.AppendFormat("runemagic usage updated:\n{0} {1}\n", logicalKey.Replace("|", " "), used);
            }
            else
            {
                runemagic_remove(who, logicalKey, oldValue);
            }
        }


        void runemagic_remove(string who, string logicalKey, string oldValue)
        {
            GameHost.Worker.del_key("_runemagic", logicalKey);
            sbOut.AppendFormat("runemagic usage removed:\n{0} {1}\n", logicalKey.Replace("|", " "), oldValue);
        }

        void runemagic_list(bool fRemove)
        {
            if (!(hashArgs.Count == 0 || (hashArgs.Count == 1 && fRemove == true)))
            {
                sbOut.Append("You can only specify remove:yes or nothing without an item.");
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            runemagic_list_helper(who, fRemove);
        }

        void runemagic_list_helper(string who, bool fRemove)
        {
            var results = findrunemagic(who);

            var prevroller = "";

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        sbOut.AppendFormat("====================\n");
                    }
                }

                prevroller = roller;
                report_one_runemagic(fRemove, result);
            }
        }

        void report_one_runemagic(bool fRemove, StringPair result)
        {
            var val = readkey("_runemagic", result.key);

            string toon;
            string id;
            GameHost.Worker.Parse2Ex(result.key, out toon, out id, "|");

            sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " "), val);
      
            if (fRemove)
            {
                GameHost.Worker.del_key("_runemagic", result.key);
            }
        }

        void exec_shugenja(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0 && hashArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");
            string spell = StringArg("spell");

            if (spell != "")
            {
                shugenja_update(fRemove, spell);
            }
            else if (listArgs.Count == 1)
            {
                shugenja_list(fRemove);
            }
            else
            {
                shugenja_new_spell(fRemove);
            }
        }

        void shugenja_new_spell(bool fRemove)
        {
            if (fRemove)
            {
                sbOut.AppendFormat("Can't remove without specifying spell:xxx.");
                return;
            }

            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            string spellId = getavailableshugenjaid(who);

            string school = StringArg("school");
            if (school == "")
            {
                sbOut.Append("school: is required.");
                return;
            } 

            string cost = StringArg("cost");
            if (cost == "")
            {
                sbOut.Append("cost: is required.");
                return;
            }

            string charges = StringArg("charges");
            if (charges == "")
            {
                sbOut.Append("charges: is required.");
                return;
            }

            string logicalKey = who + "|" + spellId;
            string effect = String.Format("school:{0} cost:{1} charges:{2} {3}", school, cost, charges, AppendLooseArgs());
            sbOut.AppendFormat("shugenja:\n{0} spell:{1} {2}\n", who, spellId, effect);
            GameHost.Worker.note3("_shugenja", logicalKey, effect.ToString());

            shugenja_list_helper(who, fRemove: false, fSummary: true, reportSchool: school);
        }

        void shugenja_update(bool fRemove, string spellId)
        {
            string who = FindWhoOrGroup(listArgs[0]);

            var logicalKey = who + "|" + spellId;

            var oldValue = readkey("_shugenja", logicalKey);

            if (oldValue == null)
            {
                sbOut.AppendFormat("No such spell was found.");
                return;
            }

            if (fRemove)
            {
                if (listArgs.Count > 1)
                {
                    sbOut.AppendFormat("Do not include an effect description with remove:yes.");
                    return;
                }

                shugenja_remove(who, logicalKey, oldValue);
                return;
            }

            shugenja_update(who, logicalKey, oldValue);
        }

        void shugenja_remove(string who, string logicalKey, string oldValue)
        {
            GameHost.Worker.del_key("_shugenja", logicalKey);
            sbOut.AppendFormat("Shugenja Removed:\n{0} {1}\n", logicalKey.Replace("|", " spell:"), oldValue);

            string school, effect, cost, charges;
            GetShugenjaParts(oldValue, out school, out cost, out charges, out effect);

            shugenja_list_helper(who, fRemove: false, fSummary: true, reportSchool: school);        }

        void shugenja_list(bool fRemove)
        {
            if (!(hashArgs.Count == 0 || (hashArgs.Count == 1 && fRemove == true)))
            {
                sbOut.Append("You can only specify remove:yes or nothing without an item.");
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            shugenja_list_helper(who, fRemove, fSummary:false, reportSchool:"*");
        }

        void shugenja_list_helper(string who, bool fRemove, bool fSummary, string reportSchool)
        {
            var costs = new Dictionary<string, int>();
            
            var results = findshugenja(who);

            var prevroller = "";

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        report_shugenja_total(prevroller, costs);
                        sbOut.AppendFormat("====================\n");
                        costs = new Dictionary<string, int>();
                    }
                }

                prevroller = roller;
                report_one_shugenja(fRemove, fSummary, result, costs, reportSchool);
            }
            report_shugenja_total(prevroller, costs);
        }

        void report_shugenja_total(string prevroller, Dictionary<string, int> costs)
        {
            var q = from cost in costs orderby cost.Key select cost;           

            foreach (var cost in q)
            {
                var shugenja = readkey(prevroller + "/magic", cost.Key);
                if (shugenja == null || shugenja == "")
                    shugenja = "0";

                sbOut.AppendFormat("{0} school:{1} used:{2} max:{3}\n", prevroller, cost.Key, cost.Value, shugenja);
            }
        }

        void report_one_shugenja(bool fRemove, bool fSummary, StringPair result, Dictionary<string,int> costs, string reportSchool)
        {
            var val = readkey("_shugenja", result.key);

            string effect;
            string school;
            string cost;
            string charges;
            GetShugenjaParts(val, out school, out cost, out charges, out effect);

            if (reportSchool == "*" || reportSchool == school)
            {
                int nCost = 0;
                if (Int32.TryParse(cost, out nCost))
                {
                    int prevcost;
                    if (costs.TryGetValue(school, out prevcost))
                        nCost += prevcost;

                    costs[school] = nCost;
                }
            }

            if (!fSummary)
            {
                sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " spell:"), val);
            }

            if (fRemove)
            {
                GameHost.Worker.del_key("_shugenja", result.key);
            }
        }

        void shugenja_update(string who, string logicalKey, string oldValue)
        {
            string school, effect, cost, charges;
            GetShugenjaParts(oldValue, out school, out cost, out charges, out effect);

            string schoolArg = StringArg("school");
            if (schoolArg != "") school = schoolArg;

            string costArg = StringArg("cost");
            if (costArg != "") cost = costArg;

            string chargesArg = StringArg("charges");
            if (chargesArg != "") charges = chargesArg;

            string effectArg = AppendLooseArgs().ToString();
            if (effectArg != "") effect = effectArg;

            string newEffect = String.Format("school:{0} cost:{1} charges:{2} {3}", school, cost, charges, effect);

            GameHost.Worker.note3("_shugenja", logicalKey, newEffect);
            sbOut.AppendFormat("shugenja Updated:\n{0} {1}\n", logicalKey.Replace("|", " spell:"), newEffect);

            shugenja_list_helper(who, fRemove: false, fSummary: true, reportSchool: school);
        }


        Dictionary<string,int> ComputeShugenjaSummary(string who)
        {
            var result = new Dictionary<string, int>();

            foreach (var p in findshugenja(who))
            {
                string toon, id;
                GameHost.Worker.Parse2Ex(p.key, out toon, out id, "|");
                
                string effect;
                string school;
                string cost;
                string charges;

                GetShugenjaParts(p.val, out school, out cost, out charges, out effect);

                var key = toon + "|" + school;

                int nCost;
                if (!int.TryParse(cost, out nCost))
                    continue;

                int total = 0;
                if (result.TryGetValue(key, out total))
                    nCost += total;

                result[key] = nCost;
            }

            return result;
        }

        static void GetShugenjaParts(string desc, out string school, out string cost, out string charges,  out string effect)
        {
            const string schoolStr = "school:";
            const string costStr = "cost:";
            const string chargesStr = "charges:";

            school = "unknown";
            cost = "0";
            charges = "0";

            string car, ctr;

            GameHost.Worker.Parse2(desc, out car, out ctr);
            if (car.StartsWith(schoolStr))
            {
                school = car.Substring(schoolStr.Length);
                desc = ctr;
            }

            GameHost.Worker.Parse2(desc, out car, out ctr);
            if (car.StartsWith(costStr))
            {
                cost = car.Substring(costStr.Length);
                desc = ctr;
            }

            GameHost.Worker.Parse2(desc, out car, out ctr);
            if (car.StartsWith(chargesStr))
            {
                charges = car.Substring(chargesStr.Length);
                desc = ctr;
            }

            effect = desc;

            if (effect == "")
                effect = "unknown";
        }

        void exec_loot(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0 && hashArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            bool fRemove = BoolArg("remove");
            string item = StringArg("item");

            if (item != "")
            {
                loot_handle_item(fRemove, item);
            }
            else if (listArgs.Count == 1)
            {
                loot_list(fRemove);
            }
            else
            {
                loot_new_item(fRemove);
            }
        }

        void loot_new_item(bool fRemove)
        {
            if (fRemove)
            {
                sbOut.AppendFormat("Can't remove without specifying item:xxx.");
                return;
            }

            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            string item = (DateTime.UtcNow.Ticks / 100000 - 6345850000000).ToString();

            string enc = StringArg("enc");
            if (enc == "")
            {
                sbOut.Append("enc: is required.");
                return;
            }

            string room = StringArg("room");
            if (room == "")
            {
                sbOut.Append("room: is required. Specify where you found it in one word.");
                return;
            }

            var loot = AppendLooseArgs();

            string logicalKey = who + "|" + item;
            loot.AppendFormat(" enc:{0} room:{1}", enc, room);
            sbOut.AppendFormat("Loot:\n{0} item:{1} {2}\n", who, item, loot.ToString());
            GameHost.Worker.note3("_loot", logicalKey, loot.ToString());
            return;
        }

        void loot_handle_item(bool fRemove, string item)
        {
            var logicalKey = getlootitem(item);

            if (logicalKey == null)
            {
                sbOut.AppendFormat("No such item was found.");
                return;
            }

            var oldValue = readkey("_loot", logicalKey);

            if (oldValue == null)
            {
                sbOut.AppendFormat("No such item was found.");
                return;
            }

            if (listArgs.Count == 1 && listArgs[0] == "update")
            {
                sbOut.Append("You must give the item number and new description when you update; enc: and room: are optional.");
                return;
            }

            if (listArgs.Count > 1 && listArgs[0] == "update")
            {
                if (fRemove)
                {
                    sbOut.AppendFormat("Can't update and remove together.");
                    return;
                }

                loot_update(logicalKey, oldValue);
                return;
            }

            if (fRemove)
            {
                if (listArgs.Count > 0)
                {
                    sbOut.AppendFormat("Do not include an item description with remove:yes.");
                    return;
                }

                loot_remove(logicalKey, oldValue);
                return;
            }

            if (listArgs.Count > 1)
            {
                sbOut.AppendFormat("Do not include an item description with loot assign.");
                return;
            } 
            
            loot_assign(item, logicalKey, oldValue);
            return;
        }

        void loot_assign(string item, string logicalKey, string oldValue)
        {
            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            string newKey = who + "|" + item;

            GameHost.Worker.del_key("_loot", logicalKey);
            GameHost.Worker.note3("_loot", newKey, oldValue);
            sbOut.AppendFormat("Loot Assigned:\n{0} item:{1} {2}\n", who, item, oldValue);
            return;
        }

        void loot_remove(string logicalKey, string oldValue)
        {
            GameHost.Worker.del_key("_loot", logicalKey);
            sbOut.AppendFormat("Loot Removed:\n{0} {1}\n", logicalKey.Replace("|", " item:"), oldValue);
            return;
        }

        void loot_list(bool fRemove)
        {
            if (!(hashArgs.Count == 0 || (hashArgs.Count == 1 && fRemove == true)))
            {
                sbOut.Append("You can only specify remove:yes or nothing without an item.");
                return;
            }

            if (listArgs[0] == "update")
            {
                sbOut.Append("You must give the item number and new description when you update; enc: and room: are optional.");
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            var results = findloot(who);

            var prevroller = "";

            foreach (var result in results)
            {
                roller = result.key.Substring(0, result.key.IndexOf('|'));

                if (prevroller != "")
                {
                    if (roller != prevroller)
                    {
                        sbOut.AppendFormat("====================\n");
                    }
                }

                prevroller = roller;
                report_one_loot(fRemove, result);
            }
            return;
        }

        void report_one_loot(bool fRemove, StringPair result)
        {
            var val = readkey("_loot", result.key);

            sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " item:"), val);
            if (fRemove)
            {
                GameHost.Worker.del_key("_loot", result.key);
            }
        }

        void report_one_used(bool fRemove, StringPair result)
        {
            var val = readkey("_used", result.key);

            sbOut.AppendFormat("{0} {1}\n", result.key.Replace("|", " -> "), val);

            // don't remove _have entries, only remove _used entries
            if (result.key.EndsWith("_used"))
            {
                if (fRemove)
                {
                    GameHost.Worker.del_key("_used", result.key);
                }
            }
        }

        void loot_update(string logicalKey, string oldValue)
        {
            string desc, enc, room;
            GetLootParts(oldValue, out desc, out enc, out room);
            var loot = AppendLooseArgs();

            string encArg = StringArg("enc");
            string roomArg = StringArg("room");

            if (encArg != "") enc = encArg;
            if (roomArg != "") room = roomArg;

            loot.AppendFormat(" enc:{0} room:{1}", enc, room);

            string newValue = loot.ToString();
            GameHost.Worker.note3("_loot", logicalKey, newValue);
            sbOut.AppendFormat("Loot Updated:\n{0} {1}\n", logicalKey.Replace("|", " item:"), newValue);
        }

        StringBuilder AppendLooseArgs()
        {
            StringBuilder loot = new StringBuilder();

            for (int i = 1; i < listArgs.Count; i++)
            {
                if (loot.Length > 0)
                    loot.Append(" ");

                loot.Append(listArgs[i]);
            }

            return loot;
        }

        static void GetLootParts(string desc, out string d1, out string enc, out string room)
        {
            const string roomStr = " room:";
            const string encStr = " enc:";

            int iEncTag = desc.IndexOf(encStr);
            int iRoomTag = desc.IndexOf(roomStr);
            int n;

            if (iEncTag < 0 || iRoomTag < 0)
                n = Math.Max(iEncTag, iRoomTag);
            else
                n = Math.Min(iEncTag, iRoomTag);

            if (n > 0)
            {
                d1 = desc.Substring(0, n);
            }
            else
            {
                d1 = desc;
            }

            if (iRoomTag > 0)
            {
                int iroom = iRoomTag + roomStr.Length;
                room = desc.Substring(iroom);
                int ispace = room.IndexOf(' ');
                if (ispace > 0)
                {
                    room = room.Substring(0, ispace);
                }
            }
            else
            {
                room = "Unknown";
            }


            if (iEncTag > 0)
            {
                int ienc = iEncTag + encStr.Length;
                enc = desc.Substring(ienc);
                int ispace = enc.IndexOf(' ');
                if (ispace > 0)
                {
                    enc = enc.Substring(0, ispace);
                }
            }
            else
            {
                enc = "Unknown";
            }
        }
        
        void exec_mana(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 1)
            {
                dumpCommandHelp();
                return;
            }

            int nResult = -1;
            int mana = 0;

            bool fRemove = BoolArg("remove");
            bool fRest = BoolArg("rest");

            List<string> matches = new List<string>();
            for (int i = 1; i < listArgs.Count; i++)
            {
                var arg = listArgs[i];

                if (arg.Length > 1 && arg.StartsWith("#"))
                {
                    Int32.TryParse(arg.Substring(1), out nResult);
                    nResult--;
                    continue;
                }

                if (arg.Length >= 1)
                {
                    if (arg.StartsWith("-") || (arg[0] >= '0' && arg[0] <= '9'))
                    {
                        Int32.TryParse(arg, out mana);
                        continue;
                    }
                }

                matches.Add(arg.ToLower());
            }

            if (matches.Count == 0)
            {
                matches.Add("/mana");
                matches.Add("total_magic_points");
            }
            else
            {
                matches.Add("/mana");
            }

            if (mana == 0)
            {
                string who = FindWhoOrGroup(listArgs[0]);

                var results = findmana(who);

                var prevroller = "";

                foreach (var result in results)
                {
                    roller = result.key.Substring(0, result.key.IndexOf('|'));

                    if (roller != prevroller)
                    {
                        if (prevroller != "")
                        {
                            sbOut.AppendFormat("====================\n");
                        }

                        var basics = getbasics(roller, false);

                        sbOut.AppendFormat("{0} pow:{1}\n", roller, basics._pow);
                    }

                    prevroller = roller;

                    ParseToMap(result.val);

                    int used = IntArg("used");
                    int max = IntArg("max");                   

                    sbOut.AppendFormat("{0} used:{1} max:{2} remaining:{3}\n", result.key, used, max, max-used);

                    if (fRemove)
                    {
                        GameHost.Worker.del_key("_mana", result.key);
                    }
                    else if (fRest)
                    {
                        RecoverMana(result.key, used, max);
                    }
                }

                if (fRest)
                {
                    ApplyShugenjaUsage(who);
                }
            }
            else
            {
                string who = FindWho(listArgs[0], fSilent: false);
                if (who == null)
                    return;

                var results = findkeys(who, matches.ToArray(), false);

                StringTriple result = null;

                if (results.Count == 1)
                {
                    result = results[0];
                }
                else if (nResult >= 0 && nResult < results.Count)
                {
                    result = results[nResult];
                }
                else
                {
                    WriteResults(results);
                    return;
                }

                string max = result.val;

                string logicalKey = result.dir.Replace('/', '|') + "|" + result.key;
                logicalKey = logicalKey.Replace("|total_magic_points", "");

                int used = AdjustManaKey(mana, max, logicalKey);

                if (logicalKey.EndsWith("|personal"))
                {
                    // when subtracting personal mana, also subtract normal mana
                    max = readkey(result.dir, "total_magic_points");
                    logicalKey = logicalKey.Replace("|personal", "");
                    AdjustManaKey(mana, max, logicalKey);
                }
                else if (logicalKey.EndsWith("|mana"))
                {
                    int maxNormal;
                    if (!Int32.TryParse(max, out maxNormal))
                        return;

                    int remaining = maxNormal - used;

                    max = readkey(result.dir, "personal");
                    if (max == null)
                    {
                        max = readkey(who, "MPMAX");
                        if (max == null)
                            return;

                        GameHost.Worker.note3(who + "/mana", "personal", max);
                    }

                    if (remaining < 0) remaining = 0;

                    CapPersonalMana(remaining, max, logicalKey + "|personal");
                }
            }
        }

        void RecoverMana(string key, int used, int max)
        {
            string manatype = key.Substring(key.LastIndexOf('|') + 1);

            if (manatype.EndsWith("_mana"))
                manatype = manatype.Substring(0, manatype.Length - 5);

            string recovery;

            if (manatype == "mana")
            {
                recovery = "mpts_per_day";
            }
            else
            {
                recovery = manatype + "_recovery";
            }

            var daily = readkey(roller + "/mana", recovery);

            int d = 0;
            Int32.TryParse(daily, out d);

            if (d == 0) d = used; // recovery it all if there is no recovery field

            used -= d;
            if (used <= 0)
            {
                sbOut.AppendFormat("---> recovered {0} {1} mana ---> restored to full\n", d, manatype);
                GameHost.Worker.del_key("_mana", key);
            }
            else
            {
                sbOut.AppendFormat("---> recovered {0} {1} mana ---> after resting used:{2}\n", d, manatype, used);
                string payload = String.Format("used:{0} max:{1}", used, max);
                GameHost.Worker.note3("_mana", key, payload);
            }
        }

        void ApplyShugenjaUsage(string who)
        {
            var shugenjaSummary = ComputeShugenjaSummary(who);

            if (shugenjaSummary.Count == 0)
                return;

            sbOut.AppendFormat("----- mana adjustment due to held shugenja magic\n");

            foreach (var k in shugenjaSummary.Keys)
            {
                string toon, school;
                GameHost.Worker.Parse2Ex(k, out toon, out school, "|");

                if (toon == "" || school == "")
                    continue;

                int used = shugenjaSummary[k];

                var logicalKey = toon + "|mana|" + school;

                string payload = String.Format("used:{0} max:0", used); // max is computed on the fly anyway so no need to do more

                GameHost.Worker.note3("_mana", logicalKey, payload);

                sbOut.AppendFormat("{0}: {1} school begins with {2} used.\n", toon, school, used);
            }
        }

        int AdjustManaKey(int mana, string max, string logicalKey)
        {
            int used = ReadUsedMana(logicalKey);

            used += mana;

            return RecordManaUsage(max, logicalKey, used);        
        }

        int ReadUsedMana(string logicalKey)
        {
            string val = readkey("_mana", logicalKey);

            int used = 0;

            if (val != null && val != "")
            {
                ParseToMap(val);

                used = IntArg("used");
            }
            return used;
        }

        void CapPersonalMana(int mana, string max, string logicalKey)
        {
            int maxMana;
            if (!Int32.TryParse(max, out maxMana))
                return;

            int used = ReadUsedMana(logicalKey);

            int remaining = maxMana - used;

            if (remaining > mana)
            {
                RecordManaUsage(max, logicalKey, maxMana - mana);
            }
        }
        
        int RecordManaUsage(string max, string logicalKey, int used)
        {
            if (used > 0)
            {
                string payload = String.Format("used:{0} max:{1}", used, max);
                sbOut.AppendFormat("Mana:\n{0} used:{1} max:{2}\n", logicalKey, used, max);
                GameHost.Worker.note3("_mana", logicalKey, payload);
                return used;
            }
            else
            {
                sbOut.AppendFormat("Mana:\n{0} usage removed\n", logicalKey);
                GameHost.Worker.del_key("_mana", logicalKey);
                return 0;
            }
        }

        void exec_fatigue(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 1)
            {
                dumpCommandHelp();
                return;
            }

            int fatigue = 0;

            bool fRemove = BoolArg("remove");
            bool fRest = BoolArg("rest");

            List<string> matches = new List<string>();
            for (int i = 1; i < listArgs.Count; i++)
            {
                var arg = listArgs[i];

                if (arg.Length >= 1)
                {
                    if (arg.StartsWith("-") || (arg[0] >= '0' && arg[0] <= '9'))
                    {
                        Int32.TryParse(arg, out fatigue);
                        continue;
                    }
                }

                arg = arg.ToLower();

                if (arg == "normal")
                    continue;

                matches.Add(arg);
            }

            if (matches.Count > 0 && matches[0] != "hard")
            {
                sbOut.AppendFormat("Only hard or normal fatigue\n");
                return;
            }

            if (fatigue == 0 || fRest)
            {
                string who = FindWhoOrGroup(listArgs[0]);

                var results = findfatigue(who);

                var prevroller = "";

                int totalused = 0;

                foreach (var result in results)
                {
                    roller = result.key.Substring(0, result.key.IndexOf('|'));

                    if (roller != prevroller)
                    {
                        if (prevroller != "")
                        {
                            if (!fRemove && !fRest) FootFatigue(prevroller, totalused);
                            sbOut.AppendFormat("====================\n");
                        }

                        totalused = 0;
                    }

                    prevroller = roller;

                    ParseToMap(result.val);

                    int used = IntArg("used");

                    totalused += used;

                    sbOut.AppendFormat("{0} used:{1}\n", result.key, used);

                    if (fRemove)
                    {
                        GameHost.Worker.del_key("_fatigue", result.key);
                    }
                    else if (fRest)
                    {
                        if (result.key.EndsWith("|hard"))
                        {
                            used -= fatigue;
                            if (used <= 0)
                            {
                                sbOut.AppendFormat("---> recovered {0} fatigue ---> restored to full\n", fatigue);
                                GameHost.Worker.del_key("_fatigue", result.key);
                            }
                            else
                            {
                                sbOut.AppendFormat("---> recovered {0} fatigue ---> after resting used:{1}\n", fatigue, used);
                                string payload = String.Format("used:{0}", used);
                                GameHost.Worker.note3("_fatigue", result.key, payload);
                            }
                        }
                        else
                        {
                            sbOut.AppendFormat("---> restored to full\n");
                            GameHost.Worker.del_key("_fatigue", result.key);
                        }
                    }
                }

                if (!fRemove && !fRest) FootFatigue(prevroller, totalused);
            }
            else
            {
                string who = FindWho(listArgs[0], fSilent: false);
                if (who == null)
                    return;

                var basics = getbasics(who, false);

                int max = basics.fatigue;

                string logicalKey;

                if (matches.Count == 1 && matches[0] == "hard")
                    logicalKey = who + "|hard";
                else
                    logicalKey = who + "|normal";

                string val = readkey("_fatigue", logicalKey);

                int used = 0;

                if (val != null && val != "")
                {
                    ParseToMap(val);

                    used = IntArg("used");
                }

                fatigue += used;

                if (fatigue > 0)
                {
                    string payload = String.Format("used:{0}", fatigue);
                    sbOut.AppendFormat("Fatigue:\n{0} used:{1} max:{2}\n", logicalKey, fatigue, max);
                    GameHost.Worker.note3("_fatigue", logicalKey, payload);
                }
                else
                {
                    sbOut.AppendFormat("Fatigue:\n{0} usage removed\n", logicalKey);
                    GameHost.Worker.del_key("_fatigue", logicalKey);
                }
            }
        }

        void FootFatigue(string prevroller, int totalused)
        {
            if (totalused == 0)
                return;

            var basics = getbasics(roller, false);
            sbOut.AppendFormat("{0} total_used:{1}, max:{2}, status:{3}\n",
                prevroller,
                totalused,
                basics.fatigue,
                basics.fatigue - totalused);
        }
        
        void exec_use(string cmd, string s)
        {
            const string remaining = "_have";
            const string consumed = "_used";
            const string usedFolder = "_used";
            ParseToMap(s);

            if (listArgs.Count < 1)
            {
                dumpCommandHelp();
                return;
            }

            int using_now = 0;

            string what = "";

            for (int i = 1; i < listArgs.Count; i++)
            {
                var arg = listArgs[i];

                if (arg.Length >= 1)
                {
                    if (arg.StartsWith("-") || (arg[0] >= '0' && arg[0] <= '9'))
                    {
                        Int32.TryParse(arg, out using_now);
                        continue;
                    }
                }

                if (what != "")
                    what = what + "_";

                what = what + arg;
            }

            what = what.ToLower().Replace(" ", "_").Replace("/", "_");

            if (what == "")
            {
                string who = FindWhoOrGroup(listArgs[0]);

                var results = findused(who);

                var prevroller = "";

                foreach (var result in results)
                {
                    roller = result.key.Substring(0, result.key.IndexOf('|'));

                    if (roller != prevroller)
                    {
                        if (prevroller != "")
                        {
                            sbOut.AppendFormat("====================\n");
                        }
                    }

                    prevroller = roller;

                    sbOut.AppendFormat("{0} {1}\n", result.key, result.val);
                }
            }
            else
            {
                string who = FindWho(listArgs[0], fSilent: false);
                if (who == null)
                    return;

                string logicalKey = who + "|" + what;
                string v = readkey(usedFolder, logicalKey + consumed);

                int existing_usage = 0;
                bool trackingUsed = false;
                if (v != null && v != "")
                {
                    logicalKey += consumed;
                    Int32.TryParse(v, out existing_usage);
                    trackingUsed = true;
                }
                else
                {
                    v = readkey(usedFolder, logicalKey + remaining);
                    if (v != null && v != "")
                    {
                        logicalKey += remaining;
                        Int32.TryParse(v, out existing_usage);
                    }
                    else
                    {
                        switch (cmd)
                        {
                            case "!use":
                                trackingUsed = true;
                                if (!logicalKey.EndsWith(consumed))
                                    logicalKey += consumed;
                                break;
                            case "!gain":
                                if (!logicalKey.EndsWith(remaining))
                                    logicalKey += remaining;
                                break;
                            default:
                                sbOut.Append("Unknown command.");
                                return;
                        }
                    }
                }

                if (cmd == "!gain") using_now *= -1;
                if (!trackingUsed) using_now *= -1;

                existing_usage += using_now;

                if (existing_usage > 0)
                {
                    sbOut.AppendFormat("Consumption:\n{0} {1}\n", logicalKey, existing_usage);
                    GameHost.Worker.note3(usedFolder, logicalKey, existing_usage.ToString());
                }
                else
                {
                    sbOut.AppendFormat("Consumption:\n{0} Removed.\n", logicalKey);
                    GameHost.Worker.del_key(usedFolder, logicalKey);
                }
            }
        }

        string FindWhoOrGroup(string arg)
        {
            if (arg == null || arg == "" || arg == "all")
                return "all";

            if (arg == "party")
                return findpartyfolder();

            string who = FindWho(arg, fSilent:true);

            if (who == null)
                return arg;
 
            return who;
        }

        void exec_wound(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 1)
            {
                dumpCommandHelp();
                return;
            }

            int nResult = -1;
            int wounds = 0;

            bool fRemove = BoolArg("remove");

            List<string> matches = new List<string>();
            for (int i = 1; i < listArgs.Count; i++)
            {
                var arg = listArgs[i];

                if (arg.Length > 1 && arg.StartsWith("#"))
                {
                    Int32.TryParse(arg.Substring(1), out nResult);
                    nResult--;
                    continue;
                }

                if (arg.Length >= 1)
                {
                    if (arg.StartsWith("-") || (arg[0] >= '0' && arg[0] <= '9'))
                    {
                        Int32.TryParse(arg, out wounds);
                        continue;
                    }
                }

                matches.Add(arg.ToLower());
            }

            matches.Add("_hit_location");

            if (wounds == 0)
            {
                string who = FindWhoOrGroup(listArgs[0]);

                var results = findwounds(who);

                var prevroller = "";
                int total = 0;

                foreach (var result in results)
                {
                    roller = result.key.Substring(0, result.key.IndexOf('|'));

                    if (prevroller != "")
                    {
                        if (roller != prevroller)
                        {
                            string maxlife = readkey(prevroller + "/_hit_location", "_life");
                            if (maxlife == null) maxlife = "unknown";

                            sbOut.AppendFormat("{0}|Total damage:{1} max_life:{2}\n", prevroller, total, maxlife);
                            sbOut.AppendFormat("====================\n");
                            total = 0;
                        }
                    }

                    prevroller = roller;

                    ParseToMap(result.val);

                    int dmg = IntArg("damage");
                    int max = IntArg("max");

                    total += dmg;
                  
                    sbOut.AppendFormat("{0} damage:{1} max:{2}\n", result.key, dmg, max);

                    if (fRemove)
                    {
                        GameHost.Worker.del_key("_wounds", result.key);
                    }
                }

                if ("" != prevroller)
                {
                    string maxlife = readkey(prevroller + "/_hit_location", "_life");
                    if (maxlife == null) maxlife = "unknown";

                    sbOut.AppendFormat("{0}|Total damage:{1} max_life:{2}\n", prevroller, total, maxlife);
                }
            }
            else
            {
                string who = listArgs[0];

                if (who != "all")
                    who = FindWho(listArgs[0], fSilent:false);

                if (who == null)
                    return;

                var results = findkeys(who, matches.ToArray(), false);

                StringTriple result = null;

                if (results.Count == 1)
                {
                    result = results[0];
                }
                else if (nResult >= 0 && nResult < results.Count)
                {
                    result = results[nResult];
                }
                else
                {
                    WriteResults(results);
                    return;
                }

                string max = readkey(who + "/_hit_location", result.key);
                if (max == null) max = "unknown";

                string logicalKey = result.dir.Replace('/', '|') + "|" + result.key;
                logicalKey = logicalKey.Replace("_hit_location|", "");

                string val = readkey("_wounds", logicalKey);

                int dmg = 0;

                if (val != null && val != "")
                {
                    ParseToMap(val);

                    dmg = IntArg("damage");
                }

                wounds += dmg;

                if (wounds > 0)
                {
                    string payload = String.Format("damage:{0} max:{1}", wounds, max);
                    sbOut.AppendFormat("Wounds:\n{0} damage:{1} max:{2}\n", logicalKey, wounds, max);
                    GameHost.Worker.note3("_wounds", logicalKey, payload);
                }
                else
                {
                    sbOut.AppendFormat("Wounds:\n{0} damage removed", logicalKey);
                    GameHost.Worker.del_key("_wounds", logicalKey);
                }
            }
        }

        void exec_party(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0)
            {
                party_show();
                return;
            }

            if (listArgs.Count == 1)
            {
                var arg = listArgs[0];

                if (arg == "help")
                {
                    dumpCommandHelp();
                    return;
                }
                else if (arg == "list")
                {
                    var parties = getallparties();
                    parties.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (string p in parties)
                    {
                        if (p.StartsWith("_party/"))
                        {
                            sbOut.Append(p.Substring(7) + "\n");
                        }
                    }
                }
                else if (arg == "disabled")
                {
                    var members = getabsent();
                    foreach (string m in members)
                    {
                        sbOut.Append(m + "\n");
                    }
                }
                else if (arg == "members")
                {
                    var members = getpresent();
                    foreach (string m in members)
                    {
                        sbOut.Append(m+"\n");
                    }
                }
                else if (arg == "show")
                {
                    party_show();
                }
                else
                {
                    party_set(listArgs);
                }
                return;
            }

            if (listArgs.Count == 2)
            {
                var arg = listArgs[0];

                if (arg == "new")
                {
                    party_new(listArgs[1]);
                    return;
                }

                var folder = findpartyfolder();

                if (arg == "date")
                {
                    if (IsValidDate(listArgs[1]))
                    {
                        GameHost.Worker.note3(folder, "__date", listArgs[1]);
                        sbOut.AppendFormat("The party date is now: {0}", listArgs[1]);
                    }
                    else
                    {
                        sbOut.AppendFormat("{0} is not a valid date", listArgs[1]);
                    }
                    return;
                }

                SetRoller(listArgs[1], roller);

                if (arg == "add")
                {
                    GameHost.Worker.note3(folder, roller, "yes");
                    GameHost.Worker.del_key(folder, ".");
                    camp_update();
                    sbOut.AppendFormat("{0} added to {1}.", roller, folder);
                }
                else if (arg == "remove")
                {
                    if (readkey(folder, roller) == null)
                    {
                        sbOut.AppendFormat("{0} not in {1}.", roller, folder);
                    }
                    else
                    {
                        GameHost.Worker.del_key(folder, roller);
                        camp_update();
                        sbOut.AppendFormat("{0} removed from {1}.", roller, folder);
                    }
                }
                else if (arg == "enable")
                {
                    var value = readkey(folder, roller);
                    if (value == null)
                    {
                        sbOut.AppendFormat("{0} not in {1}.", roller, folder);
                    }
                    else if (value.StartsWith("y"))
                    {
                        sbOut.AppendFormat("{0} is already enabled in {1}.", roller, folder);
                    }
                    else
                    {
                        GameHost.Worker.note3(folder, roller, "yes");
                        camp_update();
                        sbOut.AppendFormat("{0} enabled in {1}.", roller, folder);
                    }
                }
                else if (arg == "disable")
                {
                    var value = readkey(folder, roller);
                    if (value == null)
                    {
                        sbOut.AppendFormat("{0} not in {1}.", roller, folder);
                    }
                    else if (!value.StartsWith("y"))
                    {
                        sbOut.AppendFormat("{0} is already disabled in {1}.", roller, folder);
                    }
                    else
                    {
                        GameHost.Worker.note3(folder, roller, "no");
                        camp_update();
                        sbOut.AppendFormat("{0} disabled in {1}.", roller, folder);
                    }
                }
                else
                {
                    party_set(listArgs);
                }
                return;
            }
        }

        void party_show()
        {
            var folder = findpartyfolder();
            sbOut.AppendFormat("The party name is now: {0}\r\n", folder);

            var date = readkey(folder, "__date");

            if (date != null)
                sbOut.AppendFormat("The party date is now: {0}\r\n", date);

            sbOut.AppendFormat("Use !party help for additional info\r\n");
        }

        string getpartydate()
        {
            var folder = findpartyfolder();

            if (folder != null)
                return readkey(folder, "__date");

            return null;
        }

        // make a party tag out of the party folder name
        // convert _ to spaces (but not at the start)
        // convert CamelCase into Camel Case
        // add ": " to the end
        string getpartytag()
        {
            var folder = findpartyfolder();
            if (folder == null)
                return "";

            if (folder.StartsWith("_party/"))
                folder = folder.Substring(7);

            var b = new StringBuilder();

            bool fCap = true;

            foreach (var c in folder)
            {
                if (b.Length > 0 && c == '_')
                {
                    b.Append(" ");
                    fCap = true;
                    continue;
                }

                if (b.Length > 0 && char.IsUpper(c))
                    b.Append(" ");

                if (fCap)
                    b.Append(Char.ToUpper(c));
                else
                    b.Append(c);

                fCap = false;
            }

            b.Append(": ");

            return b.ToString();
        }

        void party_set(List<string> args)
        {
            var arg = args[0];

            if (arg.StartsWith("_party/"))
            {
                party_set_simple(arg);
                return;
            }

            var parties = getallparties();
            var found = new List<string>();
            int n = 0;

            parties.Sort();

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].StartsWith("#"))
                {
                    Int32.TryParse(args[i].Substring(1), out n);
                    args.RemoveAt(i);
                    i--;
                    continue;
                }

                args[i] = args[i].ToLower();
            }

            foreach (var praw in parties)
            {
                var p = praw.Substring(7);
                var plower = p.ToLower();

                if (args.Count == 1 && plower == args[0])
                {
                    found.Clear();
                    found.Add(p);
                    break;
                }
                
                int i = 0;
                for (i = 0; i < args.Count; i++)
                {
                    if (!plower.Contains(args[i]))
                        break;
                }

                if (i == args.Count)
                {
                    found.Add(p);
                }
            }

            if (found.Count == 0)
            {
                sbOut.AppendFormat("Party does not exist.\n");
                sbOut.AppendFormat("Current party is still '{0}'", findpartyfolder());
                return;
            }

            if (n > 0 && n <= found.Count)
            {
                party_set_simple("_party/" + found[n-1]);
                return;
            }

            if (found.Count > 1)
            {
                sbOut.AppendFormat("Many parties match.\n");
                int count = 1;
                foreach (var s in found)
                {
                    sbOut.AppendFormat("#{0} {1}\n", count++, s);
                }
                sbOut.AppendFormat("Current party is still '{0}'", findpartyfolder());
                return;
            }

            party_set_simple("_party/" + found[0]);           
        }

        void party_set_simple(string arg)
        {
            if (!GameHost.Worker.existskey(arg))
            {
                sbOut.AppendFormat("Party '{0}' does not exist.\n", arg);
                sbOut.AppendFormat("Current party is still '{0}'", findpartyfolder());
                return;
            }

            GameHost.Worker.note3("_party", GameHost.Worker.OriginToPartyKey(origin), arg);
            sbOut.AppendFormat("Current party is now '{0}'", findpartyfolder());
            camp_update();
        }

        void party_new(string arg)
        {
            if (!arg.StartsWith("_party/"))
            {
                arg = "_party/" + arg;
            }

            if (GameHost.Worker.existskey(arg))
            {
                sbOut.AppendFormat("Party '{0}' already exists.\n", arg);
                sbOut.AppendFormat("Current party is still '{0}'", findpartyfolder());
                return;
            }

            GameHost.Worker.note3("_party", "current", arg);
            GameHost.Worker.note3(arg, ".", "no");
            sbOut.AppendFormat("Current party is now '{0}'", findpartyfolder());
            camp_update();
        }

        void exec_victim(string s)
        {
            List<string> people;

            ParseToMap(s);

            int count = IntArg("count", 1);
            int otherArgs = hashArgs.Count;
            if (hashArgs.ContainsKey("count"))
                otherArgs--;

            int totalArgs = listArgs.Count + otherArgs;

            if (totalArgs == 0)
            {
                people = getpresent();
            }
            else if (listArgs.Count == 1 && listArgs[0] == "-help")
            {
                dumpCommandHelp();
                return;
            }
            else
            {
                people = listArgs;
            }

            foreach (var k in hashArgs.Keys)
            {
                if (k == "count")
                    continue;

                int c = IntArg(k, 0);

                for (int i = 0; i < c; i++)
                {
                    people.Add(k);
                }
            }

            if (people.Count > 0)
            {
                if (!fTerse)
                {
                    sbOut.AppendFormat("From {0} possible victims...\n", people.Count);
                }
                for (int i = 0; i < count; i++)
                {
                    int rnd = r.Next() % people.Count;

                    if (count > 1)
                        sbOut.AppendFormat("{0}) ", i + 1);

                    if (fTerse)
                    {
                        sbOut.AppendFormat("{0}: {1}\n", rnd + 1, people[rnd]);
                    }
                    else
                    {                       
                        sbOut.AppendFormat("The lucky winner is #{0}: {1}\n", rnd + 1, people[rnd]);
                    }
                }
            }
            else
            {
                sbOut.AppendFormat("Drat! No victims available.\n");
            }
        }

        void exec_find(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 2)
            {
                dumpCommandHelp();
                return;
            }

            List<string> matches = new List<string>();
            for (int i = 1; i < listArgs.Count; i++)
                matches.Add(listArgs[i]);

            var results= findkeysgeneral(listArgs[0], matches.ToArray());

            int min = IntArg("min", System.Int32.MinValue);
            int max = IntArg("max", System.Int32.MaxValue);
            for (int i = 0; i < results.Count; i++)
            {
                int intv = -5000;
                bool result = Int32.TryParse(results[i].val, out intv);
                if (intv < min || intv > max)
                {
                    results.RemoveAt(i);
                    i--;
                }
            }

            results.Sort((t1, t2) =>
                {
                    int intv1 = 0;
                    int intv2 = 0;
                    Int32.TryParse(t1.val, out intv1);
                    Int32.TryParse(t2.val, out intv2);

                    if (intv1 < intv2) return 1;  // sort bigget to smallest
                    if (intv1 > intv2) return -1;
                    return 0;
                }
            );

            int top = IntArg("top", 20);
            if (top > 0 && results.Count > top)
            {
                sbOut.AppendFormat("{0} matches total, showing the top {1}\n", results.Count, top);
                results.RemoveRange(top, results.Count - top);
            }

            WriteResults(results);
        }

        string FindWho(string who, bool fSilent)
        {
            if (who == "general" || who == "all" || who == "party")
                return who;

            if (who == "me")
                who = roller;

            List<string> allPlayers = getallplayers();

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
                        if (!fSilent) sbOut.AppendFormat("{0} matches more than one character: {1}, {2}\n", who, mem, member);
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

            if (!fSilent) sbOut.AppendFormat("{0} not found in players.\n", who);
            return null;
        }

        void exec_train(string s)
        {
            // common typo addressed
            s = s.Replace("=", ":");

            ParseToMap(s);

            if (listArgs.Count < 2)
            {
                dumpCommandHelp();
                return;
            }

            string gain = StringArg("gain", "1d6-2");
            string bookpct = StringArg("book");
            string bookgain = StringArg("bookgain", "");
            string skill = StringArg("skill");
            string count = StringArg("count", "1");
            string session = StringArg("session");
            string hours = StringArg("hours");

            if (hours != "" || skill != "")
                count = "";

            if (skill != "")
                hours = "";

            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            var check = ResolveToKey(who, true);
            if (check == null)
                return;

            var basics = getbasics(who, true);
            int bonus = ComputeBonus(check, basics);

            int max = -1;
            int cur = -1;

            GetRelevantStatLimits(check, basics, ref max, ref cur);

            roller = who;
           
            if (max > 0)
            {
                sbOut.AppendFormat("Training: {0}/{1} from {2}\n", check.dir, check.key, check.val); 

                if (cur >= max)
                {
                    sbOut.AppendFormat("{0} is already maxed out at {1}\n", check.key, check.val);
                    return;
                }

                if (gain == "1d6-2")
                {
                    gain = "1";
                }

                var b = new StringBuilder();
                if (session != "")
                    b.AppendFormat("{0},", session);

                if (skill != "")
                    b.AppendFormat("skill={0},", skill);

                if (count != "")
                    b.AppendFormat("count={0},", count);

                if (hours != "")
                    b.AppendFormat("hours={0},", hours);

                b.AppendFormat("{0}, {1}, {2}", cur, max, gain);

                if (bookgain == "")
                    bookgain = "1";

                if (bookpct != "")
                    b.AppendFormat(",{0}, {1}", bookpct, bookgain);

                train_stat(b.ToString());
            }
            else
            {
                int val = GetAdjustedSkill(check, basics);

                sbOut.AppendFormat("Training: {0}/{1} from {2}\n", check.dir, check.key, val); 

                var b = new StringBuilder();
                if (session != "")
                    b.AppendFormat("{0},", session);

                if (skill != "")
                    b.AppendFormat("skill={0},", skill);

                if (count != "")
                    b.AppendFormat("count={0},", count);

                if (hours != "")
                    b.AppendFormat("hours={0},", hours);

                b.AppendFormat("{0}, {1}, {2}", val, bonus, gain);

                if (bookgain == "")
                    bookgain = "2";

                if (bookpct != "")
                    b.AppendFormat(",{0}, {1}", bookpct, bookgain);

                train_skill(b.ToString());                
            }
        }

        static void GetRelevantStatLimits(StringTriple check, CharBasics basics, ref int max, ref int cur)
        {
            switch (check.key)
            {
                case "STR": cur = basics._str; max = basics.max_str; break;
                case "SIZ": cur = basics._siz; max = basics.max_siz; break;
                case "CON": cur = basics._con; max = basics.max_con; break;
                case "INT": cur = basics._int; max = basics.max_int; break;
                case "POW": cur = basics._pow; max = basics.max_pow; break;
                case "DEX": cur = basics._dex; max = basics.max_dex; break;
                case "APP": cur = basics._app; max = basics.max_app; break;
            }
        }

        int GetAdjustedSkill(StringTriple check, CharBasics basics)
        {
            int val = 0;
            Int32.TryParse(check.val, out val);

            if (check.dir.EndsWith("_spells"))
            {
                if (basics.wizardry != 0)
                    val += basics.encumberence * 5;
                else if (basics.species.ToLower().Contains("dwarf") || basics.species.ToLower().Contains("mostal"))
                    val += basics.enc_less_armor;
                else
                    val += basics.encumberence;

                return val;
            }

            switch (check.key.ToLower())
            {
                case "jump":
                case "dodge":
                case "sneak":
                case "sprint":
                    val = val + basics.encumberence;
                    break;
                case "swim":
                    val = val + basics.encumberence * 5;
                    break;
            }

            return val;
        }

        int ComputeBonus(StringTriple check, CharBasics basics)
        {
            int bonus = 0;

            if (check.key == "attack")
            {
                bonus = basics.attack;

                if (check.dir.Contains("/left_") || check.dir.Contains("/l_"))
                {
                    sbOut.AppendFormat("Adjusting for left hand skill\n");
                    bonus -= 10;
                }
            }
            else if (check.key == "parry")
            {
                bonus = basics.parry;
                if (check.dir.Contains("/left_") || check.dir.Contains("/l_"))
                {
                    sbOut.AppendFormat("Adjusting for left hand skill\n");
                    bonus -= 10;
                }
            }
            else if (check.dir.EndsWith("_spells"))
            {
                if (basics.wizardry != 0)
                    bonus = basics.wizardry;
                else
                    bonus = basics.magic;
            }
            else if (check.dir.EndsWith("_school"))
            {
                bonus = basics.magic;
            }
            else if (check.dir.EndsWith("/perception"))
            {
                bonus = basics.perception;
            }
            else if (check.dir.EndsWith("/magic"))
            {
                bonus = basics.magic;
            }
            else if (check.dir.EndsWith("/communication"))
            {
                bonus = basics.communication;
            }
            else if (check.dir.EndsWith("/stealth"))
            {
                bonus = basics.stealth;
            }
            else if (check.dir.EndsWith("/knowledge"))
            {
                bonus = basics.knowledge;
            }
            else if (check.dir.EndsWith("/alchemy"))
            {
                bonus = basics.alchemy;
            }
            else if (check.dir.EndsWith("/agility"))
            {
                bonus = basics.agility;
            }
            else if (check.dir.EndsWith("/manipulation"))
            {
                bonus = basics.manipulation;
            }
            return bonus;
        }

        StringTriple ResolveToKey(string who, bool suppressBuffs)
        {
            StringTriple check = null;
            int n = -1;

            List<string> matches = new List<string>();
            for (int i = 1; i < listArgs.Count; i++)
            {
                if (listArgs[i].StartsWith("#"))
                {
                    Int32.TryParse(listArgs[i].Substring(1), out n);
                }
                else
                {
                    matches.Add(listArgs[i].ToLower());
                }
            }

            var results = findkeys(who, matches.ToArray(), suppressBuffs);

            for (int i = 0; i < results.Count; i++)
            { 
                var d = results[i].dir;
                if (!d.Contains("/_wpn/"))
                    continue;

                var k = results[i].key;
                if (k== "sr" || k == "dmg" || k == "ap")
                {
                    results.RemoveAt(i);
                    i--;
                }
            }

            if (results.Count == 0)
            {
                sbOut.AppendFormat("There were no matching skills or abilities.\n");
                return null;
            }

            if (results.Count > 1 && (n <= 0 || n > results.Count))
            {
                WriteResults(results);
                return null;
            }

            if (results.Count == 1)
                check = results[0];
            else
                check = results[n - 1];

            return check;
        }

        long LongArg(string key)
        {
            return LongArg(key, 0);
        }

        long LongArg(string key, long def)
        {
            long val;
            if (Int64.TryParse(StringArg(key), out val))
                return val;

            return val;
        }

        int IntArg(string key)
        {
            return IntArg(key, 0);
        }

        int IntArg(string key, int def)
        {
            int val;
            if (Int32.TryParse(StringArg(key), out val))
                return val;

            return def;
        }

        bool BoolArg(string key)
        {
            var arg = StringArg(key);
            return arg == "yes" || arg == "y";
        }

        string StringArg(string key)
        {
            return StringArg(key, "");
        }

        string StringArg(string key, string def)
        {
            if (hashArgs.ContainsKey(key))
                return hashArgs[key];
            else
                return def;
        }

        void exec_check(string s)
        {
            ParseToMap(s);

            if (listArgs.Count < 2)
            {
                dumpCommandHelp();
                return;
            }

            string pct = StringArg("pct");
            string chk = StringArg("chk");
            string gain = StringArg("gain", "1d6");

            if (chk != "")
                chk = chk.Replace(',', '^');

            if (chk != "" && !chk.Contains("^"))
            {
                sbOut.AppendFormat("the check override should loook like chk:nn,nn\n");
                return;
            }

            bool fRemove = BoolArg("remove");
            bool fReplace = BoolArg("replace");
            bool fManual = BoolArg("manual");

            if (fManual)
            {
                if (chk == "" && pct == "")
                {
                    sbOut.Append("You have to specify the chk or pct role to gain in a manual check\n");
                    return;
                }

                if (chk == "")
                {
                    chk = pct + "%";
                    pct = "";
                }
            }

            string who = FindWho(listArgs[0], fSilent: false);
            if (who == null)
                return;

            string logicalKey = null;
            StringTriple check = null;

            if (!fManual)
            {
                check = ResolveToKey(who, true);
                if (check == null)
                    return;

                logicalKey = check.dir.Replace('/', '|') + "|" + check.key;
            }
            else
            {
                // make a dummy logical key
                logicalKey = who;
                for (int i = 1; i < listArgs.Count; i++)
                {
                    if (!listArgs[i].StartsWith("#"))
                    {
                        logicalKey = logicalKey + "|" + listArgs[i].ToLower();
                    }
                }
                check = new StringTriple { dir = "__dummy___", key = "__dummy___", val = "__dummy___"};
            }

            if (!fReplace && !fRemove && exists("_checks", logicalKey))
            {
                sbOut.AppendFormat("{0} already has the check for {1}\n", who, logicalKey);
                return;
            }

            if (fRemove)
            {
                GameHost.Worker.del_key("_checks", logicalKey);
                sbOut.AppendFormat("Check Removed: {0}\n", logicalKey);
                return;
            }

            string timeStr = String.Format("time:{1}", gain, DateTime.UtcNow.Ticks);

            var basics = getbasics(who, true);
            int bonus = ComputeBonus(check, basics) + runebonus(who, logicalKey);

            int max = -1;
            int cur = -1;

            GetRelevantStatLimits(check, basics, ref max, ref cur);

            if (max > 0)
            {
                if (pct == "" && max > 0 && cur > 0)
                {
                    if (cur > max)
                        pct = "0";
                    else
                        pct = ((max - cur) * 5).ToString();
                }

                if (gain == "1d6")
                {
                    gain = "1";
                }

                int junk;
                if (pct == "" || !Int32.TryParse(pct, out junk))
                {
                    sbOut.AppendFormat("{0} check requires that you specify MAX_{0} or pct:nn\n", check.key);
                    return;
                }
                else
                {
                    if (pct == "0")
                    {
                        sbOut.AppendFormat("{0} is already maxed out\n", check.key);
                        return;
                    }

                    // check for rune bonus
                    if (bonus > 0)
                    {
                        int newpct;
                        if (Int32.TryParse(pct, out newpct))
                        {
                            newpct += bonus;
                            pct = newpct.ToString();
                        }
                    }

                    string payload = String.Format("roll:{0}% gain:{1} {2}", pct, gain, timeStr);
                    sbOut.AppendFormat("Check Added:\n{0} roll:{1}% gain:{2} ({3})\n", logicalKey, pct, gain, DateTime.Now);
                    GameHost.Worker.note3("_checks", logicalKey, payload);
                }
            }
            else if (logicalKey.EndsWith("|mysticism"))
            {
                int val = GetAdjustedSkill(check, basics);
                chk = String.Format("{0}%", val);

                string payload = String.Format("roll:{0} gain:{1} {2}", chk, gain, timeStr);
                sbOut.AppendFormat("Check Added:\n{0} roll:{1} gain:{2} ({3})\n", logicalKey, chk, gain, DateTime.Now);
                GameHost.Worker.note3("_checks", logicalKey, payload);
            }
            else
            {
                if (chk == "")
                {
                    int val = GetAdjustedSkill(check, basics);
                    chk = String.Format("{0}^{1}", val, bonus);
                }

                string payload = String.Format("roll:{0} gain:{1} {2}", chk, gain, timeStr);
                sbOut.AppendFormat("Check Added:\n{0} roll:{1} gain:{2} ({3})\n", logicalKey, chk, gain, DateTime.Now);
                GameHost.Worker.note3("_checks", logicalKey, payload);
            }
        }

        void exec_checks(string s)
        {
            ParseToMap(s);

            if (listArgs.Count == 0)
            {
                dumpCommandHelp();
                return;
            }

            string who = FindWhoOrGroup(listArgs[0]);

            bool fResolve = BoolArg("resolve");
            bool fHeroquest = BoolArg("heroquest");

            var checks = findchecks(who);
            var loot = findloot(who);
            var used = findused(who);

            int icheck = 0;
            int iloot = 0;
            int iused = 0;

            var prevroller = "";

            int checkcount = 0;
            string pityCheck = "";
            string pityGain = "";
            string checkRoller = "";
            string lootRoller = "";
            string usedRoller = "";
            string lastCheckRoller = "";

            while (icheck < checks.Count || iloot < loot.Count || iused < used.Count)
            {
                StringPair checkResult = new StringPair();
                StringPair lootResult = new StringPair();
                StringPair usedResult = new StringPair();
                checkRoller = "";
                lootRoller = "";
                usedRoller = "";
                  
                if (iloot < loot.Count)
                {
                    lootResult = loot[iloot];
                    lootRoller = lootResult.key.Substring(0, lootResult.key.IndexOf('|'));
                }

                if (iused < used.Count)
                {
                    usedResult = used[iused];
                    usedRoller = usedResult.key.Substring(0, usedResult.key.IndexOf('|'));
                }

                if (icheck < checks.Count)
                {
                    checkResult = checks[icheck];
                    checkRoller = checkResult.key.Substring(0, checkResult.key.IndexOf('|'));
                }

                roller = checkRoller;

                if (roller == "" || (lootRoller != "" && lootRoller.CompareTo(roller) < 0))
                {
                    roller = lootRoller;
                }

                if (roller == "" || (usedRoller != "" && usedRoller.CompareTo(roller) < 0))
                {
                    roller = usedRoller;
                }

                bool newRoller = prevroller != "" && roller != prevroller;
                if (newRoller)
                {
                    new_roller(fResolve, prevroller, ref checkcount, ref pityCheck, pityGain);                   
                }

                prevroller = roller;

                if (roller == checkRoller) {
                    report_one_check(fResolve, fHeroquest, newRoller, checkcount, ref pityCheck, ref pityGain, checkResult);
                    icheck++;
                    lastCheckRoller = checkRoller;
                    continue;
                }

                if (roller == lootRoller) {
                    if (roller == lastCheckRoller)
                    {
                        lastCheckRoller = "";
                        sbOut.AppendFormat("-----\n");
                    }
                    report_one_loot(fResolve, lootResult);
                    iloot++;
                    continue;
                }

                if (roller == usedRoller)
                {
                    if (roller == lastCheckRoller)
                    {
                        lastCheckRoller = "";
                        sbOut.AppendFormat("-----\n");
                    }
                    report_one_used(fResolve, usedResult);
                    iused++;
                    continue;
                }
            }

            if (fResolve && pityCheck != "nopity" && pityCheck != "")
            {
                sbOut.AppendFormat("-----\nPITY CHECK\n{0}\n", pityCheck); 
                PrintRoll(pityGain);
            }
        }

        void report_one_check(bool fResolve, bool fHeroquest, bool newRoller, int checkcount, ref string pityCheck, ref string pityGain, StringPair result)
        {
            checkcount++;

            if (fResolve && !newRoller)
            {
                sbOut.AppendFormat("-----\n");
            }

            ParseToMap(result.val);

            string roll = StringArg("roll");
            string gain = StringArg("gain");
            string time = StringArg("time");

            if (pityCheck != "nopity")
            {
                if (r.Next(checkcount) == 0)
                {
                    pityCheck = result.key;
                    pityGain = gain;
                }
            }

            long ticks = 0;

            if (Int64.TryParse(time, out ticks))
            {
                DateTime t = new DateTime(ticks, DateTimeKind.Utc);
                time = string.Format("{0}", t.ToLocalTime());
            }
            else
            {
                time = "bogus date";
            }

            sbOut.AppendFormat("{0} roll:{1} gain:{2} ({3})\n", result.key, roll, gain, time);

            if (fResolve)
            {
                if (fHeroquest || PrintRoll(roll) > 0)
                {
                    pityCheck = "nopity";
                    PrintRoll(gain);
                }

                GameHost.Worker.del_key("_checks", result.key);
            }
        }

        void new_roller(bool fResolve, string prevroller, ref int checkcount, ref string pityCheck, string pityGain)
        {
            if (fResolve)
            {
                if (pityCheck != "nopity" && pityCheck != "")
                {
                    sbOut.AppendFormat("-----\nPITY CHECK\n{0}\n", pityCheck);

                    var r = roller;
                    roller = prevroller;
                    PrintRoll(pityGain);
                    roller = r;
                }
            }

            sbOut.AppendFormat("-------------------\n");

            pityCheck = "";
            checkcount = 0;
        }

        void WriteResults(List<StringTriple> results)
        {
            sbOut.AppendFormat("{0} matches\n", results.Count);
            int idx = 1;
            foreach (var result in results)
            {
                sbOut.AppendFormat("{0}) key: {1}/{2} value: {3}\n", idx, result.dir, result.key, result.val);
                idx++;
            }
        }

        int PrintRoll(String s)
        {
            ImproveRoller();

            int r = eval_roll(s);
            sbOut.AppendFormat("{2}: {0}=> {1}\n", sb1, sb2, roller);
            return r;
        }

        void ImproveRoller()
        {
            if (roller != "roller")
            {
                // silent find
                string who = FindWho(roller, true);
                if (who != null)
                    roller = who;
            }
        }

        string SetRoller(string newRoller)
        {
            if (newRoller != "me")
            {
                roller = newRoller;
            }

            ImproveRoller();
            return roller;
        }

        string SetRoller(string newRoller, string me)
        {
            roller = newRoller;
            if (roller == "me")
                roller = me;

            ImproveRoller();
            return roller;
        }

        int eval_roll(String s)
        {
            rollText = s;
            idx = 0;
            sb1.Length = 0;
            sb2.Length = 0;
            eval_mode = Evalmode.normal;
            peekToken = null;
            totalName = "Total";

            string tok = PeekToken();
            if (tok == "@")
            {
                ReadToken();

                var newRoller = ReadToken();

                if (newRoller == null)
                    throw new ParseException();

                SetRoller(newRoller);
            }

            var result = eval_expr();

            return result;
        }

        int eval_expr()
        {
            int inLen = sb2.Length;
            int v1 = eval_clause();
            bool fSplitResult = false;

            for (;;)
            {
                string s = PeekToken();

                if (!fSuppressTotal)
                {
                    sb2.AppendFormat("=> {0} {1}", totalName, v1);

                    if (s == ",")
                        fSplitResult = true;
                }

                fSuppressTotal = true;
                totalName = "Total";

                if (s == null || s == ")")
                    break;

                if (s != "&" && s != "|" && s != ",")
                    throw new ParseException();

                ReadToken();

                bool fSkip = false;

                if (s == "&" && v1 <= 0)
                    fSkip = true;

                if (s == "|" && v1 > 0)
                    fSkip = true;

                if (fSkip)
                {
                    StringBuilder sbT1 = sb1;
                    StringBuilder sbT2 = sb2;

                    sb1 = new StringBuilder();
                    sb2 = new StringBuilder();
                    eval_clause();
                    sb1 = sbT1;
                    sb2 = sbT2;
                }
                else
                {

                    sb1.Append(s);
                    sb1.Append(" ");
                    sb2.Append(s);
                    sb2.Append(" ");

                    if (fSplitResult)
                        sb2.Insert(inLen, "\r\n");

                    inLen = sb2.Length;
                    v1 = eval_clause();
                }
            }

            if (fSplitResult)
                sb2.Insert(inLen, "\r\n");

            return v1;
        }

        int eval_clause()
        {
            int v1 = eval_check();
            string s = PeekToken();
            if (s == "$")
            {
                ReadToken();
                v1 = eval_cpct(v1);
                fSuppressTotal = true;
            }
            else if (s == "%")
            {
                sb1.Append(s);
                sb1.Append(" ");
                ReadToken();
                v1 = eval_pct(v1);
                fSuppressTotal = true;
            }
            if (s == "@")
            {
                sb1.Append(s);
                sb1.Append(" ");
                ReadToken();
                v1 = eval_pow(v1);
                fSuppressTotal = true;
            }

            return v1;
        }

        int eval_check()
        {
            int v1 = eval_sum();

            for (; ; )
            {
                string s = PeekToken();
                if (s != "^")
                    return v1;

                sb1.Append(s);
                sb1.Append(" ");
                sb2.Append(s);
                sb2.Append(" ");

                ReadToken();

                int v2 = eval_sum();

                fSuppressTotal = false;
                v1 = eval_chk(v1, v2);
                fSuppressTotal = true;
            }
        }


        int eval_sum()
        {
            fSuppressTotal = true;
            int v1 = eval_term();

            for (; ; )
            {
                string s = PeekToken();
                if (s == null)
                    return v1;

                if (s != "+" && s != "-")
                    break;

                ReadToken();

                sb1.Append(s);
                sb1.Append(" ");
                sb2.Append(s);
                sb2.Append(" ");

                int v2 = eval_term();

                if (s == "+")
                    v1 += v2;
                else
                    v1 -= v2;

                fSuppressTotal = false;
            }

            return v1;
        }

        int eval_term()
        {
            int v1 = eval_factor();

            for (; ; )
            {
                string s = PeekToken();
                if (s == null)
                    return v1;

                if (s != "*" && s != "/")
                    break;

                fSuppressTotal = false;

                sb1.Append(s);
                sb1.Append(" ");
                sb2.Append(s);
                sb2.Append(" ");

                ReadToken();

                int v2 = eval_factor();

                if (s == "*")
                {
                    v1 *= v2;
                }
                else
                {
                    v1 = (v1 + v2 / 2) / v2;
                }
            }

            return v1;
        }

        int eval_factor()
        {
            StringBuilder sbT2 = sb2;

            sb2 = new StringBuilder();
            int v1 = eval_number();
            bool fExpr = false;

            StringBuilder sbT = sb2;
            sb2 = sbT2;

            for (; ; )
            {
                string s = PeekToken();
                if (s == null || (s != "d" && s != "D"))
                {
                    break;
                }

                ReadToken();
                sb1.Append("d ");
                fExpr = true;

                sbT2 = sb2;
                sb2 = new StringBuilder();
                int v2 = eval_number();
                sb2 = sbT2;

                if (eval_mode != Evalmode.normal)
                {
                    if (eval_mode == Evalmode.max)
                        v1 *= v2;

                    sb2.AppendFormat("{0} ", v1);
                }
                else
                {
                    int t = 0;

                    if (v1 > 1)
                    {
                        sb2.Append("( ");
                        fSuppressTotal = false;
                    }

                    for (int i = 0; i < v1; i++)
                    {
                        int n = 1 + (r.Next() % v2);
                        sb2.AppendFormat("{0} ", n);
                        t += n;
                    }

                    if (v1 > 1)
                        sb2.Append(") ");

                    v1 = t;
                }
            }

            if (!fExpr)
            {
                sb2.Append(sbT);
            }

            return v1;
        }

        int eval_number()
        {
            Evalmode evSaved = eval_mode;

            string s = ReadToken();

            if (s == null)
                throw new ParseException();

            if (s == "min")
            {
                eval_mode = Evalmode.min;
                s = PeekToken();
                if (s != "(")
                    throw new ParseException();

                sb1.Append("min ");

                int v = eval_number();
                eval_mode = evSaved;
                return v;
            }
            else if (s == "max")
            {
                eval_mode = Evalmode.max;
                s = PeekToken();
                if (s != "(")
                    throw new ParseException();

                sb1.Append("max ");

                int v = eval_number();
                eval_mode = evSaved;
                return v;
            }

            if (s == "(")
            {
                sb1.Append("( ");
                sb2.Append("( ");
                int v = eval_expr();
                s = ReadToken();
                if (s != ")")
                    throw new ParseException();
                sb1.Append(") ");
                sb2.Append(") ");
                return v;
            }

            if (s == "-")
            {
                s = ReadToken();
                if (s[0] >= '0' && s[0] <= '9')
                {
                    int v = -Int32.Parse(s);
                    sb1.AppendFormat("{0} ", v);
                    sb2.AppendFormat("{0} ", v);
                    return v;
                }
                else
                    throw new ParseException();
            }

            if (IsAlnum(s[0]))
            {
                // find and concatenate as many alphanumeric sequences as possible
                // but not if the seqence is just "d" like in 1d3
                for (;;)
                {
                    var t = PeekToken();

                    if (t == null || s == "d" || t == "d" || !IsAlnum(t[0]))
                        break;

                    s = s + ReadToken();
                }

                // now we have the combined token, find the first thing that isn't 
                // a number in it
                int i;
                for (i = 0; i < s.Length; i++)
                {
                    if (s[i] < '0' || s[i] > '9')
                        break;
                }

                // if it's all numeric then it's just a number, parse it
                if (i == s.Length)
                {
                    int v = Int32.Parse(s);
                    sb1.AppendFormat("{0} ", v);
                    sb2.AppendFormat("{0} ", v);
                    return v;
                }
                else
                {
                    // we need to look ahead a bit and see what the thing is
                    // to decide what to do
                    var t = PeekToken();

                    // if it's a colon then we're talking about a label
                    // that means we don't do anything with the text
                    // basically we proceed as though we never saw it
                    if (t == ":")
                    {
                        sb1.Append(s);
                        sb1.Append(": ");
                        totalName = s;
                        fSuppressTotal = false;
                        ReadToken();

                        return eval_number();
                    }

                    // if the next token is a paren, then we're talking about
                    // a table lookup with an override for the table roll
                    // we're going to evaluate what's next and then 
                    // proceed to evaluate the table
                    if (t == "(")
                    {
                        return eval_table(s, true);
                    }
                    
                    return eval_table(s, false);
                }               
            }

            throw new ParseException();
        }

        bool IsAlnum(char c)
        {
            return c >= '0' && c <= '9' ||
                   c >= 'a' && c <= 'z' ||
                   c >= 'A' && c <= 'Z' ||
                   c == '_';
        }

        int eval_table(string table, bool rollIsInline)
        {
            int result;
            if (lookup_statroll(roller, table, out result))
                return result;

            foreach (RollTableEntry rte in tables)
            {
                if (rte.key == table)
                {
                    string desc = rte.name;
                    string roll = rte.roll;

                    int v;
                    if (rollIsInline)
                    {
                        v = perform_inline_roll(desc, roll);
                    }
                    else
                    {
                        v = perform_nested_roll(desc, roll);
                    }

                    if (rte.values != null) foreach (string str in rte.values)
                        {
                            int i = str.IndexOf(' ');
                            if (i <= 0)
                                continue;

                            String ns = str.Substring(0, i);
                            int n = Int32.Parse(ns);
                            if (v <= n)
                            {
                                sb2.Append("=> ");
                                sb2.Append(str.Substring(i + 1));
                                break;
                            }
                        }
                    return v;
                }
            }
            throw new ParseException();
        }

        int perform_inline_roll(string desc, string roll)
        {
            sb2.Append(desc);
            sb2.Append(" => ");
            int v = eval_number();

            return v;
        }

        int perform_nested_roll(string desc, string roll)
        {
            int idxSaved = idx;
            string cmdSaved = rollText;
            string peekTokenSaved = peekToken;
            peekToken = null;

            rollText = roll;
            idx = 0;
            sb1.Append("( ");
            sb2.Append("( ");
            sb2.Append(desc);
            sb2.Append(" => ");
            int v = eval_expr();
            sb1.Append(") ");
            sb2.Append(") ");

            rollText = cmdSaved;
            idx = idxSaved;
            peekToken = peekTokenSaved;
            return v;
        }

        int eval_pow(int pow)
        {
            int n = 1 + r.Next() % 100;

            int k = pow + 10 - (n + 4) / 5;

            if (!fSuppressTotal)
                sb2.AppendFormat("=> {0} ", pow);

            sb2.AppendFormat("PvP roll => {0} ", n);

            if (n <= 5)
            {
                sb2.AppendFormat("Critical! Defeats {0}! ", pow + 20);
                return pow + 20;
            }
            else if (n >= 96)
            {
                sb2.AppendFormat("Fumble! ");
                return -1;
            }
            else if (k <= 0)
            {
                sb2.AppendFormat("Failed! ");
                return -1;
            }
            else
            {
                sb2.AppendFormat("Defeats {0}! ", k);
                return k;
            }
        }

        int eval_chk(int pct, int bonus)
        {
            int n = 1 + r.Next() % 100;

            if (!fSuppressTotal)
                sb2.AppendFormat("=> {0}% check, {1}% bonus ", pct, bonus);

            sb2.Length = sb2.Length - 1;
            sb2.AppendFormat(" => {0} ", n);

            if (bonus < 0)
            {
                pct -= bonus;
                bonus = 0;
            }

            if (n > pct || n == 100 || n <= bonus)
            {
                sb2.Append("Succeeded! ");
                return 1;
            }
            else
            {
                sb2.Append("Failed. ");
                return 0;
            }
        }

        int eval_cpct(int pct)
        {
            int n = 1 + r.Next() % 100;
            int c = 1 + r.Next() % 100;

            int ceremony;
            if (lookup_statroll_invisibly(roller, "ceremony", out ceremony))
            {
                if ((c <= ceremony && c <= 95) || c <= 5)
                {
                    int bonus = ceremony;
                    if (pct < bonus) bonus = pct;
                    pct += bonus;
                    sb1.AppendFormat("+ {0}% [{1} ceremony] ", bonus, ceremony);
                    sb2.AppendFormat("+ {0}% => {1} ", bonus, pct);
                }
                else
                {
                    sb1.AppendFormat("% [ceremony failed] ");
                }
            }
            else
            {
                sb1.AppendFormat("% [no ceremony skill] ");
            }
            return eval_pct(pct, n);
        }

        int eval_pct(int pct)
        {
            int n = 1 + r.Next() % 100;
            return eval_pct(pct, n);
        }

        int eval_pct(int pct, int n)
        {
            // compute minimum and maximum hit chance
            int c = pct;
            if (c < 5) c = 5;
            if (c > 95) c = 95;

            if (!fSuppressTotal)
                sb2.AppendFormat("=> {0} ", pct);

            sb2.Length = sb2.Length - 1;
            sb2.AppendFormat("% roll => {0} ", n);

            if (n <= c)
            {
                if (n == 1 || n <= (pct + 10) / 20)
                {
                    sb2.Append("Critical! ");
                    return 3;
                }
                else if (n <= (pct + 2) / 5)
                {
                    sb2.Append("Special! ");
                    return 2;
                }
                else if (n <= 2 * ((pct + 2) / 5))
                {
                    sb2.Append("Good Hit! ");
                    return 1;
                }
                else
                {
                    sb2.Append("Hit! ");
                    return 1;
                }
            }
            else
            {
                if (n == 100 || n > 100 - (110 - pct) / 20)
                {
                    sb2.Append("Fumble! ");
                    return -1;
                }
                else
                {
                    sb2.Append("Missed! ");
                    return 0;
                }
            }
        }


        String ReadToken()
        {
            if (peekToken == null)
            {
                PeekToken();
            }

            string ret = peekToken;
            peekToken = null;
            return ret;
        }

        char getc()
        {
            if (idx >= rollText.Length)
                return '\0';

            return rollText[idx++];
        }

        String PeekToken()
        {
            if (peekToken != null)
                return peekToken;

            peekToken = GetToken();

            return peekToken;
        }

        String GetToken()
        {

            if (idx >= rollText.Length)
                return null;

            char ch = '\0';

            for (; ; )
            {
                ch = getc();
                if (ch != ' ' && ch != '\t')
                    break;
            }

            int idx1 = idx - 1;

            if (ch >= 'a' && ch <= 'z' ||
              ch >= 'A' && ch <= 'Z' ||
              ch == '_')
            {

                while ('\0' != (ch = getc()))
                    if (ch >= 'a' && ch <= 'z' ||
                      ch >= 'A' && ch <= 'Z' ||
                      ch == '_')
                        continue;
                    else
                        break;

                if (ch != '\0') idx--;
                return rollText.Substring(idx1, idx - idx1);
            }

            if (ch >= '0' && ch <= '9')
            {
                while ('\0' != (ch = getc()))
                    if (ch >= '0' && ch <= '9')
                        continue;
                    else
                        break;

                if (ch != '\0') idx--;
                return rollText.Substring(idx1, idx - idx1);
            }

            switch (ch)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '$':
                case '%':
                case '^':
                case '@':
                case ':':
                case '&':
                case '|':
                case '(':
                case ')':
                case ',':
                    return rollText.Substring(idx1, 1);
            }

            return null;
        }

        public static int GetRemainingHours(string roller)
        {
            lock (hashHoursRemaining)
            {
                if (hashHoursRemaining.Contains(roller))
                    return (int)hashHoursRemaining[roller];
                else
                    return 0;
            }
        }

        public static void SetRemainingHours(int hours, string roller)
        {
            lock (hashHoursRemaining)
            {
                if (hashHoursRemaining.Contains(roller))
                    hashHoursRemaining.Remove(roller);

                hashHoursRemaining.Add(roller, hours);
            }
        }

        void train_mysticism(string str)
        {
            string[] args = str.Split(comma);

            // mtrn [1000],skill=90,25[,95]

            if (args.Length < 2)
                goto Usage;

            int hours = GetRemainingHours(roller);

            int iarg = 0;

            if (args[iarg][0] >= '0' && args[iarg][0]<='9')
            {
                if (!Int32.TryParse(args[iarg++], out hours))
                    goto Usage;
            }

            if (iarg + 2 > args.Length)
                goto Usage;
            
            string strStop = args[iarg++];
            int cur = eval_roll(args[iarg++]);
            int pctMaster = 0;
            int org = cur;

            if (iarg + 1 <= args.Length)
            {
                pctMaster = eval_roll(args[iarg++]);
            }

            string[] stops = strStop.Split(equal);
            if (stops.Length != 2)
                goto Usage;

            int maxcount = 0;
            int maxskill = 0;
            int maxhours = 0;

            if (stops[0].IndexOf("count") >= 0) maxcount = eval_roll(stops[1]);
            if (stops[0].IndexOf("hours") >= 0) maxhours = eval_roll(stops[1]);
            if (stops[0].IndexOf("skill") >= 0) maxskill = eval_roll(stops[1]);

            if (maxcount == 0 && maxskill == 0 && maxhours == 0)
                goto Usage;

            if (pctMaster == 0)
                sbOut.AppendFormat("{0} Training Mysticism: {1},{2}\n", roller, strStop, cur);
            else
                sbOut.AppendFormat("{0} Training Mysticism: {1},{2},{3}\n", roller, strStop, cur, pctMaster);

            int totalhours = 0;
            int totalcount = 0;

            for (;;)
            {
                int t = 224;

                if (hours < t)
                    break;

                if (maxcount != 0 && totalcount >= maxcount)
                    break;

                if (maxhours != 0 && totalhours + t > maxhours)
                    break;

                if (maxskill != 0 && cur >= maxskill)
                    break;

                totalcount++;
                totalhours += t;
                hours -= t;

                bool fBook = false;
                string g;

                int rnd = 1 + r.Next() % 100;
                if (rnd > cur + pctMaster || rnd > 95)
                {
                    g = "1";
                    if (!fTerse)
                        sbOut.AppendFormat("({2} hours) missed !pct {1}\n", roller, cur, t);
                }
                else
                {
                    g = "1d6";
                    if (rnd > cur)
                        fBook = true;
                }

                cur += eval_roll(g);

                if (!fTerse)
                    sbOut.AppendFormat("({3} hours) {0}=> {1} => skill now {2}{4}\n",
                      sb1, sb2, cur, t, fBook ? " (master)" : "");
            }

            sbOut.AppendFormat("{0} final skill {1}, amount gained {2} remaining hours:{3}\n", roller, cur, cur - org, hours);
            SetRemainingHours(hours, roller);
            return;

        Usage:
            dumpCommandHelp();
            return;
        }

        void train_skill(string str)
        {
            string[] args = str.Split(comma);

            if (args.Length < 4 || args.Length > 7)
                goto Usage;

            int iarg = 0;
            int hours = GetRemainingHours(roller);

            if (args.Length == 5 || args.Length == 7)
                hours = eval_roll(args[iarg++]);

            string strStop = args[iarg++];
            int cur = eval_roll(args[iarg++]);
            int bonus = eval_roll(args[iarg++]);
            string gain = args[iarg++];
            string gainBook = null;
            int pctBook = 0;
            int org = cur;


            if (iarg + 2 == args.Length)
            {
                pctBook = eval_roll(args[iarg++]);
                gainBook = args[iarg++];
            }

            string[] stops = strStop.Split(equal);
            if (stops.Length != 2)
                goto Usage;


            int maxcount = 0;
            int maxskill = 0;
            int maxhours = 0;

            if (stops[0].IndexOf("count") >= 0) maxcount = eval_roll(stops[1]);
            if (stops[0].IndexOf("hours") >= 0) maxhours = eval_roll(stops[1]);
            if (stops[0].IndexOf("skill") >= 0) maxskill = eval_roll(stops[1]);

            if (maxcount == 0 && maxskill == 0 && maxhours == 0)
                goto Usage;


            if (pctBook == 0)
                sbOut.AppendFormat("{5} Training: {0},{1},{2},{3},{4}\n",
                  hours, strStop, cur, bonus, gain, roller);
            else
                sbOut.AppendFormat("{7} Training: {0},{1},{2},{3},{4},{5},{6}\n",
                  hours, strStop, cur, bonus, gain, pctBook, gainBook, roller);

            int totalhours = 0;
            int totalcount = 0;

            for (; ; )
            {
                int t = cur;
                if (t < 0)
                    t = 1;

                if (hours < t)
                    break;

                int chk = cur;
                if (chk > 99) chk = 99;

                if (maxcount != 0 && totalcount >= maxcount)
                    break;

                if (maxhours != 0 && totalhours + t > maxhours)
                    break;

                if (maxskill != 0 && cur >= maxskill)
                    break;

                totalcount++;
                totalhours += t;
                hours -= t;
                bool fBook = false;
                string g;

                int rnd = 1 + r.Next() % 100;
                if (rnd > pctBook)
                {
                    rnd = 1 + r.Next() % 100;
                    if (rnd <= chk - bonus)
                    {
                        if (!fTerse)
                            sbOut.AppendFormat("({2} hours) missed !chk {1}\n", roller, chk - bonus, t);
                        continue;
                    }
                    g = gain;
                }
                else
                {
                    g = gainBook;
                    fBook = true;
                }

                cur += eval_roll(g);

                if (!fTerse)
                    sbOut.AppendFormat("({3} hours) {0}=> {1} => skill now {2}{4}\n",
                      sb1, sb2, cur, t, fBook ? " (book)" : "");
            }

            sbOut.AppendFormat("{3} final skill {0}, remaining hours {1}, amount gained {2}\n", cur, hours, cur - org, roller);
            SetRemainingHours(hours, roller);
            return;

        Usage:
            dumpCommandHelp();
            return;
        }


        void train_ki(string str)
        {
            string[] args = str.Split(comma);

            // ktrn [max_hours], skill=90,25,95,1d6-2

            if (args.Length < 4 || args.Length > 5)
                goto Usage;

            int hours = GetRemainingHours(roller);

            int iarg = 0;

            if (args[iarg][0] >= '0' && args[iarg][0]<='9')
            {
                if (!Int32.TryParse(args[iarg++], out hours))
                    goto Usage;
            }

            if (iarg + 4 != args.Length)
                goto Usage;

            
            string strStop = args[iarg++];
            int cur = eval_roll(args[iarg++]);
            int mastery = eval_roll(args[iarg++]);
            string gain = args[iarg++];

            int org = cur;

            string[] stops = strStop.Split(equal);
            if (stops.Length != 2)
                goto Usage;

            int maxcount = 0;
            int maxskill = 0;
            int maxhours = 0;

            if (stops[0].IndexOf("count") >= 0) maxcount = eval_roll(stops[1]);
            if (stops[0].IndexOf("hours") >= 0) maxhours = eval_roll(stops[1]);
            if (stops[0].IndexOf("skill") >= 0) maxskill = eval_roll(stops[1]);

            if (maxcount == 0 && maxskill == 0 && maxhours == 0)
                goto Usage;

            if (mastery == 0)
                goto Usage;

            sbOut.AppendFormat("{0} Training Ki: {1},{2},{3},{4}\n", roller, strStop, cur, mastery, gain);
     
            int totalhours = 0;
            int totalcount = 0;

            for (;;)
            {
                int t = mastery;

                if (hours < t)
                    break;

                if (maxcount != 0 && totalcount >= maxcount)
                    break;

                if (maxhours != 0 && totalhours + t > maxhours)
                    break;

                if (maxskill != 0 && cur >= maxskill)
                    break;

                totalcount++;
                totalhours += t;
                hours -= t;

                int rnd = 1 + r.Next() % 100;
                if (rnd <= cur)
                {
                    if (!fTerse)
                        sbOut.AppendFormat("({0} hours) missed !chk {1}\n", t, cur);
                    continue;
                }

                cur += eval_roll(gain);

                if (!fTerse)
                    sbOut.AppendFormat("({0} hours) {1} => {2} => skill now {3}\n", t, sb1, sb2, cur);
            }

            sbOut.AppendFormat("{0} final ki skill {1}, amount gained {2} remaining hours:{3}\n", roller, cur, cur - org, hours);
            SetRemainingHours(hours, roller);
            return;

        Usage:
            dumpCommandHelp();
            return;
        }

        void train_stat(string str)
        {
            string[] args = str.Split(comma);

            if (args.Length < 4 || args.Length > 7)
                goto Usage;

            int iarg = 0;

            int hours = GetRemainingHours(roller);

            if (args.Length == 5 || args.Length == 7)
                hours = eval_roll(args[iarg++]);

            string strStop = args[iarg++];
            int cur = eval_roll(args[iarg++]);
            int racemax = eval_roll(args[iarg++]);
            string gain = args[iarg++];
            string gainBook = null;
            int pctBook = 0;
            int org = cur;

            if (iarg + 2 == args.Length)
            {
                pctBook = eval_roll(args[iarg++]);
                gainBook = args[iarg++];
            }

            string[] stops = strStop.Split(equal);
            if (stops.Length != 2)
                goto Usage;

            int maxcount = 0;
            int maxskill = 0;
            int maxhours = 0;

            if (stops[0].IndexOf("count") >= 0) maxcount = eval_roll(stops[1]);
            if (stops[0].IndexOf("hours") >= 0) maxhours = eval_roll(stops[1]);
            if (stops[0].IndexOf("skill") >= 0) maxskill = eval_roll(stops[1]);

            if (maxcount == 0 && maxskill == 0 && maxhours == 0)
                goto Usage;


            if (pctBook == 0)
                sbOut.AppendFormat("{5} Training: {0},{1},{2},{3},{4}\n",
                  hours, strStop, cur, racemax, gain, roller);
            else
                sbOut.AppendFormat("{7} Training: {0},{1},{2},{3},{4},{5},{6}\n",
                  hours, strStop, cur, racemax, gain, pctBook, gainBook, roller);

            int totalhours = 0;
            int totalcount = 0;

            for (; ; )
            {
                int t = cur * 5;
                if (t < 0)
                    t = 1;

                if (hours < t)
                    break;

                if (cur >= racemax || cur < 1)
                    break;

                int pct = 5 * (racemax - cur);

                if (maxcount != 0 && totalcount >= maxcount)
                    break;

                if (maxhours != 0 && totalhours + t > maxhours)
                    break;

                if (maxskill != 0 && cur >= maxskill)
                    break;

                totalcount++;
                totalhours += t;
                hours -= t;
                bool fBook = false;
                string g;

                int rnd = 1 + r.Next() % 100;
                if (rnd > pctBook)
                {
                    rnd = 1 + r.Next() % 100;
                    if (rnd > pct)
                    {
                        if (!fTerse) sbOut.AppendFormat("({2} hours) missed !pct {1}\n", roller, pct, t);
                        continue;
                    }
                    g = gain;
                }
                else
                {
                    g = gainBook;
                    fBook = true;
                }

                cur += eval_roll(g);

                if (cur > racemax)
                    cur = racemax;

                if (!fTerse) sbOut.AppendFormat("({3} hours) {0}=> {1} => skill now {2}{4}\n",
                          sb1, sb2, cur, t, fBook ? " (book)" : "");
            }

            sbOut.AppendFormat("{3} final skill {0}, remaining hours {1}, amount gained {2}\n", cur, hours, cur - org, roller);
            SetRemainingHours(hours, roller);
            return;

        Usage:
            dumpCommandHelp();
            return;
        }

        // parse the incoming string into normal arguments plus a dictionary of arguments
        // all option arguments are accepted
        void ParseToMap(string s)
        {
            ParseToMap(s, null);
        }

        // parse the incoming string into normal arguments plus a dictionary of arguments that were of the form foo:bar
        // if the map is provided then only those prefixes can go into the dictionary
        void ParseToMap(string s, Dictionary<string, bool> validArgs)
        {
            hashArgs = new Dictionary<string,string>();
            listArgs = new List<string>();

            if (s == null || s == "")
                return;

            string[] args = s.Split(space);

            string quoteBuffer = "";
            bool fInQuotes = false;

            for (int i = 0; i < args.Length; i++)
            {
                string x = args[i];
                int ich;

                if (fInQuotes)
                    ich = -1;
                else
                    ich = x.IndexOf(':');

                string k, v;

                if (ich >= 0)
                {
                    k = x.Substring(0, ich);
                    v = x.Substring(ich + 1);

                    if (validArgs != null && !validArgs.ContainsKey(k))
                    {
                        listArgs.Add(x);
                    }
                    else
                    {
                        if (hashArgs.ContainsKey(k))
                            hashArgs.Remove(k);

                        hashArgs.Add(k, v);
                    }
                }
                else
                {
                    if (x.Length > 0)
                    {
                        if (fInQuotes)
                        {
                            if (x.EndsWith("\""))
                            {
                                x = quoteBuffer + " " + x.Substring(0, x.Length - 1);
                                fInQuotes = false;
                                quoteBuffer = "";
                                listArgs.Add(x);
                            }
                            else
                            {
                                quoteBuffer += " " + x;
                            }
                        }
                        else
                        {
                            if (x.StartsWith("\""))
                            {
                                if (x.Length >= 2 && x.EndsWith("\""))
                                {
                                    x = x.Substring(1, x.Length - 2);
                                    listArgs.Add(x);
                                    continue;
                                }

                                fInQuotes = true;
                                quoteBuffer = x.Substring(1);
                            }
                            else
                            {
                                listArgs.Add(x);
                            }
                        }
                    }
                }
            }

            if (fInQuotes && quoteBuffer.Length > 0)
                listArgs.Add(quoteBuffer);
        }

        void exec_rpt(string args)
        {
            int cFumble, cFail, cHit, cSpecial, cCrit, cAttempt, cTest;
            int cFumbleMax, cFailMax, cHitMax, cSpecialMax, cCritMax, cAttemptMax, cTestMax;

            cFumble = cFail = cHit = cSpecial = cCrit = cAttempt = cTest = 0;
            cFumbleMax = cFailMax = cHitMax = cSpecialMax = cCritMax = cAttemptMax = cTestMax = 0;

            if (args == null || args.Length == 0)
            {
                dumpCommandHelp();
                return;
            }

            ParseToMap(args);

            if (hashArgs.ContainsKey("test")) cTestMax = eval_roll(hashArgs["test"]);
            if (hashArgs.ContainsKey("tests")) cTestMax = eval_roll(hashArgs["tests"]);
            if (hashArgs.ContainsKey("attempt")) cAttemptMax = eval_roll(hashArgs["attempt"]);
            if (hashArgs.ContainsKey("attempts")) cAttemptMax = eval_roll(hashArgs["attempts"]);
            if (hashArgs.ContainsKey("fumble")) cFumbleMax = eval_roll(hashArgs["fumble"]);
            if (hashArgs.ContainsKey("fumbles")) cFumbleMax = eval_roll(hashArgs["fumbles"]);
            if (hashArgs.ContainsKey("fail")) cFailMax = eval_roll(hashArgs["fail"]);
            if (hashArgs.ContainsKey("fails")) cFailMax = eval_roll(hashArgs["fails"]);
            if (hashArgs.ContainsKey("special")) cSpecialMax = eval_roll(hashArgs["special"]);
            if (hashArgs.ContainsKey("specials")) cSpecialMax = eval_roll(hashArgs["specials"]);
            if (hashArgs.ContainsKey("crit")) cCritMax = eval_roll(hashArgs["crit"]);
            if (hashArgs.ContainsKey("crits")) cCritMax = eval_roll(hashArgs["crits"]);
            if (hashArgs.ContainsKey("success")) cHitMax = eval_roll(hashArgs["success"]);
            if (hashArgs.ContainsKey("successes")) cHitMax = eval_roll(hashArgs["successes"]);
            if (!hashArgs.ContainsKey("pct"))
            {
                sbOut.AppendFormat("Success pct not set!\n");
            }

            if (cTestMax > 0)
            {
                cFumbleMax = 1;
            }

            string pct = hashArgs["pct"] + "%";

            if (cAttemptMax == 0 && cFumbleMax == 0 && cFailMax == 0 && cHitMax == 0 && cSpecialMax == 0 && cCritMax == 0)
            {
                sbOut.AppendFormat("No stopping condition set\n");
                return;
            }

            for (;;)
            {
                if (cTestMax != 0 && cTest >= cTestMax)
                {
                    sbOut.AppendFormat("Total tests reached, success.\n");
                    break;
                }

                if (cAttemptMax != 0 && cAttempt >= cAttemptMax)
                {
                    sbOut.AppendFormat("Total attempts reached.\n");
                    break;
                }

                if (cHitMax != 0 && cHit >= cHitMax)
                {
                    sbOut.AppendFormat("Total successes reached.\n");
                    break;
                }

                if (cFailMax != 0 && cFail >= cFailMax)
                {
                    sbOut.AppendFormat("Total failures reached.\n");
                    break;
                }

                if (cFumbleMax != 0 && cFumble >= cFumbleMax)
                {
                    sbOut.AppendFormat("Total fumbles reached.\n");
                    break;
                }

                if (cSpecialMax != 0 && cSpecial >= cSpecialMax)
                {
                    sbOut.AppendFormat("Total specials reached.\n");
                    break;
                }

                if (cCritMax != 0 && cCrit >= cCritMax)
                {
                    sbOut.AppendFormat("Total criticals reached.\n");
                    break;
                }

                int v = eval_roll(pct);
                if (!fTerse)
                    sbOut.AppendFormat("{0}\n", sb2);

                // crits will count for 3, specials for 2;  first fumble stops anyway
                cTest += v;
                if (v >= 3) cCrit++;
                if (v >= 2) cSpecial++;
                if (v >= 1) cHit++;
                if (v <= 0) cFail++;
                if (v <= -1) cFumble++;
                cAttempt++;
            }

            sbOut.AppendFormat("Results: attempt:{0} success:{1} special:{3} crit:{4} fail:{2} fumble:{5} (any-success:{6} any-fail:{7})\n",
              cAttempt, cHit-cSpecial, cFail-cFumble, cSpecial-cCrit, cCrit, cFumble, cHit, cFail);
        }

        //--------------------------------------------------------------
        // sell command, sell an object or objects
        // apply the appropriate price enhancements as indicated by eval, lore, and bargain skill
        //
        void exec_sell(string args)
        {
            int tot = 0;

            if (args == null || args.Length == 0)
            {
                dumpCommandHelp();
                return;
            }

            ParseToMap(args);

            if (!hashArgs.ContainsKey("values"))
            {
                sbOut.AppendFormat("No values set!\n");
                return;
            }

            if (!hashArgs.ContainsKey("pct"))
            {
                sbOut.AppendFormat("Success pct not set!\n");
                return;
            }

            int pct = eval_roll(hashArgs["pct"]);

            string[] a = hashArgs["values"].Split(comma);

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < a.Length; i++)
            {
                int val = eval_roll(a[i]);

                sb.AppendFormat("{0} * ({1}%)", val, pct);

                int p = pct;

                if (hashArgs.ContainsKey("eval"))
                {
                    int v = eval_roll(hashArgs["eval"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 85 / 100; sb.Append(" * (eval fumble)"); break;
                        case 1: p = p * 105 / 100; sb.Append(" * (eval success)"); break;
                        case 2: p = p * 110 / 100; sb.Append(" * (eval special)"); break;
                        case 3: p = p * 115 / 100; sb.Append(" * (eval crit)"); break;
                    }
                }

                if (hashArgs.ContainsKey("lore"))
                {
                    int v = eval_roll(hashArgs["lore"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 85 / 100; sb.Append(" * (lore fumble)"); break;
                        case 1: p = p * 105 / 100; sb.Append(" * (lore success)"); break;
                        case 2: p = p * 110 / 100; sb.Append(" * (lore special)"); break;
                        case 3: p = p * 115 / 100; sb.Append(" * (lore crit)"); break;
                    }
                }

                if (hashArgs.ContainsKey("bargain"))
                {
                    int v = eval_roll(hashArgs["bargain"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 7 / 10; sb.Append(" * (bargain fumble)"); break;
                        case 1: p = p * 11 / 10; sb.Append(" * (bargain success)"); break;
                        case 2: p = p * 12 / 10; sb.Append(" * (bargain special)"); break;
                        case 3: p = p * 13 / 10; sb.Append(" * (bargain crit)"); break;
                    }
                }

                val = val * p;
                sb.AppendFormat(" = {0}L {1}C\n", val / 100, val % 100);
                tot += val;
            }

            if (!fTerse) sbOut.Append(sb);
            sbOut.AppendFormat("Total value of sale: {0}L {1}C\n", tot / 100, tot % 100);
        }

        //--------------------------------------------------------------
        // buy command, sell an object or objects
        // apply the appropriate price enhancements as indicated by eval, lore, and bargain skill
        //
        void exec_buy(string args)
        {
            int tot = 0;

            if (args == null || args.Length == 0)
            {
                dumpCommandHelp();
                return;
            }

            ParseToMap(args);

            if (!hashArgs.ContainsKey("values"))
            {
                sbOut.AppendFormat("No values set!\n");
                return;
            }

            if (!hashArgs.ContainsKey("pct"))
            {
                sbOut.AppendFormat("Success pct not set!\n");
                return;
            }

            int pct = eval_roll(hashArgs["pct"]);

            string[] a = hashArgs["values"].Split(comma);

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < a.Length; i++)
            {
                int val = eval_roll(a[i]);

                sb.AppendFormat("{0} * ({1}%)", val, pct);

                int p = pct;

                if (hashArgs.ContainsKey("eval"))
                {
                    int v = eval_roll(hashArgs["eval"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 115 / 100; sb.Append(" * (eval fumble)"); break;
                        case 1: p = p * 95 / 100; sb.Append(" * (eval success)"); break;
                        case 2: p = p * 90 / 100; sb.Append(" * (eval special)"); break;
                        case 3: p = p * 85 / 100; sb.Append(" * (eval crit)"); break;
                    }
                }

                if (hashArgs.ContainsKey("lore"))
                {
                    int v = eval_roll(hashArgs["lore"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 115 / 100; sb.Append(" * (lore fumble)"); break;
                        case 1: p = p * 95 / 100; sb.Append(" * (lore success)"); break;
                        case 2: p = p * 90 / 100; sb.Append(" * (lore special)"); break;
                        case 3: p = p * 85 / 100; sb.Append(" * (lore crit)"); break;
                    }
                }

                if (hashArgs.ContainsKey("bargain"))
                {
                    int v = eval_roll(hashArgs["bargain"] + "%");
                    switch (v)
                    {
                        case -1: p = p * 13 / 10; sb.Append(" * (bargain fumble)"); break;
                        case 1: p = p * 9 / 10; sb.Append(" * (bargain success)"); break;
                        case 2: p = p * 8 / 10; sb.Append(" * (bargain special)"); break;
                        case 3: p = p * 7 / 10; sb.Append(" * (bargain crit)"); break;
                    }
                }

                val = val * p;
                sb.AppendFormat(" = {0}L {1}C\n", val / 100, val % 100);
                tot += val;
            }

            if (!fTerse) sbOut.Append(sb);
            sbOut.AppendFormat("Total cost of purchase: {0}L {1}C\n", tot / 100, tot % 100);
        }

        void exec_rcmd(string args)
        {
            int i = args.IndexOf(' ');
            if (i <= 0)
            {
                dumpCommandHelp();
                return;
            }

            string s = args.Substring(i + 1);
            int count = eval_roll(args.Substring(0, i));

            for (i = 0; i < count; i++)
            {
                var cmd = s.Replace("{#}", (i+1).ToString());
                ParseBotCommand(cmd);
                fScanForOwnage = false;
            }
        }

        //--------------------------------------------------------------
        // group cmd ... all those present run the indicated comand
        //
        void exec_gcmd(string args)
        {
            if (args == null || args.Length == 0)
            {
                dumpCommandHelp();
                return;
            }
           
            foreach (string s in getpresent())
            {

                roller = s;
                var cmd = args.Replace("{@}", s);

                try
                {
                    ParseBotCommand(cmd);
                }
                catch (Exception)
                {
                    ParseBotCommand("!echo " + s + ": error executing: " + cmd);
                }

                fScanForOwnage = false;
            }
        }


        public class Summons
        {
            public Summons(int pct, string name, string _int, string pow, string plane)
            {
                Pct = pct;
                Name = name;
                Int = _int;
                Pow = pow;
                Plane = plane;
            }

            public Summons(Summons s)
            {
                Pct = s.Pct;
                Name = s.Name;
                Int = s.Int;
                Pow = s.Pow;
                Plane = s.Plane;
            }

            public int Pct;
            public string Name;
            public string Int;
            public string Pow;
            public string Plane;
        };

        public static Summons[] critters = {
      new Summons (  1, "Bad Man", "20", "35" , "frontier"),
      new Summons (  7, "demon", "3d6", "3d6+6", "frontier" ),
      new Summons ( 17, "disease spirit", "0", "3d6+6", "frontier" ),
      new Summons ( 37, "elemental", "0", "varies", "frontier" ),
      new Summons ( 57, "ghost", "2d6+6", "6d6", "frontier" ),
      new Summons ( 61, "hellion", "4d6", "3d6+6", "frontier" ),
      new Summons ( 66, "healing spirit", "0", "4d6" , "frontier"), 
      new Summons ( 71, "intellect spirit", "1d6", "2d10" , "frontier"),
      new Summons ( 76, "magic spirit", "3d6", "4d6+6", "frontier" ),
      new Summons ( 81, "passion spirit", "0", "3d6+6", "frontier" ),
      new Summons ( 86, "nymph", "varies", "varies" , "frontier"),
      new Summons ( 91, "power spirit", "0", "2d6+3" , "frontier"),
      new Summons ( 96, "spell spirit", "varies", "3d6", "frontier" ),
      new Summons (100, "wraith", "2d6+6", "5d6+6", "frontier" )
    };

        public static Summons[] crittersFrontier = {
      new Summons (  2, "chonchon", "4d6", "3d6+6" , "frontier"),
      new Summons (  8, "disease spirit", "0", "3d6+6" , "frontier"),
      new Summons ( 10, "ghoul spirit", "3d6", "2d6+6", "frontier" ),
      new Summons ( 25, "ghost", "2d6+6", "4d6", "frontier" ),
      new Summons ( 27, "wraith", "2d6+6", "3d6+6", "frontier" ),
      new Summons ( 30, "nymph", "varies", "varies", "frontier" ), 
      new Summons ( 60, "spell spirit", "varies", "3d6", "frontier" ),
      new Summons ( 70, "intellect spirit", "1d6", "2d10", "frontier" ),
      new Summons ( 80, "power spirit", "0", "2d6+3", "frontier" ),
      new Summons ( 85, "discorporate shaman", "1d6+12", "3d6+6", "frontier" ),
      new Summons (100, "outer plane", "", "", "outer" )
    };

        public static Summons[] crittersOuter = {
      new Summons (  3, "chonchon", "5d6", "5d6+6", "outer" ),
      new Summons ( 10, "disease spirit", "0", "5d6+6", "outer" ),
      new Summons ( 12, "elemental", "0", "varies", "outer" ),
      new Summons ( 15, "hellion", "4d6", "3d6+6", "outer" ),
      new Summons ( 25, "ghost", "3d6+6", "6d6", "outer" ),
      new Summons ( 28, "wraith", "3d6+6", "5d6+6", "outer" ),
      new Summons ( 35, "healing spirit", "0", "4d6", "outer" ), 
      new Summons ( 45, "intellect spirit", "1d10", "3d10", "outer" ),
      new Summons ( 55, "magic spirit", "3d6", "3d6+6", "outer" ),
      new Summons ( 60, "power spirit", "0", "3d6+3", "outer" ),
      new Summons ( 70, "spell spirit", "varies", "4d6", "outer" ),
      new Summons ( 80, "passion spirit", "varies", "3d6+6", "outer" ),
      new Summons ( 85, "other spirit or demon", "varies", "varies", "outer" ),
      new Summons ( 90, "discorporate shaman", "1d6+12", "3d6+6", "outer" ),
      new Summons (100, "inner plane", "", "", "inner" )
    };

        public static Summons[] crittersInner = {
      new Summons (  1, "Bad Man", "20", "35", "inner" ),
      new Summons ( 10, "cult spirit", "varies", "varies", "inner" ),
      new Summons ( 25, "elemental", "0", "varies", "inner" ),
      new Summons ( 35, "ghost", "4d6+6", "8d6", "inner" ),
      new Summons ( 40, "hellion", "4d6", "6d6+6", "inner" ),
      new Summons ( 45, "healing spirit", "0", "6d6", "inner" ), 
      new Summons ( 50, "intellect spirit", "2d6", "4d10", "inner" ),
      new Summons ( 60, "magic spirit", "4d6", "5d6+6", "inner" ),
      new Summons ( 65, "power spirit", "0", "4d6+3", "inner" ),
      new Summons ( 70, "spell spirit", "varies", "5d6", "inner" ),
      new Summons ( 80, "passion spirit", "varies", "5d6+6", "inner" ),
      new Summons ( 90, "discorporate shaman", "1d6+12", "3d6+6", "inner" ),
      new Summons (100, "gamemasters choice", "varies", "varies", "inner" )
    };

        public class Spell
        {
            public Spell(int p, string s) { pct = p; name = s; }
            public int pct;
            public string name;
        };

        public static Spell[] spellsRandom = {
      new Spell( 01, "Armoring Enchantment" ),
      new Spell( 04, "Befuddle" ),
      new Spell( 07, "Binding Enchantment" ),
      new Spell( 08, "Bladesharp" ),
      new Spell( 09, "Bludgeon" ),
      new Spell( 15, "Control [Spirit-Type]" ),
      new Spell( 16, "Coordination" ),
      new Spell( 19, "Countermagic" ),
      new Spell( 20, "Darkwall" ),
      new Spell( 23, "Demoralize" ),
      new Spell( 24, "Detect Enemey" ),
      new Spell( 25, "Detect Magic" ),
      new Spell( 28, "Detect [Substance]" ),
      new Spell( 31, "Dispel Magic" ),
      new Spell( 34, "Disruption" ),
      new Spell( 35, "Dullblade" ),
      new Spell( 36, "Endurance" ),
      new Spell( 37, "Extinguish" ),
      new Spell( 38, "Fanaticism" ),
      new Spell( 39, "Farsee" ),
      new Spell( 41, "Firearrow" ),
      new Spell( 41, "Fireblade" ),
      new Spell( 42, "Glamour" ),
      new Spell( 43, "Glue" ),
      new Spell( 44, "Heal" ),
      new Spell( 45, "Ignite" ),
      new Spell( 46, "Ironhand" ),
      new Spell( 47, "Light" ),
      new Spell( 48, "Lightwall" ),
      new Spell( 51, "Magic Point Matrix Enchantment" ),
      new Spell( 54, "Mindspeech" ),
      new Spell( 55, "Mobility" ),
      new Spell( 56, "Multimissle" ),
      new Spell( 57, "Protection" ),
      new Spell( 58, "Repair" ),
      new Spell( 59, "Second Sight" ),
      new Spell( 60, "Shimmer" ),
      new Spell( 61, "Slow" ),
      new Spell( 62, "Speedart" ),
      new Spell( 65, "Spell Matrix Enchantment" ),
      new Spell( 75, "Spirit Screen" ),
      new Spell( 76, "Strength" ),
      new Spell( 77, "Strengthening Enchantment" ),
      new Spell( 83, "Summon [Species]" ),
      new Spell( 84, "Vigor" ),
      new Spell( 90, "Visibility" ),
      new Spell(100, "Other" )
    };

        void exec_summon(string args)
        {
            if (args == null || args.Length == 0)
                goto Usage;

            ParseToMap(args);

            if (!hashArgs.ContainsKey("pct"))
            {
                sbOut.AppendFormat("No pct set!\n");
                return;
            }

            int pct = eval_roll(hashArgs["pct"]);

            string plane = "frontier";
            string type = null;

            if (hashArgs.ContainsKey("type"))
                type = hashArgs["type"];

            string critterType = null;
            string critterName = null;
            string critterFullname = null;
            string critterInt = null;
            string critterPow = null;
            bool fNameGiven = false;
            int ceremony = 0;

            if (hashArgs.ContainsKey("spirit"))
            {
                critterType = hashArgs["spirit"];

                if (critterType == "pow")
                    critterType = "power";

                if (critterType == "int")
                    critterType = "intellect";

                critterName = critterType + " spirit";
                critterFullname = critterName;
            }

            if (hashArgs.ContainsKey("elemental"))
            {
                critterType = hashArgs["elemental"];
                critterName = "elemental";
                critterFullname = critterType + " " + critterName;
            }

            if (hashArgs.ContainsKey("nymph"))
            {
                critterType = hashArgs["nymph"];
                critterName = "nymph";
                critterFullname = critterType + " " + critterName;
            }

            if (hashArgs.ContainsKey("other"))
            {
                critterType = hashArgs["other"];
                critterName = critterType;
                critterFullname = critterType;
            }

            if (hashArgs.ContainsKey("name"))
            {
                fNameGiven = true;
                critterFullname = hashArgs["name"];
            }

            if (hashArgs.ContainsKey("pow"))
            {
                critterPow = hashArgs["pow"];
            }

            if (hashArgs.ContainsKey("int"))
            {
                critterInt = hashArgs["int"];
            }

            if (hashArgs.ContainsKey("ceremony"))
            {
                ceremony = eval_roll(hashArgs["ceremony"]);
            }

            int count = 1;

            if (hashArgs.ContainsKey("count"))
            {
                if (fNameGiven)
                {
                    sbOut.AppendFormat("Summon by name must be resolve individually.\n");
                    return;
                }

                count = eval_roll(hashArgs["count"]);
                if (count <= 0)
                {
                    count = 1;
                }
            }

            Summons[] table = null;

            if (plane == "frontier")
                table = critters;
            else if (plane == "outer")
                table = crittersOuter;
            else if (plane == "inner")
                table = crittersInner;

            Summons summonDesired = null;

            int i = 0;

            for (i = 0; i < table.Length; i++)
            {
                if (table[i].Name == critterName)
                {
                    summonDesired = new Summons(table[i]);
                    break;
                }
            }

            if (summonDesired == null && !fNameGiven && critterName != null)
            {
                sbOut.AppendFormat("Creature '{0}' partly specified but not found\nStandard critters on the {1} plane are:\n", critterName, plane);
                for (i = 0; i < table.Length; i++)
                {
                    sbOut.AppendFormat("  {0}\n", table[i].Name);
                }
                return;
            }

            if (summonDesired == null && fNameGiven)
            {   
                if (critterInt == null || critterPow == null)
                {
                    sbOut.AppendFormat("Unusual summon must include int: and pow:\n");
                    sbOut.AppendFormat("int may be '0' or 'varies', pow may be 'varies'\n");
                    return;
                }

                summonDesired = new Summons(0, critterFullname, critterInt, critterPow, plane);
            }

            if (summonDesired != null)
            {
                sbOut.AppendFormat(
                  type != null ?
                   "Desired critter: {0} type:{3} int:{1} pow:{2}\n" :
                   "Desired critter: {0} int:{1} pow:{2}\n",
                      critterFullname, summonDesired.Int, summonDesired.Pow, type);
            }
            else
            {
                sbOut.AppendFormat("Critter not fully specified or unknown.\n");
                return;
            }

            Summons summon = null;

            for (int cCount = 1; cCount <= count; cCount++)
            {
                if (count > 1)
                {
                    sbOut.AppendFormat("\nSummon Result #{0}\n\n", cCount);
                }

                int pctnet = pct;

                if (ceremony > 0)
                {
                    if (eval_roll(ceremony.ToString() + "%") > 0)
                    {
                        sbOut.AppendFormat("Ceremony succeeds ");

                        if (ceremony > pct)
                        {
                            sbOut.AppendFormat("adding {0}%\n", pct);
                            pctnet += pct;
                        }
                        else
                        {
                            sbOut.AppendFormat("adding {0}%\n", ceremony);
                            pctnet += ceremony;
                        }
                    }
                }

                string strPct = pctnet.ToString() + "%";

                int v = eval_roll(strPct);
                sbOut.AppendFormat("-------\n{2}: {0}=> {1}\n", sb1, sb2, roller);


                if (type == null)
                {
                    switch (v)
                    {
                        // summon <something>
                        // hit I get my something -- summon table
                        // miss I get a random something -- frontier encounter
                        // special I get the thing I named, upgrade to outer
                        // critical I get the thing I named, upgrade to inner
                        // fumble I get a random summon encounter table

                        case -1:
                            sbOut.Append("Fumble: Summoning a random critter from the summon table\n");
                            summon = random_summon("basic");
                            break;

                        case 0:
                            sbOut.Append("Miss: Summoning a random critter from the frontier encounter table\n");
                            summon = random_summon("frontier");
                            break;

                        case 1:
                            sbOut.Append("Hit: desired creature summoned\n");
                            summon = summonDesired;
                            break;

                        case 2:
                            sbOut.Append("Special: desired creature summoned -- upgraded to outer quality\n");
                            summon = upgrade_summon(summonDesired, crittersOuter);
                            break;

                        case 3:
                            sbOut.Append("Critical: desired creature summoned -- upgraded to inner quality\n");
                            summon = upgrade_summon(summonDesired, crittersInner);
                            break;
                    }
                }
                else
                {
                    switch (v)
                    {
                        // specify a spirit
                        // hit I get my something -- summon table
                        // if you miss you'll get summon outer region sec outer
                        // if you fumble you'll roll summon inner with inner secondary encounter

                        case -1:
                            plane = "inner";
                            type = null;
                            sbOut.Append("Fumble: Summoning a random critter from the summon table -- upgraded to inner\n");
                            summon = random_summon("basic");
                            summon = upgrade_summon(summon, crittersInner);
                            break;

                        case 0:
                            plane = "outer";
                            type = null;
                            sbOut.Append("Miss: Summoning a random critter from the summon table -- upgraded to outer\n");
                            summon = random_summon("basic");
                            summon = upgrade_summon(summon, crittersOuter);
                            break;

                        case 1:
                            sbOut.Append("Hit: desired creature summoned\n");
                            break;

                        case 2:
                            plane = "outer";
                            sbOut.Append("Special: desired creature summoned -- upgraded to outer quality\n");
                            summon = upgrade_summon(summon, crittersOuter);
                            break;

                        case 3:
                            plane = "inner";
                            sbOut.Append("Critical: desired creature summoned -- upgraded to inner quality\n");
                            summon = upgrade_summon(summon, crittersInner);
                            break;
                    }
                }

                resolve_summon(summon, type);

                // The secondary effect is disabled...
                //
                // sbOut.AppendFormat("-------\nSecondary effect on the {0} plane\n", plane);
                // summon = random_summon(plane);
                // if (summon != null)
                //     resolve_summon(summon, null);
            }

            return;

        Usage:
            dumpCommandHelp();
        }

        Summons random_summon(string plane)
        {
            for (; ; )
            {
                Summons[] table = null;

                if (plane == "basic")
                    table = critters;

                if (plane == "frontier")
                    table = crittersFrontier;

                if (plane == "outer")
                    table = crittersOuter;

                if (plane == "inner")
                    table = crittersInner;

                int rnd = 1 + r.Next() % 100;
                sbOut.AppendFormat("Rolling on the {0} table: rolled {1}\n", plane, rnd);

                int i;

                for (i = 0; i < table.Length; i++)
                {
                    if (table[i].Pct >= rnd)
                        break;
                }

                if (i == table.Length)
                    throw new Exception("Invalid table entry");

                Summons s = table[i];

                if (plane != "basic" && s.Plane != plane)
                {
                    sbOut.AppendFormat("Going to the {0} plane\n", s.Plane);
                    plane = s.Plane;
                    continue;
                }

                sbOut.AppendFormat("Summoned {0} int:{1} pow:{2}\n", s.Name, s.Int, s.Pow);
                return s;
            }
        }

        void resolve_summon(Summons summon, string type)
        {
            int v;
            sbOut.AppendFormat(">>NAME: {0}\n", summon.Name);

            string strInt = summon.Int;

            if (type != null)
                sbOut.AppendFormat(">>TYPE: {0}\n", type);

            if (summon.Name == "spell spirit" || summon.Name == "magic spirit")
            {
                if (type == null)
                    resolve_spell();
                if (strInt == "varies")
                {
                    if (summon.Plane == "frontier")
                        strInt = "1d4";

                    if (summon.Plane == "outer")
                        strInt = "1d4+2";

                    if (summon.Plane == "inner")
                        strInt = "1d4+4";
                }
            }

            if (strInt == "varies")
            {
                sbOut.AppendFormat(">> INT:  VARIES\n");
            }
            else if (strInt == "0")
            {
                sbOut.AppendFormat(">> INT:  NONE\n");
            }
            else
            {
                v = eval_roll(strInt);
                sbOut.AppendFormat(">> INT:  {0}=> {1}\n", sb1, sb2);
            }

            if (summon.Pow == "varies")
            {
                sbOut.AppendFormat(">> POW:  VARIES\n");
            }
            else if (summon.Pow == "0")
            {
                sbOut.AppendFormat(">> POW:  NONE\n");
            }
            else
            {
                v = eval_roll(summon.Pow);
                sbOut.AppendFormat(">> POW:  {0}=> {1}\n", sb1, sb2);
            }
        }

        void resolve_spell()
        {
            int rnd = r.Next() % 100 + 1;

            for (int i = 0; i < spellsRandom.Length; i++)
            {
                if (spellsRandom[i].pct >= rnd)
                {
                    sbOut.AppendFormat(">>TYPE: ({0}) {1}\n", rnd, spellsRandom[i].name);
                    return;
                }
            }

            sbOut.AppendFormat(">>TYPE: ({0}) Spell Error\n", rnd);
        }

        Summons upgrade_summon(Summons summon, Summons[] table)
        {
            for (int i = 0; i < table.Length; i++)
            {
                Summons s = table[i];
                if (s.Name == summon.Name)
                {
                    sbOut.AppendFormat("Upgraded {0} int:{1} pow:{2}\n", s.Name, s.Int, s.Pow);
                    return new Summons(s);
                }
            }

            sbOut.Append("Upgraded statistics not found -- you'll want to add a few d6\n");
            return summon;
        }

        class ScArgs
        {
            public string suffix;

            public string name;
            public string apct;
            public string dpct;
            public string soi;
            public ScSoi scsoi;
            public int pow;
            public int screen;
            public int resist;
            public int block;
            public int shield;

            public int vAtkResult;
            public int vDefResult;
            public string incomingDamage;
            public int netDamage;
            
            public ScArgs(string suffix)
            {
                this.suffix = suffix;
            }

            public void Reset()
            {
                name = "";
                apct = "";
                dpct = "";
                soi = "";
                screen = 0;
                resist = 0;
                block = 0;
                shield = 0;
            }

            public bool Validate(StringBuilder sbOut)
            {
                if (name == null || name == "")
                {
                    sbOut.AppendFormat("No name{0} set!\n", suffix);
                    return false;
                }

                if ((apct == null || !apct.Contains("%"))
                   && (dpct == null || !dpct.Contains("%")))
                {
                    sbOut.AppendFormat("No pct{0} set!\n", suffix);
                    return false;
                }

                if (apct == null || !apct.Contains("%"))
                {
                    sbOut.AppendFormat("No apct{0} set!\n", suffix);
                    return false;
                }

                if (dpct == null || !dpct.Contains("%"))
                {
                    sbOut.AppendFormat("No dpct{0} set!\n", suffix);
                    return false;
                }

                if (pow == 0)
                {
                    sbOut.AppendFormat("No pow{0} set!\n", suffix);
                    return false;
                }

                if (soi == null || soi == "")
                {
                    sbOut.AppendFormat("No soi{0} set!\n", suffix);
                    return false;
                }

                return true;
            }

            public void ParseArgs(Dictionary<string, string> hashArgs, Func<string,int> eval_roll)
            {
                string name = "name" + suffix;
                string pct = "pct" + suffix;
                string apct = "apct" + suffix;
                string dpct = "dpct" + suffix;
                string soi = "soi" + suffix;
                string pow = "pow" + suffix;
                string screen = "screen" + suffix;
                string resist = "resist" + suffix;
                string block = "block" + suffix;
                string shield = "shield" + suffix;

                if (hashArgs.ContainsKey(name))
                {
                    this.Reset();
                    this.name = hashArgs[name];
                }

                if (hashArgs.ContainsKey(pct))
                    this.apct = this.dpct = hashArgs[pct] + "%";

                if (hashArgs.ContainsKey(apct))
                    this.apct = hashArgs[apct] + "%";

                if (hashArgs.ContainsKey(dpct))
                    this.dpct = hashArgs[dpct] + "%";

                
                if (hashArgs.ContainsKey(soi))
                {
                    this.soi = hashArgs[soi];
                    this.scsoi = parse_sc_soi(this.soi);
                }

                if (hashArgs.ContainsKey(pow))
                    this.pow = eval_roll(hashArgs[pow]);

                if (hashArgs.ContainsKey(screen))
                    this.screen = eval_roll(hashArgs[screen]);

                if (hashArgs.ContainsKey(resist))
                    this.resist = eval_roll(hashArgs[resist]);

                if (hashArgs.ContainsKey(block))
                    this.block = eval_roll(hashArgs[block]);

                if (hashArgs.ContainsKey(shield))
                    this.shield = eval_roll(hashArgs[shield]);

            }

            public string FormatDesc()
            {
                return String.Format("{0}: {1}/{2} @{3} pow using {4}", name, apct, dpct, pow, soi);
            }

            public void AppendBuffs(StringBuilder sbOut)
            {
                if (screen > 0)
                    sbOut.AppendFormat("{0} {1} point screen adds {2}% to defense\n", name, screen, screen * 10);

                if (block > 0)
                    sbOut.AppendFormat("{0} {1} point block adds {2}% to defense\n", name, block, block * 50);

                if (resist> 0)
                    sbOut.AppendFormat("{0} {1} point resist will be used in defense\n", name, resist);

                if (shield > 0)
                    sbOut.AppendFormat("{0} {1} point shield will reduce damage taken by {1}\n", name, shield);
            }

            internal string NameAndPow()
            {
                return String.Format("{0} POW:{1}", name, pow);
            }
        }

        class ScData
        {
            public ScArgs player1;
            public ScArgs player2;
        }

        static Dictionary<string, ScData> scDict = new Dictionary<string, ScData>();

        void exec_spirit_combat(string args)
        {
            if (args == null || args.Length == 0)
                goto Usage;

            ScData scData;

            lock (scDict)
            {
                if (scDict.ContainsKey(roller))
                {
                    scData = scDict[roller];
                }
                else
                {
                    scData = new ScData();
                    scData.player1 = new ScArgs("1");
                    scData.player2 = new ScArgs("2");
                    scDict[roller] = scData;
                }
            }

            ParseToMap(args);

            ScArgs player1 = scData.player1;
            ScArgs player2 = scData.player2;

            if (listArgs.Count >= 1 && listArgs[0].StartsWith("@"))
            {
                ParseSpiritCombatant(player1, listArgs[0].Substring(1));
            }

            Func<string,int> eval = (string s) => eval_roll(s);

            player1.ParseArgs(hashArgs, eval);
            player2.ParseArgs(hashArgs, eval);

            int rounds = 0;
            if (hashArgs.ContainsKey("rounds"))
            {
                rounds = eval_roll(hashArgs["rounds"]);
            }

            if (rounds == 0)
                return;

            if (!player1.Validate(sbOut))
                return;

            if (!player2.Validate(sbOut))
                return;

            sbOut.AppendFormat("{0} rounds of spirit combat {1} vs. {2}\n",
                  rounds, player1.FormatDesc(), player2.FormatDesc());

            player1.AppendBuffs(sbOut);
            player2.AppendBuffs(sbOut);

            for (int i = 0; i < rounds; i++)
            {
                StringBuilder sbT = null;

                sbOut.AppendFormat("----- round {0} -----\n", i + 1);
                sbOut.AppendFormat("BEFORE: {0}, {1}\n", player1.NameAndPow(), player2.NameAndPow());

                if (fTerse)
                {
                    sbT = sbOut;
                    sbOut = new StringBuilder();
                }

                sbOut.AppendFormat("Initial Combat Rolls and Effects\n");

                pre_resolve_sc(player1, player2);
                pre_resolve_sc(player2, player1);

                compute_damage_sc(player1, player2);
                compute_damage_sc(player2, player1);

                sbOut.Append("Damage Resolution\n");

                resolve_damage(player1);
                resolve_damage(player2);

                apply_damage(player1);
                apply_damage(player2);

                if (fTerse)
                {
                    sbOut = sbT;
                }

                sbOut.AppendFormat("AFTER: {0}, {1}\n", player1.NameAndPow(), player2.NameAndPow());

                if (player1.pow == 0 || player2.pow == 0)
                {
                    sbOut.Append("Combat Ends.\n");
                    break;
                }
            }

            return;

        Usage:
            dumpCommandHelp();
        }


        void exec_spirit_combat_one_attack(string args)
        {
            if (args == null || args.Length == 0)
                goto Usage;

            ScData scData;

            lock (scDict)
            {
                if (scDict.ContainsKey(roller))
                {
                    scData = scDict[roller];
                }
                else
                {
                    scData = new ScData();
                    scData.player1 = new ScArgs("1");
                    scData.player2 = new ScArgs("2");
                    scDict[roller] = scData;
                }
            }

            ParseToMap(args);

            ScArgs player1 = scData.player1;
            ScArgs player2 = scData.player2;
           

            if (listArgs.Count >= 1 && listArgs[0].StartsWith("@"))
            {
                ParseSpiritCombatant(player1, listArgs[0].Substring(1));
            }

            if (String.IsNullOrEmpty(player1.name)) player1.name = "attacker";
            if (String.IsNullOrEmpty(player2.name)) player2.name = "defender";
            if (String.IsNullOrEmpty(player1.soi)) player1.soi = "a";
            if (String.IsNullOrEmpty(player2.soi)) player2.soi = "d";

            if (player2.soi == "ad") player2.soi = "d";           
            if (player2.soi == "aa") player2.soi = "x";
            if (player2.soi == "a") player2.soi = "x";

            player1.scsoi = parse_sc_soi(player1.soi);
            player2.scsoi = parse_sc_soi(player2.soi);

            Func<string, int> eval = (string s) => eval_roll(s);

            player1.ParseArgs(hashArgs, eval);
            player2.ParseArgs(hashArgs, eval);

            if (!player1.Validate(sbOut))
                return;

            if (!player2.Validate(sbOut))
                return;

            sbOut.AppendFormat("Spirit combat {0} attacks {1}\n", player1.FormatDesc(), player2.FormatDesc());

            player1.AppendBuffs(sbOut);
            player2.AppendBuffs(sbOut);


            sbOut.AppendFormat("-----\n");
            sbOut.AppendFormat("BEFORE: {0}, {1}\n", player1.NameAndPow(), player2.NameAndPow());

            sbOut.AppendFormat("Initial Combat Rolls and Effects\n");

            pre_resolve_sc(player1, player2);

            bool dd_crit = player2.scsoi == ScSoi.alldefend && player2.vDefResult == 3;

            if (dd_crit)
            {
                pre_resolve_sc(player2, player1);
            }

            compute_damage_sc(player1, player2);      

            if (dd_crit)
            {
                // critical defense causes attack
                compute_damage_sc(player2, player1);
            }

            sbOut.Append("Damage Resolution\n");

            if (dd_crit)
            {
                resolve_damage(player1);
            }

            resolve_damage(player2);

            if (dd_crit)
            {
                apply_damage(player1);
            }

            apply_damage(player2);


            sbOut.AppendFormat("AFTER: {0}, {1}\n", player1.NameAndPow(), player2.NameAndPow());
            sbOut.AppendFormat("-----\n");

            return;

        Usage:
            dumpCommandHelp();
        }

        private void ParseSpiritCombatant(ScArgs player1, string who)
        {
            player1.Reset();

            if (who.Contains("/"))
            {
                ParseSpiritCombatantFromInventory(player1, who);
                return;
            }

            who = FindWho(who, fSilent: false);
            if (who == null)
                return;

            string pct = readkey(who + "/magic", "spirit_combat");

            if (pct != null)
                player1.apct = player1.dpct = pct + "%";

            string usage = readkey("_mana", who + "|mana|personal");
            if (usage == null)
            {
                string pow = readkey(who, "POW");
                if (pow != null)
                {
                    Int32.TryParse(pow, out player1.pow);
                }
            }
            else
            {
                SetPowFromUsage(player1, usage);
            }

            player1.soi = "ad";
            player1.scsoi = ScSoi.attackdefend;
            player1.name = who;

            if (player1.pow < 0)
                player1.pow = 0;

            sbOut.AppendFormat("Combatant1 loaded: {0}\n", player1.FormatDesc());
        }

        private void ParseSpiritCombatantFromInventory(ScArgs player1, string who)
        {
            string toon;
            string spirit;
            GameHost.Worker.Parse2Ex(who, out toon, out spirit, "/");

            who = FindWho(toon, fSilent: false);
            if (who == null)
                return;

            var info = readkey(who + "/_spirits", spirit);
            if (info == null)
            {
                sbOut.AppendFormat("Spirit {0}/{1} not found\n", who, spirit);
                return;
            }

            string sc;
            string pow;
            string stored;
            GameHost.Dict.ExtractSpiritInfoParts(info, out sc, out pow, out stored);

            string usage = readkey("_spiritmana", who + "|" + spirit);
            if (usage == null)
            {
                Int32.TryParse(pow, out player1.pow);
            }
            else
            {
                SetPowFromUsage(player1, usage);
            }

            player1.apct = player1.dpct = sc + "%";
            player1.soi = "ad";
            player1.scsoi = ScSoi.attackdefend;
            player1.name = who + "/" + spirit;

            if (player1.pow < 0)
                player1.pow = 0;

            sbOut.AppendFormat("Combatant1 loaded: {0}\n", player1.FormatDesc());
        }

        private static void SetPowFromUsage(ScArgs player1, string usage)
        {
            string used;
            string max;
            GameHost.Worker.Parse2(usage, out used, out max);

            if (used.StartsWith("used:") && max.StartsWith("max:"))
            {
                used = used.Substring(5);
                max = max.Substring(4);

                int nUsed, nMax;

                if (Int32.TryParse(used, out nUsed) &&
                    Int32.TryParse(max, out nMax))
                {
                    player1.pow = nMax - nUsed;
                }
            }
        }
       
        enum ScSoi
        {
            none, attack, defend, attackdefend, allattack, alldefend
        };

        static ScSoi parse_sc_soi(string sc_soi)
        {
            if (sc_soi == "x")
                return ScSoi.none;
            if (sc_soi == "a")
                return ScSoi.attack;
            if (sc_soi == "d")
                return ScSoi.defend;
            if (sc_soi == "aa")
                return ScSoi.allattack;
            if (sc_soi == "ad")
                return ScSoi.attackdefend;
            if (sc_soi == "dd")
                return ScSoi.alldefend;

            throw new ParseException("Invalid spirit combat soi " + sc_soi);
        }

        static String[] vname = { "fumble", "miss", "hit", "special", "critical" };

        void pre_resolve_sc(ScArgs atk, ScArgs def)
        {
            string defAdjusted = def.dpct;
            ScSoi atkScsoi = atk.scsoi;
            ScSoi defScsoi = def.scsoi;

            // figure out if the attacking percentage is greater than 100
            int pDefPenalty = 0;
            if (Int32.TryParse(atk.apct.Substring(0, atk.apct.Length-1), out pDefPenalty))
            {
                pDefPenalty -= 100;
                if (pDefPenalty < 0) pDefPenalty = 0;
            }

            // adjust SOI for buffs
            if (!IsDefending(def.scsoi) && def.screen > 0)
            {
                defAdjusted = "0%";
                defScsoi = ScSoi.defend;
                sbOut.AppendFormat(" {0} No defense ordered but spirit screen gets free defense action.\n", def.name);
            }

            // adjust pct for buffs
            defAdjusted = (pDefPenalty > 0 ? (-pDefPenalty).ToString() + "+" : "") + (def.block > 0 ? "50*" + def.block + "+" : "") + (def.screen > 0 ? "10*" + def.screen + "+" : "") + defAdjusted;

            atk.vAtkResult = 0;
            if (!IsAttacking(atkScsoi))
            {
                sbOut.AppendFormat(" {0} No attack ordered.\n", atk.name);
            }
            else
            {
                atk.vAtkResult = eval_roll(atk.apct);
                sbOut.AppendFormat(" {0} attack: {1}=> {2}\n", atk.name, sb1, sb2);
            }

            def.vDefResult = 0;
            if (!IsDefending(defScsoi))
            {
                sbOut.AppendFormat(" {0} No defense ordered.\n", def.name);
            }
            else
            {
                def.vDefResult = eval_roll(defAdjusted);
                sbOut.AppendFormat(" {0} defense: {1}=> {2}\n", def.name, sb1, sb2);
            }

            if (def.vDefResult == 3 && defScsoi == ScSoi.alldefend)
            {
                sbOut.Append(" All Out Defense Critical: No Attack succeeds\n");
                sbOut.Append(" Defender does normal damage to attacker.\n");
                sbOut.Append(" This damage can be affected by a Defend option.\n");
            }
        }

        static bool IsAttacking(ScSoi scsoi)
        {
            switch (scsoi)
            {
                case ScSoi.none:
                case ScSoi.defend:
                case ScSoi.alldefend:
                    return false;

            }
            return true;
        }

        static bool IsDefending(ScSoi scsoi)
        {
            switch (scsoi)
            {
                case ScSoi.none:
                case ScSoi.attack:
                case ScSoi.allattack:
                    return false;
            }
            return true;
        }

        void compute_damage_sc(ScArgs atk, ScArgs def)
        {
            ScSoi atkScsoi = atk.scsoi;
            ScSoi defScsoi = def.scsoi;

            sbOut.AppendFormat("Consequences of {0} attacks {1}\n", atk.name, def.name);

            if (atk.vAtkResult == 0 && atkScsoi == ScSoi.allattack)
            {
                // downgrade to normal attack
                atkScsoi = ScSoi.attack;

                sbOut.AppendFormat(" All out attack missed; gets a 2nd chance for a basic hit\n");
                atk.vAtkResult = eval_roll(atk.apct);
                sbOut.AppendFormat(" Attack 2nd chance: {0}=> {1}\n", sb1, sb2);
                switch (atk.vAtkResult)
                {
                    case -1:
                        sbOut.Append(" 2nd chance fumble converted to basic miss\n");
                        atk.vAtkResult = 0;
                        break;
                    case 0:
                        sbOut.Append(" 2nd chance miss does no damage\n");
                        break;
                    case 1:
                        sbOut.Append(" 2nd chance hit does basic damage\n");
                        break;
                    case 2:
                        sbOut.Append(" 2nd chance special converted to basic hit, normal damage\n");
                        atk.vAtkResult = 1;
                        break;
                    case 3:
                        sbOut.Append(" 2nd chance critical converted to basic hit, normal damage\n");
                        atk.vAtkResult = 1;
                        break;
                }
            }

            // adjust soi for spirit screen and no defense, you get a base defense
            if (!IsDefending(def.scsoi) && def.screen > 0)
            {
                defScsoi = ScSoi.defend;
            }

            // adjust for critical all out defense
            if (atkScsoi == ScSoi.alldefend && atk.vDefResult == 3)
            {
                sbOut.Append(" Critical All Out Defense Causes Attack!\n");
                atk.vAtkResult = 1;
                atkScsoi = ScSoi.attack;
            }

            bool fHalf = false;

            string dmg = "";

            if (defScsoi == ScSoi.alldefend)
            {
                switch (def.vDefResult)
                {
                    case -1:
                        sbOut.Append(" All Out Defense Fumble: no effect\n");
                        break;

                    case 0:
                        sbOut.Append(" All Out Defense Failure: Attack does one-half the normal damage (roll and divide, round up)\n");
                        fHalf = true;
                        break;

                    case 1:
                        sbOut.Append(" All Out Defense Success: Attack is one level of success lower than normal.\n");
                        if (atk.vAtkResult >= 1)
                        {
                            sbOut.AppendFormat(" Attacker {0} reduced to {1}\n", vname[atk.vAtkResult + 1], vname[atk.vAtkResult]);
                            atk.vAtkResult--;
                        }
                        break;

                    case 2:
                        sbOut.Append(" All Out Defense Special: No Attack succeeds\n");
                        if (atk.vAtkResult >= 1)
                        {
                            sbOut.AppendFormat(" Attacker {0} reduced to miss\n", vname[atk.vAtkResult + 1]);
                            atk.vAtkResult = 0;
                        }
                        break;

                    case 3:
                        sbOut.Append(" All Out Defense Critical: No Attack succeeds\n");
                        if (atk.vAtkResult >= 1)
                        {
                            sbOut.AppendFormat(" Attacker {0} reduced to miss\n", vname[atk.vAtkResult + 1]);
                            atk.vAtkResult = 0;
                        }
                        break;
                }
            }
            else if (defScsoi == ScSoi.defend || defScsoi == ScSoi.attackdefend)
            {
                switch (def.vDefResult)
                {
                    case -1:
                        sbOut.Append(" Defense Fumble: All opponents' attempts succeed without a roll. Treat attacker failure or fumble as a success.\n");
                        if (atk.vAtkResult <= 0)
                        {
                            sbOut.AppendFormat(" Attacker {0} increased to hit\n", vname[atk.vAtkResult + 1]);
                            atk.vAtkResult = 1;
                        }
                        break;

                    case 0:
                        sbOut.Append(" Defense Failure: No effect\n");
                        break;

                    case 1:
                        sbOut.Append(" Defense Success: Attack does one-half the normal damage (round up)\n");
                        fHalf = true;
                        break;

                    case 2:
                        sbOut.Append(" Defense Special: Attack is one level of success lower than normal.\n");
                        if (atk.vAtkResult >= 1)
                        {
                            sbOut.AppendFormat(" Attacker {0} reduced to {1}\n", vname[atk.vAtkResult + 1], vname[atk.vAtkResult]);
                            atk.vAtkResult--;
                        }
                        break;

                    case 3:
                        sbOut.Append(" Defense Critical: No Attack succeeds\n");
                        if (atk.vAtkResult >= 1)
                        {
                            sbOut.AppendFormat(" Attacker {0} reduced to miss\n", vname[atk.vAtkResult + 1]);
                            atk.vAtkResult = 0;
                        }
                        break;
                }
            }
            else
            {
                sbOut.Append(" No defense ordered\n");
            }

            if (atkScsoi == ScSoi.allattack)
            {
                switch (atk.vAtkResult)
                {
                    case -1:
                        sbOut.Append(" All Out Attack Fumble: No damage\n");
                        sbOut.Append(" All opponents' attempts to Appease, Banish, or use Spirit Sense succeed\n");
                        sbOut.Append(" without a roll. The opponent should roll anyway, if he or she has not already done so.\n");
                        sbOut.Append(" However, treat a failure or fumble as a success.\n");
                        break;

                    case 0:
                        sbOut.Append(" All Out Attack Failure: No damage\n");
                        break;

                    case 1:
                        sbOut.Append(" All Out Attack Success: Damage is automatically the maximum\n");
                        dmg = compute_damage(atk.pow);
                        dmg = "max(" + dmg + ")";
                        break;

                    case 2:
                        sbOut.Append(" All Out Attack Special: Roll damage and add it to the maximum damage for the character's MP.\n");
                        dmg = compute_damage(atk.pow);
                        dmg = "max(" + dmg + ")+" + dmg;
                        break;

                    case 3:
                        sbOut.Append(" All Out Attack Critical: Damage is two times the maximum for the character's MP.\n");
                        sbOut.Append(" On a successful MP vs. MP roll attacker may bind or possess\n");
                        sbOut.Append(" (or do whatever it does to defenseless targets) the target.\n");
                        dmg = compute_damage(atk.pow);
                        dmg = "2*max(" + dmg + ")";
                        break;
                }
            }
            else if (atkScsoi == ScSoi.attack || atkScsoi == ScSoi.attackdefend)
            {
                switch (atk.vAtkResult)
                {
                    case -1:
                        sbOut.Append(" Attack Fumble: No damage\n");
                        sbOut.Append(" One opponents' attempts to Appease, Banish, or use Spirit Sense succeed\n");
                        sbOut.Append(" without a roll. The opponent should roll anyway, if he or she has not already done so.\n");
                        sbOut.Append(" However, treat a failure or fumble as a success.\n");
                        break;

                    case 0:
                        sbOut.Append(" Attack Failure: No Damage\n");
                        break;

                    case 1:
                        sbOut.Append(" Attack Success: Normal Damage\n");
                        dmg = compute_damage(atk.pow);
                        break;

                    case 2:
                        sbOut.Append(" Attack Special: Damage is automatically the maximum\n");
                        dmg = compute_damage(atk.pow);
                        dmg = "max(" + dmg + ")";
                        break;

                    case 3:
                        sbOut.Append(" Attack Critical: Roll damage and add it to the maximum damage for the character's MP.\n");
                        dmg = compute_damage(atk.pow);
                        dmg = "max(" + dmg + ")+" + dmg;
                        break;
                }
            }
            else
            {
                sbOut.Append(" No attack ordered\n");
            }

            if (fHalf && dmg.Length > 0)
            {
                dmg = "(" + dmg + ")/2";
            }

            def.incomingDamage = dmg;
        }

        string compute_damage(int pow)
        {
            if (pow <= 10) return "1d3";
            if (pow <= 20) return "1d4";
            if (pow <= 30) return "1d6";
            if (pow <= 40) return "1d8";
            if (pow <= 50) return "1d10";
            if (pow <= 60) return "2d6";
            if (pow <= 70) return "2d6+2";
            if (pow <= 80) return "3d6";
            if (pow <= 90) return "3d6+2";

            int dice = 2 + (pow - 60) / 20;
            int bonus = 2 * ((pow / 10) % 2);

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}d6{1}", dice, bonus > 0 ? "+2" : "");
            return sb.ToString();
        }

        void append_damage(StringBuilder sb, string dmg)
        {
            if (sb.Length > 0)
            {
                sb.Append("+");
            }
            sb.Append(dmg);
        }

        void resolve_damage(ScArgs def)
        {
            string dmgRoll = def.incomingDamage;

            int dmg = 0;
            if (dmgRoll.Length == 0)
            {
                sbOut.AppendFormat(" {0} takes no damage\n", def.name);
            }
            else
            {
                dmg = eval_roll(dmgRoll);
                sbOut.AppendFormat(" {2} takes: {0}=> {1}\n", sb1, sb2, def.name);
            }

            if (dmg > 0 && def.resist > 0)
            {
                int r = eval_roll(def.resist.ToString() + "@");
                if (r >= dmg)
                {
                    sbOut.AppendFormat(" {2} resists damage: {0}=> {1} RESISTED!\n", sb1, sb2, def.name);
                    sbOut.AppendFormat(" {0} damage reduced to zero\n", def.name);
                    dmg = 0;
                }
                else
                {
                    sbOut.AppendFormat(" {2} resists damage: {0}=> {1} FAILS!\n", sb1, sb2, def.name);
                }
            }

            if (dmg > 0 && def.block > 0)
            {
                dmg -= def.block;
                if (dmg < 0) dmg = 0;

                sbOut.AppendFormat(" {0} spirit block {1} reduces damage to {2}\n", def.name, def.block, dmg);
            }

            if (dmg > 0 && def.shield > 0)
            {
                dmg -= def.shield;
                if (dmg < 0) dmg = 0;

                sbOut.AppendFormat(" {0} spirit shield {1} reduces damage to {2}\n", def.name, def.shield, dmg);
            }

            def.netDamage = dmg;
        }

        void apply_damage(ScArgs def)
        {
            def.pow -= def.netDamage;
            if (def.pow < 0) def.pow = 0;
        }

        public class RollTableEntry
        {
            public string key;
            public string name;
            public string roll;
            public string[] values;

            public RollTableEntry(string k, string n, string r, string[] v)
            {
                key = k;
                name = n;
                roll = r;
                values = v;
            }
        }

        static RollTableEntry[] tables = {
            new RollTableEntry(
                "bday",
                "Birthday",
                "1d294",
                new string[] {
                "1 Sea Season 1, Disorder Week, Freezeday",
                "2 Sea Season 2, Disorder Week, Watersday",
                "3 Sea Season 3, Disorder Week, Claysday",
                "4 Sea Season 4, Disorder Week, Windsday",
                "5 Sea Season 5, Disorder Week, Firesday",
                "6 Sea Season 6, Disorder Week, Wildsday",
                "7 Sea Season 7, Disorder Week, Godsday",
                "8 Sea Season 8, Harmony Week, Freezeday",
                "9 Sea Season 9, Harmony Week, Watersday",
                "10 Sea Season 10, Harmony Week, Claysday",
                "11 Sea Season 11, Harmony Week, Windsday",
                "12 Sea Season 12, Harmony Week, Firesday",
                "13 Sea Season 13, Harmony Week, Wildsday",
                "14 Sea Season 14, Harmony Week, Godsday",
                "15 Sea Season 15, Death Week, Freezeday",
                "16 Sea Season 16, Death Week, Watersday",
                "17 Sea Season 17, Death Week, Claysday",
                "18 Sea Season 18, Death Week, Windsday",
                "19 Sea Season 19, Death Week, Firesday",
                "20 Sea Season 20, Death Week, Wildsday",
                "21 Sea Season 21, Death Week, Godsday",
                "22 Sea Season 22, Fertility Week, Freezeday",
                "23 Sea Season 23, Fertility Week, Watersday",
                "24 Sea Season 24, Fertility Week, Claysday",
                "25 Sea Season 25, Fertility Week, Windsday",
                "26 Sea Season 26, Fertility Week, Firesday",
                "27 Sea Season 27, Fertility Week, Wildsday",
                "28 Sea Season 28, Fertility Week, Godsday",
                "29 Sea Season 29, Statis Week, Freezeday",
                "30 Sea Season 30, Statis Week, Watersday",
                "31 Sea Season 31, Statis Week, Claysday",
                "32 Sea Season 32, Statis Week, Windsday",
                "33 Sea Season 33, Statis Week, Firesday",
                "34 Sea Season 34, Statis Week, Wildsday",
                "35 Sea Season 35, Statis Week, Godsday",
                "36 Sea Season 36, Movement Week, Freezeday",
                "37 Sea Season 37, Movement Week, Watersday",
                "38 Sea Season 38, Movement Week, Claysday",
                "39 Sea Season 39, Movement Week, Windsday",
                "40 Sea Season 40, Movement Week, Firesday",
                "41 Sea Season 41, Movement Week, Wildsday",
                "42 Sea Season 42, Movement Week, Godsday",
                "43 Sea Season 43, Illusion Week, Freezeday",
                "44 Sea Season 44, Illusion Week, Watersday",
                "45 Sea Season 45, Illusion Week, Claysday",
                "46 Sea Season 46, Illusion Week, Windsday",
                "47 Sea Season 47, Illusion Week, Firesday",
                "48 Sea Season 48, Illusion Week, Wildsday",
                "49 Sea Season 49, Illusion Week, Godsday",
                "50 Sea Season 50, Truth Week, Freezeday",
                "51 Sea Season 51, Truth Week, Watersday",
                "52 Sea Season 52, Truth Week, Claysday",
                "53 Sea Season 53, Truth Week, Windsday",
                "54 Sea Season 54, Truth Week, Firesday",
                "55 Sea Season 55, Truth Week, Wildsday",
                "56 Sea Season 56, Truth Week, Godsday",
                "57 Fire Season 1, Disorder Week, Freezeday",
                "58 Fire Season 2, Disorder Week, Watersday",
                "59 Fire Season 3, Disorder Week, Claysday",
                "60 Fire Season 4, Disorder Week, Windsday",
                "61 Fire Season 5, Disorder Week, Firesday",
                "62 Fire Season 6, Disorder Week, Wildsday",
                "63 Fire Season 7, Disorder Week, Godsday",
                "64 Fire Season 8, Harmony Week, Freezeday",
                "65 Fire Season 9, Harmony Week, Watersday",
                "66 Fire Season 10, Harmony Week, Claysday",
                "67 Fire Season 11, Harmony Week, Windsday",
                "68 Fire Season 12, Harmony Week, Firesday",
                "69 Fire Season 13, Harmony Week, Wildsday",
                "70 Fire Season 14, Harmony Week, Godsday",
                "71 Fire Season 15, Death Week, Freezeday",
                "72 Fire Season 16, Death Week, Watersday",
                "73 Fire Season 17, Death Week, Claysday",
                "74 Fire Season 18, Death Week, Windsday",
                "75 Fire Season 19, Death Week, Firesday",
                "76 Fire Season 20, Death Week, Wildsday",
                "77 Fire Season 21, Death Week, Godsday",
                "78 Fire Season 22, Fertility Week, Freezeday",
                "79 Fire Season 23, Fertility Week, Watersday",
                "80 Fire Season 24, Fertility Week, Claysday",
                "81 Fire Season 25, Fertility Week, Windsday",
                "82 Fire Season 26, Fertility Week, Firesday",
                "83 Fire Season 27, Fertility Week, Wildsday",
                "84 Fire Season 28, Fertility Week, Godsday",
                "85 Fire Season 29, Statis Week, Freezeday",
                "86 Fire Season 30, Statis Week, Watersday",
                "87 Fire Season 31, Statis Week, Claysday",
                "88 Fire Season 32, Statis Week, Windsday",
                "89 Fire Season 33, Statis Week, Firesday",
                "90 Fire Season 34, Statis Week, Wildsday",
                "91 Fire Season 35, Statis Week, Godsday",
                "92 Fire Season 36, Movement Week, Freezeday",
                "93 Fire Season 37, Movement Week, Watersday",
                "94 Fire Season 38, Movement Week, Claysday",
                "95 Fire Season 39, Movement Week, Windsday",
                "96 Fire Season 40, Movement Week, Firesday",
                "97 Fire Season 41, Movement Week, Wildsday",
                "98 Fire Season 42, Movement Week, Godsday",
                "99 Fire Season 43, Illusion Week, Freezeday",
                "100 Fire Season 44, Illusion Week, Watersday",
                "101 Fire Season 45, Illusion Week, Claysday",
                "102 Fire Season 46, Illusion Week, Windsday",
                "103 Fire Season 47, Illusion Week, Firesday",
                "104 Fire Season 48, Illusion Week, Wildsday",
                "105 Fire Season 49, Illusion Week, Godsday",
                "106 Fire Season 50, Truth Week, Freezeday",
                "107 Fire Season 51, Truth Week, Watersday",
                "108 Fire Season 52, Truth Week, Claysday",
                "109 Fire Season 53, Truth Week, Windsday",
                "110 Fire Season 54, Truth Week, Firesday",
                "111 Fire Season 55, Truth Week, Wildsday",
                "112 Fire Season 56, Truth Week, Godsday",
                "113 Earth Season 1, Disorder Week, Freezeday",
                "114 Earth Season 2, Disorder Week, Watersday",
                "115 Earth Season 3, Disorder Week, Claysday",
                "116 Earth Season 4, Disorder Week, Windsday",
                "117 Earth Season 5, Disorder Week, Firesday",
                "118 Earth Season 6, Disorder Week, Wildsday",
                "119 Earth Season 7, Disorder Week, Godsday",
                "120 Earth Season 8, Harmony Week, Freezeday",
                "121 Earth Season 9, Harmony Week, Watersday",
                "122 Earth Season 10, Harmony Week, Claysday",
                "123 Earth Season 11, Harmony Week, Windsday",
                "124 Earth Season 12, Harmony Week, Firesday",
                "125 Earth Season 13, Harmony Week, Wildsday",
                "126 Earth Season 14, Harmony Week, Godsday",
                "127 Earth Season 15, Death Week, Freezeday",
                "128 Earth Season 16, Death Week, Watersday",
                "129 Earth Season 17, Death Week, Claysday",
                "130 Earth Season 18, Death Week, Windsday",
                "131 Earth Season 19, Death Week, Firesday",
                "132 Earth Season 20, Death Week, Wildsday",
                "133 Earth Season 21, Death Week, Godsday",
                "134 Earth Season 22, Fertility Week, Freezeday",
                "135 Earth Season 23, Fertility Week, Watersday",
                "136 Earth Season 24, Fertility Week, Claysday",
                "137 Earth Season 25, Fertility Week, Windsday",
                "138 Earth Season 26, Fertility Week, Firesday",
                "139 Earth Season 27, Fertility Week, Wildsday",
                "140 Earth Season 28, Fertility Week, Godsday",
                "141 Earth Season 29, Statis Week, Freezeday",
                "142 Earth Season 30, Statis Week, Watersday",
                "143 Earth Season 31, Statis Week, Claysday",
                "144 Earth Season 32, Statis Week, Windsday",
                "145 Earth Season 33, Statis Week, Firesday",
                "146 Earth Season 34, Statis Week, Wildsday",
                "147 Earth Season 35, Statis Week, Godsday",
                "148 Earth Season 36, Movement Week, Freezeday",
                "149 Earth Season 37, Movement Week, Watersday",
                "150 Earth Season 38, Movement Week, Claysday",
                "151 Earth Season 39, Movement Week, Windsday",
                "152 Earth Season 40, Movement Week, Firesday",
                "153 Earth Season 41, Movement Week, Wildsday",
                "154 Earth Season 42, Movement Week, Godsday",
                "155 Earth Season 43, Illusion Week, Freezeday",
                "156 Earth Season 44, Illusion Week, Watersday",
                "157 Earth Season 45, Illusion Week, Claysday",
                "158 Earth Season 46, Illusion Week, Windsday",
                "159 Earth Season 47, Illusion Week, Firesday",
                "160 Earth Season 48, Illusion Week, Wildsday",
                "161 Earth Season 49, Illusion Week, Godsday",
                "162 Earth Season 50, Truth Week, Freezeday",
                "163 Earth Season 51, Truth Week, Watersday",
                "164 Earth Season 52, Truth Week, Claysday",
                "165 Earth Season 53, Truth Week, Windsday",
                "166 Earth Season 54, Truth Week, Firesday",
                "167 Earth Season 55, Truth Week, Wildsday",
                "168 Earth Season 56, Truth Week, Godsday",
                "169 Dark Season 1, Disorder Week, Freezeday",
                "170 Dark Season 2, Disorder Week, Watersday",
                "171 Dark Season 3, Disorder Week, Claysday",
                "172 Dark Season 4, Disorder Week, Windsday",
                "173 Dark Season 5, Disorder Week, Firesday",
                "174 Dark Season 6, Disorder Week, Wildsday",
                "175 Dark Season 7, Disorder Week, Godsday",
                "176 Dark Season 8, Harmony Week, Freezeday",
                "177 Dark Season 9, Harmony Week, Watersday",
                "178 Dark Season 10, Harmony Week, Claysday",
                "179 Dark Season 11, Harmony Week, Windsday",
                "180 Dark Season 12, Harmony Week, Firesday",
                "181 Dark Season 13, Harmony Week, Wildsday",
                "182 Dark Season 14, Harmony Week, Godsday",
                "183 Dark Season 15, Death Week, Freezeday",
                "184 Dark Season 16, Death Week, Watersday",
                "185 Dark Season 17, Death Week, Claysday",
                "186 Dark Season 18, Death Week, Windsday",
                "187 Dark Season 19, Death Week, Firesday",
                "188 Dark Season 20, Death Week, Wildsday",
                "189 Dark Season 21, Death Week, Godsday",
                "190 Dark Season 22, Fertility Week, Freezeday",
                "191 Dark Season 23, Fertility Week, Watersday",
                "192 Dark Season 24, Fertility Week, Claysday",
                "193 Dark Season 25, Fertility Week, Windsday",
                "194 Dark Season 26, Fertility Week, Firesday",
                "195 Dark Season 27, Fertility Week, Wildsday",
                "196 Dark Season 28, Fertility Week, Godsday",
                "197 Dark Season 29, Statis Week, Freezeday",
                "198 Dark Season 30, Statis Week, Watersday",
                "199 Dark Season 31, Statis Week, Claysday",
                "200 Dark Season 32, Statis Week, Windsday",
                "201 Dark Season 33, Statis Week, Firesday",
                "202 Dark Season 34, Statis Week, Wildsday",
                "203 Dark Season 35, Statis Week, Godsday",
                "204 Dark Season 36, Movement Week, Freezeday",
                "205 Dark Season 37, Movement Week, Watersday",
                "206 Dark Season 38, Movement Week, Claysday",
                "207 Dark Season 39, Movement Week, Windsday",
                "208 Dark Season 40, Movement Week, Firesday",
                "209 Dark Season 41, Movement Week, Wildsday",
                "210 Dark Season 42, Movement Week, Godsday",
                "211 Dark Season 43, Illusion Week, Freezeday",
                "212 Dark Season 44, Illusion Week, Watersday",
                "213 Dark Season 45, Illusion Week, Claysday",
                "214 Dark Season 46, Illusion Week, Windsday",
                "215 Dark Season 47, Illusion Week, Firesday",
                "216 Dark Season 48, Illusion Week, Wildsday",
                "217 Dark Season 49, Illusion Week, Godsday",
                "218 Dark Season 50, Truth Week, Freezeday",
                "219 Dark Season 51, Truth Week, Watersday",
                "220 Dark Season 52, Truth Week, Claysday",
                "221 Dark Season 53, Truth Week, Windsday",
                "222 Dark Season 54, Truth Week, Firesday",
                "223 Dark Season 55, Truth Week, Wildsday",
                "224 Dark Season 56, Truth Week, Godsday",
                "225 Storm Season 1, Disorder Week, Freezeday",
                "226 Storm Season 2, Disorder Week, Watersday",
                "227 Storm Season 3, Disorder Week, Claysday",
                "228 Storm Season 4, Disorder Week, Windsday",
                "229 Storm Season 5, Disorder Week, Firesday",
                "230 Storm Season 6, Disorder Week, Wildsday",
                "231 Storm Season 7, Disorder Week, Godsday",
                "232 Storm Season 8, Harmony Week, Freezeday",
                "233 Storm Season 9, Harmony Week, Watersday",
                "234 Storm Season 10, Harmony Week, Claysday",
                "235 Storm Season 11, Harmony Week, Windsday",
                "236 Storm Season 12, Harmony Week, Firesday",
                "237 Storm Season 13, Harmony Week, Wildsday",
                "238 Storm Season 14, Harmony Week, Godsday",
                "239 Storm Season 15, Death Week, Freezeday",
                "240 Storm Season 16, Death Week, Watersday",
                "241 Storm Season 17, Death Week, Claysday",
                "242 Storm Season 18, Death Week, Windsday",
                "243 Storm Season 19, Death Week, Firesday",
                "244 Storm Season 20, Death Week, Wildsday",
                "245 Storm Season 21, Death Week, Godsday",
                "246 Storm Season 22, Fertility Week, Freezeday",
                "247 Storm Season 23, Fertility Week, Watersday",
                "248 Storm Season 24, Fertility Week, Claysday",
                "249 Storm Season 25, Fertility Week, Windsday",
                "250 Storm Season 26, Fertility Week, Firesday",
                "251 Storm Season 27, Fertility Week, Wildsday",
                "252 Storm Season 28, Fertility Week, Godsday",
                "253 Storm Season 29, Statis Week, Freezeday",
                "254 Storm Season 30, Statis Week, Watersday",
                "255 Storm Season 31, Statis Week, Claysday",
                "256 Storm Season 32, Statis Week, Windsday",
                "257 Storm Season 33, Statis Week, Firesday",
                "258 Storm Season 34, Statis Week, Wildsday",
                "259 Storm Season 35, Statis Week, Godsday",
                "260 Storm Season 36, Movement Week, Freezeday",
                "261 Storm Season 37, Movement Week, Watersday",
                "262 Storm Season 38, Movement Week, Claysday",
                "263 Storm Season 39, Movement Week, Windsday",
                "264 Storm Season 40, Movement Week, Firesday",
                "265 Storm Season 41, Movement Week, Wildsday",
                "266 Storm Season 42, Movement Week, Godsday",
                "267 Storm Season 43, Illusion Week, Freezeday",
                "268 Storm Season 44, Illusion Week, Watersday",
                "269 Storm Season 45, Illusion Week, Claysday",
                "270 Storm Season 46, Illusion Week, Windsday",
                "271 Storm Season 47, Illusion Week, Firesday",
                "272 Storm Season 48, Illusion Week, Wildsday",
                "273 Storm Season 49, Illusion Week, Godsday",
                "274 Storm Season 50, Truth Week, Freezeday",
                "275 Storm Season 51, Truth Week, Watersday",
                "276 Storm Season 52, Truth Week, Claysday",
                "277 Storm Season 53, Truth Week, Windsday",
                "278 Storm Season 54, Truth Week, Firesday",
                "279 Storm Season 55, Truth Week, Wildsday",
                "280 Storm Season 56, Truth Week, Godsday",
                "281 Sacred Time 1, Infinity Week 1, Freezeday",
                "282 Sacred Time 2, Infinity Week 1, Watersday",
                "283 Sacred Time 3, Infinity Week 1, Claysday",
                "284 Sacred Time 4, Infinity Week 1, Windsday",
                "285 Sacred Time 5, Infinity Week 1, Firesday",
                "286 Sacred Time 6, Infinity Week 1, Wildsday",
                "287 Sacred Time 7, Infinity Week 1, Godsday",
                "288 Sacred Time 8, Infinity Week 2, Freezeday",
                "289 Sacred Time 9, Infinity Week 2, Watersday",
                "290 Sacred Time 10, Infinity Week 2, Claysday",
                "291 Sacred Time 11, Infinity Week 2, Windsday",
                "292 Sacred Time 12, Infinity Week 2, Firesday",
                "293 Sacred Time 13, Infinity Week 2, Wildsday",
                "294 Sacred Time 14, Infinity Week 2, Godsday"
              }
            ),
            new RollTableEntry(
                "fumble",
                "Melee or Parry Fumble",
                "1d100",
                new string[] {
                "5 Lose next parry",
                "10 Lose next attack",
                "15 Lose next parry and attack",
                "20 Lose next attack, parry, and dodge",
                "25 Lose next 1d3 attacks",
                "30 Lose next 1d3 attacks and parries",
                "35 Shield strap breaks: shield immeidately falls",
                "40 Shield strap breaks: shield immeidately falls, lose next attack",
                "45 Armor strap breaks: roll hit location for lost armor",
                "50 Armor strap breaks: roll hit location for lost armor, lose next attack and parry",
                "55 Fall: lose parry and Dodge this round, take 1d3 rounds to get up",
                "60 Twist ankle: 1/2 move rate for 5d10 rounds",
                "63 Twist ankle and fall: 1/2 move rate for 5d10 rounds, lose parry/dodge this round, 1d3 rounds to get up",
                "67 Vision Impaired: -25 to atk/parry;fix helment in 1d3 unengaged rounds",
                "70 Vision Impaired: -50 to atk/parry;fix helment in 1d6 unengaged rounds",
                "72 Vision Blocked: lose all atk and parries;fix helment in 1d6 unengaged rounds",
                "74 Distracted: foes attack at +25 for next round",
                "78 Attack/Parry:weapon/shield dropped: recover in 1d2 rounds.  ",
                "82 Attack/Parry:weapon/sheild knocked away: 1d6 meters distance, 1d8 direction, 1d3+1 recovery",
                "86 Weapon/shield shattered: 100% chance if unenchanted, subtract 10% from chance per pt of spirt/sorcery, 20% per pt of divine magic",
                "89 Attack: hit nearest friend for rolled dmg, self if no friends;  Parry: wide open: foe auto-hits for rolled damage",
                "91 Attack: hit nearest friend for MAX rolled dmg, self if no friends;  Parry: wide open: foe auto-hits for rolled damage",
                "92 Attack: hit nearest friend for MAX rolled dmg NO ARMOR, self if no friends;  Parry: wide open: foe auto-hits for rolled damage",
                "95 Attack: hit self: do rolled damage;  Parry: foe auto-hits for rolled damage",
                "98 Attack: hit self: do MAX damage, NO ARMOR! Parry: foe auto-hits critically",
                "99 Blow it: roll twice on this table!",
                "100 Blow it badly: roll three times on this table!"
                }
            ),
            new RollTableEntry(
                "fumble_missile",
                "Missile Fumble Table",
                "1d100",
                new string[] {
                    "10 Lose next attack",
                    "20 Lose next 1d4 attacks",
                    "30 Lose next 1d3 melee rounds for any activity",
                    "40 Weapon strap breaks: lose melee weapon",
                    "50 Armor strap breaks: roll hit location to determine which peice breaks and falls",
                    "60 Armor strap breaks: roll hit location to determine which peice breaks and falls; Lose action and parry next round",
                    "65 Fall to ground",
                    "70 Vision impaired: lose 50% from all attack chances for 1d3 rounds",
                    "73 Vision blocked: can't see for 1d3 rounds",
                    "80 Drop weapon: lands 1d6-1 meters distant",
                    "85 Weapon shatters: 100% chance if unenchanted: -10% per pt of spirit magic sorcery; -20% per pt of divine magic",
                    "89 Hit nearest friend, if no friend then Weapon shatters: 100% chance if unenchanted: -10% per pt of spirit magic sorcery; -20% per pt of divine magic",
                    "92 Hit nearest friend IMPALING DAMAGE, if no friend then Weapon shatters: 100% chance if unenchanted: -10% per pt of spirit magic sorcery; -20% per pt of divine magic",
                    "94 Hit nearest friend CRITICAL DAMAGE, if no friend then Weapon shatters: 100% chance if unenchanted: -10% per pt of spirit magic sorcery; -20% per pt of divine magic",
                    "98 Blow it: roll twice on this table",
                    "100 Blow it badly: roll three times on this table",
                }
            ),
            new RollTableEntry(
                "fumble_natural",
                "Natural Weapon Fumble Table",
                "1d100",
                new string[] {
                    "5 Lose next dodge",
                    "10 Lose next attack",
                    "15 Lose next dodge and parry",
                    "20 Lose next dodge parry and attack",
                    "25 Lose next 1d3 rounds; no action no parry",
                    "30 Lose next 1d6 attacks",
                    "35 Armor or clothing strap breaks: roll hit location",
                    "40 Armor or clothing strap breaks: roll hit location; Lose next 1d3 rounds; no action no parry",
                    "50 Fall: lose dodge and parry this round",
                    "60 Fall and twist ankle: lose 1 meter of movement per SR for 5d10 rounds",
                    "70 Vision impaired: for 1d3 rounds, -25% on attacks/parries/dodges",
                    "73 Vision impaired: for 1d4 rounds, -50% on attacks/parries/dodges",
                    "75 Vision blocked: can't see for 1d3 rounds",
                    "80 Distracted: all foes attack at +25% next round",
                    "85 Miss an attack and strain muscle: lose 1 hit point in attacking limb and 3 fatigue",
                    "90 Hit nearest friend: do regular rolled damaged. If no friend: miss an attack and strain muscle: lose 1 hit point in attacking limb and 3 fatigue",
                    "94 Hit nearest friend: do MAX damaged. If no friend: miss an attack and strain muscle: lose 1 hit point in attacking limb and 3 fatigue",
                    "96 Hit nearest friend: do CRITICAL damaged. If no friend: miss an attack and strain muscle: lose 1 hit point in attacking limb and 3 fatigue",
                    "98 Hit self: do maximum rolled damaged",
                    "99 Blow it: roll twice on this table",
                    "100 Blow it badly: roll three times on this table",
                }
            ),
            new RollTableEntry(
                "human_female",
                "Human Female",
                "Strength:2d6+2, Constitution:3d6+1, Size:2d6+3, Intelligence:2d6+6, Power:3d6, Dexterity:3d6+1, Appearance:3d6+1",
                null
            ),
            new RollTableEntry(
                "human_male",
                "Human Male",
                "Strength:3d6, Constitution:3d6, Size:2d6+6, Intelligence:2d6+6, Power:3d6, Dexterity:3d6,Appearance:3d6",
                null
            ),
            
            new RollTableEntry(
                "loc",
                "Humanoid Melee Hit Location",
                "1d20",
                new string[] {
                    "4 Right Leg",
                    "8 Left Leg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 Right Arm",
                    "18 Left Arm",
                    "20 Head",
                }
            ),

            new RollTableEntry(
                "loc_missile",
                "Humanoid Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),

            new RollTableEntry(
                "loc_human",
                "Humanoid Melee Hit Location",
                "1d20",
                new string[] {
                    "4 Right Leg",
                    "8 Left Leg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 Right Arm",
                    "18 Left Arm",
                    "20 Head",
                }
            ),

            new RollTableEntry(
                "loc_human_missile",
                "Humanoid Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),

            new RollTableEntry(
                "loc_allosaur",
                "Allosaur Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "16 RClaw",
                    "17 LClaw",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_amphisboenae",
                "Amphisboenae Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Head",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_ape",
                "Ape Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "10 Chest",
                    "14 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_baboon",
                "Baboon Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "13 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_behemoth",
                "Behemoth Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "04 RHLeg",
                    "06 LHLeg",
                    "10 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_bird",
                "Bird Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RClaw",
                    "06 LClaw",
                    "10 Body",
                    "13 RWing",
                    "16 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_broo",
                "Broo Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_centaur",
                "Centaur Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "06 HindQ",
                    "08 ForeQ",
                    "10 RFLeg",
                    "12 LFLeg",
                    "14 Chest",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_centipede",
                "Centipede Melee Hit Location",
                "1d20",
                new string[] {
                    "04 R Side Legs",
                    "08 L Side Legs",
                    "18 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_ceratopsian",
                "Ceratopsian Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "04 RHLeg",
                    "06 LHLeg",
                    "08 HindQ",
                    "10 ForeQ",
                    "12 RFLeg",
                    "14 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_chonchon",
                "Chonchon Melee Hit Location",
                "1d20",
                new string[] {
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_clifftoad",
                "CliffToad Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RHLeg",
                    "08 LHLeg",
                    "10 Abdomen",
                    "12 Chest",
                    "14 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_cockatrice",
                "Cockatrice Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RClaw",
                    "08 LClaw",
                    "12 Body",
                    "15 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_crab",
                "Crab Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "08 Thorax",
                    "09 RCLeg",
                    "10 LCLeg",
                    "11 RFLeg",
                    "12 LFLeg",
                    "14 RFClaw",
                    "16 LFClaw",
                    "20 Forebody",
                }
            ),
            new RollTableEntry(
                "loc_crocodile",
                "Crocodile Melee Hit Location",
                "1d20",
                new string[] {
                    "03 Tail",
                    "04 RHLeg",
                    "05 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "15 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_demibird",
                "DemiBird Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "10 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_doc",
                "Doc Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "10 Missed",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "19 Missed",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragon",
                "Dragon Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "04 RHLeg",
                    "06 LHLeg",
                    "08 HindQ",
                    "10 ForeQ",
                    "12 RWing",
                    "14 LWing",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonnewt",
                "Dragonnewt Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "13 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonsnail1",
                "Dragonsnail1 Melee Hit Location",
                "1d20",
                new string[] {
                    "08 Shell",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonsnail2",
                "Dragonsnail2 Melee Hit Location",
                "1d20",
                new string[] {
                    "07 Shell",
                    "12 Body",
                    "16 Head1",
                    "20 Head2",
                }
            ),
            new RollTableEntry(
                "loc_elemental",
                "Elemental Melee Hit Location",
                "1d20",
                new string[] {
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_elephant",
                "Elephant Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "08 HindQ",
                    "12 ForeQ",
                    "14 RFLeg",
                    "16 LFLeg",
                    "17 Trunk",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fachan",
                "Fachan Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Leg",
                    "10 Abdomen",
                    "12 Chest",
                    "16 Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fish",
                "Fish Melee Hit Location",
                "1d20",
                new string[] {
                    "03 Tail",
                    "08 HindBody",
                    "13 Forebody",
                    "14 RFin",
                    "15 LFin",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fourlegged",
                "FourLegged Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "13 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gargoyle",
                "Gargoyle Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "10 Chest",
                    "12 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_giantinsect",
                "GiantInsect Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "09 Abdomen",
                    "13 Thorax",
                    "14 RFLeg",
                    "15 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gobbler",
                "Gobbler Melee Hit Location",
                "1d20",
                new string[] {
                    "02 R Leg",
                    "04 L Leg",
                    "05 Tail",
                    "08 RF Arm",
                    "11 LF Arm",
                    "14 RU Arm",
                    "17 LU Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gorgon",
                "Gorgon Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "10 Chest",
                    "12 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_grampus",
                "Grampus Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "09 Abdomen",
                    "10 RCLeg",
                    "11 LCLeg",
                    "12 RFLeg",
                    "13 LFLeg",
                    "15 RFClaw",
                    "17 LFClaw",
                    "20 Thorax",
                }
            ),
            new RollTableEntry(
                "loc_greatrace",
                "GreatRace Melee Hit Location",
                "1d20",
                new string[] {
                    "04 Base",
                    "08 Upper Torso",
                    "12 RPincer",
                    "16 LPincer",
                    "18 Feeding Head",
                    "20 Sensory head",
                }
            ),
            new RollTableEntry(
                "loc_griffin",
                "Griffin Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "11 RWing",
                    "12 LWing",
                    "14 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_grotaron",
                "Grotaron Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "11 Chest",
                    "14 RArm",
                    "17 LArm",
                    "20 CArm",
                }
            ),
            new RollTableEntry(
                "loc_grue",
                "Grue Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_harpy",
                "Harpy Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RClaw",
                    "04 LClaw",
                    "07 Abdomen",
                    "09 Chest",
                    "13 RWing",
                    "17 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_headhanger",
                "Headhanger Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "13 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_headless",
                "Headless Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "14 Chest",
                    "17 RArm",
                    "20 LArm",
                }
            ),
            new RollTableEntry(
                "loc_huan_to",
                "Huan To Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_hulk",
                "Hulk Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_humanoid",
                "Humanoid Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_hydra",
                "Hydra Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Body",
                    "20 Chaotic Head",
                }
            ),
            new RollTableEntry(
                "loc_jabberwock",
                "Jabberwock Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "07 Tail",
                    "10 Abdomen",
                    "12 Chest",
                    "13 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_kali",
                "Kali Melee Hit Location",
                "1d20",
                new string[] {
                    "03 Hindtail",
                    "06 Midtail",
                    "09 Abdomen",
                    "12 Forebody",
                    "13 RL Arm",
                    "14 LL Arm",
                    "16 R Arm",
                    "18 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_krarshtkid",
                "Krarshtkid Melee Hit Location",
                "1d20",
                new string[] {
                    "03 Leg 1",
                    "06 Leg 2",
                    "09 Leg 3",
                    "12 Leg 4",
                    "15 Leg 5",
                    "18 Leg 6",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_lamia",
                "Lamia Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "10 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_lion",
                "Lion Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "13 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_lucan",
                "Lucan Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "07 Abdomen",
                    "09 Thorax",
                    "11 RL Arm",
                    "13 LL Arm",
                    "15 RU Arm",
                    "17 LU Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_magisaur",
                "Magisaur Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "16 RForepaw",
                    "17 LForepaw",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_manatee",
                "Manatee Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "10 Abdomen",
                    "12 Forebody",
                    "15 R Flipper",
                    "18 L Flipper",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_manticore",
                "Manticore Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "06 Tail",
                    "09 HindQ",
                    "12 ForeQ",
                    "14 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_mantis",
                "Mantis Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "05 Abdomen",
                    "06 R Wing",
                    "07 L Wing",
                    "08 Thorax",
                    "09 R Claw",
                    "12 L Claw",
                    "15 Head",
                    "18 RHLeg",
                    "20 LHLeg",
                }
            ),
            new RollTableEntry(
                "loc_merman",
                "Merman Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "10 Abdomen",
                    "12 Forebody",
                    "15 R Arm",
                    "18 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_migo",
                "Migo Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "06 Abdomen",
                    "08 Chest",
                    "09 RLArm",
                    "11 RUArm",
                    "12 LLArm",
                    "14 LLArm",
                    "16 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_morocanth",
                "Morocanth Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "07 HindQ",
                    "10 ForeQ",
                    "13 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_moth",
                "Moth Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Abdomen",
                    "03 RHLeg",
                    "04 LHLeg",
                    "05 RCLeg",
                    "06 LCLeg",
                    "07 RFLeg",
                    "08 LFLeg",
                    "10 R Wing",
                    "12 L Wing",
                    "13 Thorax",
                    "16 RF Wing",
                    "19 LF Wing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_murthoi",
                "Murthoi Melee Hit Location",
                "1d20",
                new string[] {
                    "07 Flagelum",
                    "10 Abdomen",
                    "12 Chest",
                    "15 R Arm",
                    "18 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_naga",
                "Naga Melee Hit Location",
                "1d20",
                new string[] {
                    "03 Hindtail",
                    "06 Midtail",
                    "09 Abdomen",
                    "12 Forebody",
                    "15 R Arm",
                    "18 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_newtling",
                "Newtling Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_nuckelavee",
                "Nuckelavee Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "06 HindQ",
                    "07 ForeQ",
                    "09 RFLeg",
                    "11 LFLeg",
                    "13 Horse Head",
                    "14 Chest",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_octopus",
                "Octopus Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Arm1",
                    "04 Arm2",
                    "06 Arm3",
                    "08 Arm4",
                    "10 Arm5",
                    "12 Arm6",
                    "14 Arm7",
                    "16 Arm8",
                    "18 Head",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_oldone",
                "OldOne Melee Hit Location",
                "1d20",
                new string[] {
                    "01 Tendril1",
                    "02 Tendril2",
                    "03 Tendril3",
                    "04 Tendril4",
                    "05 Tendril5",
                    "08 Torso",
                    "09 Tentacle1",
                    "10 Tentacle2",
                    "11 Tentacle3",
                    "12 Tentacle4",
                    "13 Tentacle5",
                    "14 Wing1",
                    "15 Wing2",
                    "16 Wing3",
                    "17 Wing4",
                    "18 Wing5",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_orveltor",
                "Orveltor Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "10 Body",
                    "13 RArm",
                    "16 LArm",
                    "19 Carm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_plesiosaur",
                "Plesiosaur Melee Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "03 RHPaddle",
                    "05 LHPaddle",
                    "08 HindBody",
                    "11 Body",
                    "13 RFPaddle",
                    "15 LFPaddle",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_preserver",
                "Preserver Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "10 Chest",
                    "12 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_rocklizard",
                "RockLizard Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "04 RHLeg",
                    "06 LHLeg",
                    "09 HindQ",
                    "13 ForeQ",
                    "15 RFLeg",
                    "17 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_satyr",
                "Satyr Melee Hit Location",
                "1d20",
                new string[] {
                    "04 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "12 Chest",
                    "15 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_scorpion",
                "Scorpion Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "05 RCLeg",
                    "06 LCLeg",
                    "07 RFLeg",
                    "08 LFLeg",
                    "10 Tail",
                    "13 Thorax",
                    "15 RFClaw",
                    "17 LFClaw",
                    "20 Forebody",
                }
            ),
            new RollTableEntry(
                "loc_scorpionman",
                "Scorpionman Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 RCLeg",
                    "04 RFLeg",
                    "05 LHLeg",
                    "06 LCLeg",
                    "08 LFLeg",
                    "10 Tail",
                    "12 Thorax",
                    "14 Chest",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_serpent",
                "Serpent Melee Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_spider",
                "Spider Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RBLeg",
                    "02 RHLeg",
                    "03 LBLeg",
                    "04 LHLeg",
                    "08 Thorax",
                    "10 RCLeg",
                    "12 RFLeg",
                    "14 LCLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_stirge",
                "Stirge Melee Hit Location",
                "1d20",
                new string[] {
                    "04 Tail",
                    "08 Abdomen",
                    "12 Body",
                    "14 RWing",
                    "16 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_tako",
                "Tako Melee Hit Location",
                "1d20",
                new string[] {
                    "02 Arm1",
                    "04 Arm2",
                    "06 Arm3",
                    "08 Arm4",
                    "10 Arm5",
                    "12 Arm6",
                    "14 Arm7",
                    "16 Arm8",
                    "18 Head",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_tengu",
                "Tengu Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "09 Abdomen",
                    "10 Chest",
                    "12 RWing",
                    "14 LWing",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_termite",
                "Termite Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "08 Abdomen",
                    "12 Thorax",
                    "14 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_tree",
                "Tree Melee Hit Location",
                "1d20",
                new string[] {
                    "10 Trunk",
                    "11 Branch1",
                    "12 Branch2",
                    "13 Branch3",
                    "14 Branch4",
                    "15 Branch5",
                    "16 Branch6",
                    "17 Branch7",
                    "18 Branch8",
                    "19 Branch9",
                    "20 Branch10",
                }
            ),
            new RollTableEntry(
                "loc_walktapus",
                "Walktapus Melee Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "05 Abdomen",
                    "06 Chest",
                    "08 RArm",
                    "10 LArm",
                    "11 Tentacle1",
                    "12 Tentacle2",
                    "13 Tentacle3",
                    "14 Tentacle4",
                    "15 Tentacle5",
                    "16 Tentacle6",
                    "17 Tentacle7",
                    "18 Tentacle8",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wasp",
                "Wasp Melee Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "08 Abdomen",
                    "10 Thorax",
                    "12 R Wing",
                    "14 L Wing",
                    "15 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wyrm",
                "Wyrm Melee Hit Location",
                "1d20",
                new string[] {
                    "04 Tail",
                    "08 Abdomen",
                    "12 Chest",
                    "14 RWing",
                    "16 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wyvern",
                "Wyvern Melee Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "08 Abdomen",
                    "11 Chest",
                    "12 Tail",
                    "14 RWing",
                    "16 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_allosaur_missile",
                "Allosaur Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "16 RClaw",
                    "17 LClaw",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_amphisboenae_missile",
                "Amphisboenae Missile Hit Location",
                "1d20",
                new string[] {
                    "06 Head",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_ape_missile",
                "Ape Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "08 Abdomen",
                    "13 Chest",
                    "16 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_baboon_missile",
                "Baboon Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_behemoth_missile",
                "Behemoth Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "04 RHLeg",
                    "06 LHLeg",
                    "10 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_bird_missile",
                "Bird Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RClaw",
                    "02 LClaw",
                    "11 Body",
                    "15 RWing",
                    "19 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_broo_missile",
                "Broo Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_centaur_missile",
                "Centaur Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "06 HindQ",
                    "10 ForeQ",
                    "11 RFLeg",
                    "12 LFLeg",
                    "17 Chest",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_centipede_missile",
                "Centipede Missile Hit Location",
                "1d20",
                new string[] {
                    "04 R Side Legs",
                    "08 L Side Legs",
                    "18 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_ceratopsian_missile",
                "Ceratopsian Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "03 RHLeg",
                    "05 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_chonchon_missile",
                "Chonchon Missile Hit Location",
                "1d20",
                new string[] {
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_clifftoad_missile",
                "CliffToad Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RHLeg",
                    "06 LHLeg",
                    "10 Abdomen",
                    "14 Chest",
                    "15 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_cockatrice_missile",
                "Cockatrice Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "03 RClaw",
                    "05 LClaw",
                    "10 Body",
                    "14 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_crab_missile",
                "Crab Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "09 Thorax",
                    "10 RCLeg",
                    "11 LCLeg",
                    "12 RFLeg",
                    "13 LFLeg",
                    "15 RFClaw",
                    "17 LFClaw",
                    "20 Forebody",
                }
            ),
            new RollTableEntry(
                "loc_crocodile_missile",
                "Crocodile Missile Hit Location",
                "1d20",
                new string[] {
                    "03 Tail",
                    "04 RHLeg",
                    "05 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "15 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_demibird_missile",
                "DemiBird Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "16 RWing",
                    "17 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_doc_missile",
                "Doc Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "11 Missed",
                    "13 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 Missed",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragon_missile",
                "Dragon Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "02 RHLeg",
                    "03 LHLeg",
                    "08 HindQ",
                    "14 ForeQ",
                    "15 RWing",
                    "16 LWing",
                    "17 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonnewt_missile",
                "Dragonnewt Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "03 RLeg",
                    "05 LLeg",
                    "09 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonsnail1_missile",
                "Dragonsnail1 Missile Hit Location",
                "1d20",
                new string[] {
                    "08 Shell",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_dragonsnail2_missile",
                "Dragonsnail2 Missile Hit Location",
                "1d20",
                new string[] {
                    "07 Shell",
                    "12 Body",
                    "16 Head1",
                    "20 Head2",
                }
            ),
            new RollTableEntry(
                "loc_elemental_missile",
                "Elemental Missile Hit Location",
                "1d20",
                new string[] {
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_elephant_missile",
                "Elephant Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "08 HindQ",
                    "12 ForeQ",
                    "14 RFLeg",
                    "16 LFLeg",
                    "17 Trunk",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fachan_missile",
                "Fachan Missile Hit Location",
                "1d20",
                new string[] {
                    "04 Leg",
                    "09 Abdomen",
                    "15 Chest",
                    "18 Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fish_missile",
                "Fish Missile Hit Location",
                "1d20",
                new string[] {
                    "03 Tail",
                    "08 HindBody",
                    "13 Forebody",
                    "14 RFin",
                    "15 LFin",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_fourlegged_missile",
                "FourLegged Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gargoyle_missile",
                "Gargoyle Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "08 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_giantinsect_missile",
                "GiantInsect Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "09 Abdomen",
                    "13 Thorax",
                    "14 RFLeg",
                    "15 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gobbler_missile",
                "Gobbler Missile Hit Location",
                "1d20",
                new string[] {
                    "01 R Leg",
                    "02 L Leg",
                    "03 Tail",
                    "05 RF Arm",
                    "07 LF Arm",
                    "09 RU Arm",
                    "11 LU Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_gorgon_missile",
                "Gorgon Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "08 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_grampus_missile",
                "Grampus Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "09 Abdomen",
                    "10 RCLeg",
                    "11 LCLeg",
                    "12 RFLeg",
                    "13 LFLeg",
                    "15 RFClaw",
                    "17 LFClaw",
                    "20 Thorax",
                }
            ),
            new RollTableEntry(
                "loc_greatrace_missile",
                "GreatRace Missile Hit Location",
                "1d20",
                new string[] {
                    "04 Base",
                    "08 Upper Torso",
                    "12 RPincer",
                    "16 LPincer",
                    "18 Feeding Head",
                    "20 Sensory head",
                }
            ),
            new RollTableEntry(
                "loc_griffin_missile",
                "Griffin Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "07 HindQ",
                    "12 ForeQ",
                    "14 RWing",
                    "16 LWing",
                    "17 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_grotaron_missile",
                "Grotaron Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "14 Chest",
                    "16 RArm",
                    "18 LArm",
                    "20 CArm",
                }
            ),
            new RollTableEntry(
                "loc_grue_missile",
                "Grue Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "04 RLeg",
                    "07 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_harpy_missile",
                "Harpy Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RClaw",
                    "02 LClaw",
                    "06 Abdomen",
                    "11 Chest",
                    "15 RWing",
                    "19 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_headhanger_missile",
                "Headhanger Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_headless_missile",
                "Headless Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "16 Chest",
                    "18 RArm",
                    "20 LArm",
                }
            ),
            new RollTableEntry(
                "loc_huan_to_missile",
                "Huan To Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_hulk_missile",
                "Hulk Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_humanoid_missile",
                "Humanoid Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_hydra_missile",
                "Hydra Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Body",
                    "20 Chaotic Head",
                }
            ),
            new RollTableEntry(
                "loc_jabberwock_missile",
                "Jabberwock Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "05 Tail",
                    "09 Abdomen",
                    "13 Chest",
                    "14 RWing",
                    "15 LWing",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_kali_missile",
                "Kali Missile Hit Location",
                "1d20",
                new string[] {
                    "03 Hindtail",
                    "06 Midtail",
                    "10 Abdomen",
                    "15 Forebody",
                    "16 RL Arm",
                    "17 LL Arm",
                    "18 R Arm",
                    "19 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_krarshtkid_missile",
                "Krarshtkid Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Leg 1",
                    "04 Leg 2",
                    "06 Leg 3",
                    "08 Leg 4",
                    "10 Leg 5",
                    "12 Leg 6",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_lamia_missile",
                "Lamia Missile Hit Location",
                "1d20",
                new string[] {
                    "05 Tail",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_lion_missile",
                "Lion Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_lucan_missile",
                "Lucan Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "09 Abdomen",
                    "14 Thorax",
                    "15 RL Arm",
                    "16 LL Arm",
                    "17 RU Arm",
                    "18 LU Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_magisaur_missile",
                "Magisaur Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Tail",
                    "05 RLeg",
                    "08 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "16 RForepaw",
                    "17 LForepaw",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_manatee_missile",
                "Manatee Missile Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "10 Abdomen",
                    "15 Forebody",
                    "17 R Flipper",
                    "19 L Flipper",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_manticore_missile",
                "Manticore Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "05 Tail",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "17 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_mantis_missile",
                "Mantis Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "05 Abdomen",
                    "06 R Wing",
                    "07 L Wing",
                    "08 Thorax",
                    "09 R Claw",
                    "12 L Claw",
                    "15 Head",
                    "18 RHLeg",
                    "20 LHLeg",
                }
            ),
            new RollTableEntry(
                "loc_merman_missile",
                "Merman Missile Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "10 Abdomen",
                    "15 Forebody",
                    "17 R Arm",
                    "19 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_migo_missile",
                "Migo Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "06 Abdomen",
                    "08 Chest",
                    "09 RLArm",
                    "11 RUArm",
                    "12 LLArm",
                    "14 LLArm",
                    "16 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_morocanth_missile",
                "Morocanth Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_moth_missile",
                "Moth Missile Hit Location",
                "1d20",
                new string[] {
                    "02 Abdomen",
                    "03 RHLeg",
                    "04 LHLeg",
                    "05 RCLeg",
                    "06 LCLeg",
                    "07 RFLeg",
                    "08 LFLeg",
                    "10 R Wing",
                    "12 L Wing",
                    "13 Thorax",
                    "16 RF Wing",
                    "19 LF Wing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_murthoi_missile",
                "Murthoi Missile Hit Location",
                "1d20",
                new string[] {
                    "05 Flagelum",
                    "10 Abdomen",
                    "15 Chest",
                    "17 R Arm",
                    "19 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_naga_missile",
                "Naga Missile Hit Location",
                "1d20",
                new string[] {
                    "03 Hindtail",
                    "06 Midtail",
                    "10 Abdomen",
                    "15 Forebody",
                    "17 R Arm",
                    "19 L Arm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_newtling_missile",
                "Newtling Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "04 RLeg",
                    "07 LLeg",
                    "11 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_nuckelavee_missile",
                "Nuckelavee Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RHLeg",
                    "04 LHLeg",
                    "06 HindQ",
                    "07 ForeQ",
                    "09 RFLeg",
                    "11 LFLeg",
                    "13 Horse Head",
                    "14 Chest",
                    "16 RArm",
                    "18 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_octopus_missile",
                "Octopus Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Arm1",
                    "02 Arm2",
                    "03 Arm3",
                    "04 Arm4",
                    "05 Arm5",
                    "06 Arm6",
                    "07 Arm7",
                    "08 Arm8",
                    "13 Head",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_oldone_missile",
                "OldOne Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tendril1",
                    "02 Tendril2",
                    "03 Tendril3",
                    "04 Tendril4",
                    "05 Tendril5",
                    "08 Torso",
                    "09 Tentacle1",
                    "10 Tentacle2",
                    "11 Tentacle3",
                    "12 Tentacle4",
                    "13 Tentacle5",
                    "14 Wing1",
                    "15 Wing2",
                    "16 Wing3",
                    "17 Wing4",
                    "18 Wing5",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_orveltor_missile",
                "Orveltor Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "10 Body",
                    "13 RArm",
                    "16 LArm",
                    "19 Carm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_plesiosaur_missile",
                "Plesiosaur Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "02 RHPaddle",
                    "03 LHPaddle",
                    "09 HindBody",
                    "15 Body",
                    "16 RFPaddle",
                    "17 LFPaddle",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_preserver_missile",
                "Preserver Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "08 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_rocklizard_missile",
                "RockLizard Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Tail",
                    "03 RHLeg",
                    "05 LHLeg",
                    "09 HindQ",
                    "14 ForeQ",
                    "16 RFLeg",
                    "18 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_satyr_missile",
                "Satyr Missile Hit Location",
                "1d20",
                new string[] {
                    "03 RLeg",
                    "06 LLeg",
                    "10 Abdomen",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_scorpion_missile",
                "Scorpion Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RBLeg",
                    "04 LBLeg",
                    "05 RCLeg",
                    "06 LCLeg",
                    "07 RFLeg",
                    "08 LFLeg",
                    "10 Tail",
                    "13 Thorax",
                    "15 RFClaw",
                    "17 LFClaw",
                    "20 Forebody",
                }
            ),
            new RollTableEntry(
                "loc_scorpionman_missile",
                "Scorpionman Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 RCLeg",
                    "03 RFLeg",
                    "04 LHLeg",
                    "05 LCLeg",
                    "06 LFLeg",
                    "07 Tail",
                    "10 Thorax",
                    "15 Chest",
                    "17 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_serpent_missile",
                "Serpent Missile Hit Location",
                "1d20",
                new string[] {
                    "06 Tail",
                    "14 Body",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_spider_missile",
                "Spider Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RBLeg",
                    "02 RHLeg",
                    "03 LBLeg",
                    "04 LHLeg",
                    "08 Thorax",
                    "10 RCLeg",
                    "12 RFLeg",
                    "14 LCLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_stirge_missile",
                "Stirge Missile Hit Location",
                "1d20",
                new string[] {
                    "04 Tail",
                    "08 Abdomen",
                    "12 Body",
                    "14 RWing",
                    "16 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_tako_missile",
                "Tako Missile Hit Location",
                "1d20",
                new string[] {
                    "01 Arm1",
                    "02 Arm2",
                    "03 Arm3",
                    "04 Arm4",
                    "05 Arm5",
                    "06 Arm6",
                    "07 Arm7",
                    "08 Arm8",
                    "13 Head",
                    "20 Body",
                }
            ),
            new RollTableEntry(
                "loc_tengu_missile",
                "Tengu Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "08 Abdomen",
                    "13 Chest",
                    "15 RWing",
                    "17 LWing",
                    "18 RArm",
                    "19 LArm",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_termite_missile",
                "Termite Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "08 Abdomen",
                    "12 Thorax",
                    "14 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_tree_missile",
                "Tree Missile Hit Location",
                "1d20",
                new string[] {
                    "10 Trunk",
                    "11 Branch1",
                    "12 Branch2",
                    "13 Branch3",
                    "14 Branch4",
                    "15 Branch5",
                    "16 Branch6",
                    "17 Branch7",
                    "18 Branch8",
                    "19 Branch9",
                    "20 Branch10",
                }
            ),
            new RollTableEntry(
                "loc_walktapus_missile",
                "Walktapus Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RLeg",
                    "02 LLeg",
                    "04 Abdomen",
                    "07 Chest",
                    "08 RArm",
                    "09 LArm",
                    "10 Tentacle1",
                    "11 Tentacle2",
                    "12 Tentacle3",
                    "13 Tentacle4",
                    "14 Tentacle5",
                    "15 Tentacle6",
                    "16 Tentacle7",
                    "17 Tentacle8",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wasp_missile",
                "Wasp Missile Hit Location",
                "1d20",
                new string[] {
                    "01 RHLeg",
                    "02 LHLeg",
                    "03 RCLeg",
                    "04 LCLeg",
                    "08 Abdomen",
                    "10 Thorax",
                    "12 R Wing",
                    "14 L Wing",
                    "15 RFLeg",
                    "16 LFLeg",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wyrm_missile",
                "Wyrm Missile Hit Location",
                "1d20",
                new string[] {
                    "03 Tail",
                    "08 Abdomen",
                    "14 Chest",
                    "16 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "loc_wyvern_missile",
                "Wyvern Missile Hit Location",
                "1d20",
                new string[] {
                    "02 RLeg",
                    "04 LLeg",
                    "07 Abdomen",
                    "13 Chest",
                    "14 Tail",
                    "16 RWing",
                    "18 LWing",
                    "20 Head",
                }
            ),
            new RollTableEntry(
                "rainbow_d8",
                "Rainbow Shield",
                "1d8",
                new string[] {
                    "01 Red, Fire/Light",
                    "02 Orange, Earth/Petrification",
                    "03 Yellow, Air/Electrical",
                    "04 Green, Nature/Poison",
                    "05 Blue, Water/Acid",
                    "06 Indigo, Moon/Mental",
                    "07 Violet, Dark/Spirit",
                    "08 Swirled, Roll 2d7",
                }
            ),
            new RollTableEntry(
                "rainbow_d7",
                "Rainbow Shield",
                "1d7",
                new string[] {
                    "01 Red, Fire/Light",
                    "02 Orange, Earth/Petrification",
                    "03 Yellow, Air/Electrical",
                    "04 Green, Nature/Poison",
                    "05 Blue, Water/Acid",
                    "06 Indigo, Moon/Mental",
                    "07 Violet, Dark/Spirit",
                }
            ),
            new RollTableEntry(
                "korlin",
                "Korlin D35 Vow Table",
                "1d35",
                new string[] {
                    "01 Devote yourself to life. If there is any way to prevent death and preserve life, it is the way of Korlin.",
                    "02 In judging others, you yourself are judged  with the judgement by which you judge.",
                    "03 Honor your words and live by them. Your oath is as true as these laws.",
                    "04 Help others live a full life, and harm not the harmless.",
                    "05 Help those in misfortune, for gold is valueless next to life.",
                    "06 Heal the sick and wounded who ask for assistance. Those whom you have deathlessly defeated remain your responsiblity.",
                    "07 Abandon all greed, hatred, and evil feelings towards others. These close the mind to truth and engender a hatred for life.",
                    "08 Pay homage to life by living freely and allowing others the same.",
                    "09 Respect those who would teach you and ask nothing of those you teach.",
                    "10 Respect the laws of mortals,but remember always that your fealty is to the laws of Korlin.",
                    "11 Pledge your life, soul, and word to Korlin. Never should you worship another and never should your faith falter.",
                    "12 Not Pride, Humility. Not Greed, Generosity. Not Envy, Love. Not Wrath, Kindness. Not Lust, Temperance. Not Gluttony, Moderation. Not Sloth, Zeal Never Evil, always Good. Bring light to the darkness and you shall always be counted among Korlin's blessed.",
                    "13 Wear the symbol of Korlin with Honor, Love, and Devotion. Never rape, steal, or murder.",
                    "14 Take only what you need, and give only what you can.",
                    "15 Thou shalt believe all that Rofirein teaches, and shalt observe all her directions.",
                    "16 Thou shalt defend the Church of Suberle with all thy might.",
                    "17 Thou shalt respect all weaknesses, and shalt constitute thyself the defender of them.",
                    "18 Thou shalt not recoil before thine enemy.",
                    "19 Thou shalt make war against the Infidel followers of Pyrtechon without cessation and without mercy.",
                    "20 Thou shalt perform scrupulously thy duties of Justice, in line with the Teachings of the Church of Korlin.",
                    "21 Thou shalt never lie, and shall remain faithful to thy pledged word.",
                    "22 Thou shalt be generous, and give largess to all that have need.",
                    "23 Thou shalt avoid avarice like the deadly pestilence and shalt embrace its opposite.",
                    "24 Thou shalt keep thyself chaste for the sake of her whom Suberle has chosen for thee to love.",
                    "25 Thou shalt only step foot in Temples dedicated to Korlin and his Holy allies, unless thy enemies seek sanctuary in an unholy temple, in which case it is thy duty to cleanse the place of their filth.",
                    "26 Thou art a Paladin of Korlin, one of his beloved few. Remember this in all that thy do. As one of the few Paladins of the Holy Order of Korlin, it is your task to root out evil, injustice and tyranny wherever thee may be.",
                    "27 Truth, honor, and loyalty are your highest calling. Never deviate from these principles, even if it should mean your death.",
                    "28 Never question Suberle, nor her priesthood. Your loyalty to your God will be put to the test in a time of trial. Remain steadfast and loyal during that time and all shall be achieved.",
                    "29 Aid those that you can, and neither ask nor expect reward. Your valor and honor will bring glory to Korlin's name.",
                    "30 Do not suffer evil, hatred, or corruption, for these are the greatest sins of mortals. Battle against these sins and those who commit them, but be mindful that there is no shame in retreat, especially to preserve your life and the lives of other innocents around you.",
                    "31 Your word is your bond. Keep your word, even should it bring you to death's gate. The faithful of Korlin must honor their word and trust in their valor and their God to aid them in keeping their promises. There is no honor in a vow that calls for the death or dishonor of innocents, and such promises may be freely broken.",
                    "32 All life is sacred. Raping, pillaging, and other crimes of war are strictly forbidden to those in Korlin's service. Killing should be an act of last resort. Honor your enemies, regardless of their race, even in defeat.",
                    "33 Make no demands of others. A knight of Korlin should be proud and self sufficient. There is no shame in accepting offers of help, but a Paladin in Korlin's service should never reduce himself to beggary or desperate pleas.",
                    "34 Unless specifically directed by Korlin or Sunerle, do not enter another god's temple except in times of extreme duress. Even when desperate, you may only enter temples of gods sympathetic to Korlin.",
                    "35 Remain virtuous and honorable in all ways. Do not participate in anything that would require you to compromise your Code. These are the laws of the Paladin, and those who would treat them lightly are not worthy of the title.",
                }
            ),
        };
    }
}
