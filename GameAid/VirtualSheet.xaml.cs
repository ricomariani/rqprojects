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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GameAid
{
    public enum SkillType
    {
        Stat,
        Checkable_Skill,
        Non_Checkable_Skill,
        Shugenja,
        Runemagic,
        OneUse_Runemagic
    }

    /// <summary>
    /// Interaction logic for VirtualSheet.xaml
    /// </summary>
    public partial class VirtualSheet : UserControl
    {
        static MainWindow Main { get { return MainWindow.mainWindow; } }

        string name = null;
        bool fProcessing = true;
        bool fTrainingMode = false;
        bool fSquadMode = false;

        string lastWpn = "";
        bool isSorceror = false;
        bool isWizard = false;

        string deferredCmd;
        string deferredSchool;
        string deferredEffect;
        long deferredNumber;

        static int squadCol = 0;

        List<WeaponRecord> weapons = null;
        Dictionary<string, ConsumeableStat> fatigue = new Dictionary<string, ConsumeableStat>();
        Dictionary<string, ConsumeableStat> wounds = new Dictionary<string, ConsumeableStat>();
        Dictionary<string, ConsumeableStat> mana = new Dictionary<string, ConsumeableStat>();
        Dictionary<string, TextBlock> armor = new Dictionary<string, TextBlock>();
        Dictionary<string, CheckableSkill> checks = new Dictionary<string, CheckableSkill>();
        Dictionary<string, ShugenjaInfo> shugenja = new Dictionary<string, ShugenjaInfo>();
        Dictionary<string, RunemagicInfo> runemagic = new Dictionary<string, RunemagicInfo>();
        Dictionary<string, SpiritInfo> spirits = new Dictionary<string, SpiritInfo>();

        public VirtualSheet()
        {
            InitializeComponent();
        }

        public class WeaponRecord
        {
            public string name;
            public int attack;
            public int parry;
            public int ap;
            public int sr;
            public string dmg;
        }

        class KeyVal
        {
            public string key;
            public int val;
        }

        class CheckableSkill
        {
            public string key;
            public CheckBox check;
            public TextBlock text;
        }

        class ShugenjaInfo
        {
            public string key;
            public TextBlock charges;                
        }

        class RunemagicInfo
        {
            public string key;
            public TextBlock used;
        }

        class SpiritInfo
        {
            public string key;
            public TextBlock used;
        }

        class ConsumeableStat
        {
            public string name;
            public int max;
            public int used;
            public TextBlock block;
        }

        internal void CustomInit()
        {
            panel4.Visibility = Visibility.Collapsed;
            panel5.Visibility = Visibility.Collapsed;
        }

        void FadeIn()
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(scrollviewer, new NameScope());

            // Register the name with the page to which the element belongs.
            scrollviewer.RegisterName("canvas", scrollviewer);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(1000));
    
            GameMap.Anim2Point(story, dur, "canvas", ScrollViewer.OpacityProperty, 0, 1);

            story.Begin(scrollviewer);
        }

        void FadeOut(Action action)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(scrollviewer, new NameScope());

            // Register the name with the page to which the element belongs.
            scrollviewer.RegisterName("canvas", scrollviewer);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(500));
            
            GameMap.Anim2Point(story, dur, "canvas", ScrollViewer.OpacityProperty, 1, 0);

            story.Completed += new EventHandler((_,__) => 
            {
                action();                
            });

            story.Begin(scrollviewer);
        }

        public void ChangeToNamedPlayer(string name)
        {
            comboParty.Text = name;
            // InitializeForName(name);
        }

        void InitializeForName(string name)
        {
            this.name = name;

            fProcessing = true;

            FadeOut(() =>
            {
                isSorceror = false;
                isWizard = false;

                gTop.Children.Clear();
                gTop.RowDefinitions.Clear();
                gBottom.Children.Clear();
                gBottom.RowDefinitions.Clear();
                g1.Children.Clear();
                g1.RowDefinitions.Clear();
                g2.Children.Clear();
                g2.RowDefinitions.Clear();
                g3.Children.Clear();
                g3.RowDefinitions.Clear();
                g4.Children.Clear();
                g4.RowDefinitions.Clear();
                g5.Children.Clear();
                g5.RowDefinitions.Clear();
                g6.Children.Clear();
                g6.RowDefinitions.Clear();

                checks = new Dictionary<string, CheckableSkill>();
                shugenja = new Dictionary<string, ShugenjaInfo>();
                runemagic = new Dictionary<string, RunemagicInfo>();
                spirits = new Dictionary<string, SpiritInfo>();

                weapons = new List<WeaponRecord>();

                Main.SendHost(String.Format("dir {0}", name));
                Main.SendHost(String.Format("dir {0}/magic", name));
                Main.SendHost(String.Format("dir {0}/agility", name));
                Main.SendHost(String.Format("dir {0}/communication", name));
                Main.SendHost(String.Format("dir {0}/manipulation", name));
                Main.SendHost(String.Format("dir {0}/stealth", name));
                Main.SendHost(String.Format("dir {0}/knowledge", name));
                Main.SendHost(String.Format("dir {0}/alchemy", name));
                Main.SendHost(String.Format("dir {0}/perception", name));
                Main.SendHost(String.Format("dir {0}/special", name));
                Main.SendHost(String.Format("dir {0}/_wpn", name));
                Main.SendHost(String.Format("dir {0}/_hit_location", name));
                Main.SendHost(String.Format("dir {0}/_armor", name));
                Main.SendHost(String.Format("dir {0}/mana", name));
                Main.SendHost(String.Format("dir {0}/_misc", name));
                Main.SendHost(String.Format("dir {0}/_battlemagic", name));
                Main.SendHost(String.Format("dir {0}/_spells", name));
                Main.SendHost(String.Format("dir {0}/_stored_spells", name));
                Main.SendHost(String.Format("dir {0}/_runemagic", name));
                Main.SendHost(String.Format("dir {0}/_spirits", name));
                Main.SendHost(String.Format("dir {0}/_herocast", name));
                Main.SendHost(String.Format("dir {0}/_wizardry", name));
                Main.SendHost(String.Format("dir {0}/_music", name));
                Main.SendHost(String.Format("dir {0}/_one_use", name));
                Main.SendHost(String.Format("dir {0}/_others_spells", name));
            });
        }

        string CellString(DictBundle b, string key)
        {
            if (b.dict.ContainsKey(key))
                return b.dict[key];
            else
                return "";
        }

        int CellInt(DictBundle b, string key)
        {
            int output = 0;

            if (b.dict.ContainsKey(key))
                if (Int32.TryParse(b.dict[key], out output))
                    return output;
                
            return output;
        }

        string partyPath;
        Dictionary<string, string> playerDict;
        Dictionary<string, string> partyDict;
        Dictionary<string, string> partiesDict;
        string partyPathActiveParty;
        string pendingSquadDir;

        public void Consider(DictBundle bundle)
        {
            if (pendingSquadDir != null && bundle.path == pendingSquadDir)
            {
                LoadSquadFromParty(bundle);
                pendingSquadDir = null;
            }

            if (bundle.path == partyPath)
            {
                partyDict = bundle.dict;

                var s = GetComboSelectionAsString(comboPlayers);
                if (s == "*Party" || s == "")
                {
                    SetPartyDropDown();
                } 
                return;
            }
            else switch (bundle.path)
            {
                case "_gameaid/_players":
                    SetPlayerDropDown(bundle);
                    break;

                case "_party":
                    partiesDict = bundle.dict;
                    if (bundle.dict.ContainsKey(Main.botchannel))
                    {
                        partyPathActiveParty = bundle.dict[Main.botchannel];
                    }

                    var s = GetComboSelectionAsString(comboPlayers);
                    var p = GetComboSelectionAsString(comboGroup);
                    if (s == "*Party" && p == "*Active")
                    {
                        SetGroupDropDownParty();
                    }
                    return;

                case "/":             
                    return;
            }

            if (name == null)
                return;

            switch (bundle.path)
            {
                case "_wounds":
                    ProcessWounds(bundle);
                    return;

                case "_mana":
                    ProcessConsumedMana(bundle);
                    return;

                case "_checks":
                    ProcessChecks(bundle);
                    return;

                case "_shugenja":
                    ProcessShugenja(bundle);
                    return;

                case "_runemagic":
                    ProcessRunemagicUsed(bundle);
                    return;

                case "_spiritmana":
                    ProcessSpiritManaConsumed(bundle);
                    return;

                case "_fatigue":
                    ProcessConsumedFatigue(bundle);
                    fProcessing = false;
                    return;
            }

            if (!fProcessing)
                return;

            if (!bundle.path.StartsWith(name))
                return;

            // strip off the name part of the path
            var path = bundle.path.Substring(name.Length);
            Grid g;

            // strip the leading slash
            if (path.StartsWith("/"))
                path = path.Substring(1);

            if (bundle.dict.ContainsKey(".."))
                bundle.dict.Remove("..");

            switch (path)
            {
                case "":
                    {
                        g = g1;
                        g.RowDefinitions.Clear();

                        AddSkillHeader(g, "Stats");
                        AddRow(bundle, g, "STR", SkillType.Stat);
                        AddRow(bundle, g, "CON", SkillType.Stat);
                        AddRow(bundle, g, "SIZ", SkillType.Stat);
                        AddRow(bundle, g, "INT", SkillType.Stat);
                        AddRow(bundle, g, "POW", SkillType.Stat);
                        AddRow(bundle, g, "DEX", SkillType.Stat);
                        AddRow(bundle, g, "APP", SkillType.Stat);

                        var q = from k in bundle.dict.Keys
                                where k.EndsWith("_school") && !k.StartsWith("spare_")
                                orderby k ascending
                                select k;

                        foreach (var k in q)
                        {
                            Main.SendHost(String.Format("dir {0}/{1}", name, k));
                        }

                        break;
                    }

                case "_red":
                case "_blue":
                case "_green":
                    ProcessColoration(bundle, path);
                    break;

                case "perception":
                    {
                        g = fSquadMode ? g2 : g1;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "magic":
                    {
                        if (bundle.dict.ContainsKey("intensity") && bundle.dict["intensity"] != "0")
                            isSorceror = true;

                        g = fSquadMode ? g2 : g1;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "agility":
                    {
                        g = fSquadMode ? g2 : g2;
                        StandardSkillClass(bundle, path, grid: g, addBlank: false);
                        break;
                    }

                case "manipulation":
                    {
                        g = fSquadMode ? g2 : g2;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "stealth":
                    {
                        g = fSquadMode ? g2 : g2;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "special":
                    {
                        g = fSquadMode ? g2 : g3;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "knowledge":
                    {
                        g = fSquadMode ? g2 : g2;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "alchemy":
                    {
                        g = fSquadMode ? g2 : g2;
                        StandardSkillClass(bundle, path, grid: g, addBlank: true);
                        break;
                    }

                case "communication":
                    {
                        g = fSquadMode ? g2 : g3;
                        StandardSkillClass(bundle, path, grid: g, addBlank: false);
                        break;
                    }

                case "_runemagic":
                case "_one_use":
                case "_herocast":
                case "_wizardry":
                case "_music":
                case "_spells":
                case "_stored_spells":
                case "_battlemagic":
                    StandardSkillClass(bundle, path, grid: g3, addBlank: true);
                    break;

                case "_spirits":
                    ProcessSpiritInventory(bundle);
                    break;

                case "_others_spells":
                    ProcessOthersSpells(bundle);
                    break;

                case "_misc":
                    ProcessMisc(bundle);
                    break;

                case "mana":
                    ProcessMana(bundle);
                    break;

                case "_hit_location":
                    ProcessHitLocations(bundle);
                    break;

                case "_armor":
                    ProcessArmor(bundle);
                    break;

                case "_wpn":
                    foreach (var k in bundle.dict.Keys)
                    {
                        if (bundle.dict[k] != "<Folder>")
                            continue;

                        if (k == "..")
                            continue;

                        Main.SendHost(String.Format("dir {0}/_wpn/{1}", name, k));
                        lastWpn = k;
                    }

                    Main.SendHost(String.Format("dir {0}/_red", name));
                    Main.SendHost(String.Format("dir {0}/_blue", name));
                    Main.SendHost(String.Format("dir {0}/_green", name));

                    break;

                default:
                    if (path.EndsWith("/.."))
                        break;

                    if (path.EndsWith("_school"))
                    {
                        if (bundle.dict.ContainsKey("spare_0"))
                            bundle.dict.Remove("spare_0");

                        StandardSkillClass(bundle, path, g3, true);
                        break;
                    }

                    else if (path.Contains("_wpn/"))
                    {
                        var w = new WeaponRecord();
                        w.dmg = CellString(bundle, "dmg");
                        w.name = path.Substring(path.LastIndexOf('/')+1);
                        w.parry = CellInt(bundle, "parry");
                        w.attack = CellInt(bundle, "attack");
                        w.sr = CellInt(bundle, "sr");
                        w.ap = CellInt(bundle, "ap");

                        weapons.Add(w);

                        if (w.name == lastWpn)
                            ProcessWeapons();
                    }
                    break;
            }
        }

        private void ProcessColoration(DictBundle bundle, string color)
        {
            // now apply the checks

            foreach (var k in bundle.dict.Keys)
            {
                string logicalKey = name + "|" + k;
                if (!checks.ContainsKey(logicalKey))
                    continue;

                var text = checks[logicalKey].text;
                if (text == null)
                    continue;

                switch (color)
                {
                    case "_red":
                        text.Foreground = System.Windows.Media.Brushes.Red;
                        break;

                    case "_blue":
                        text.Foreground = System.Windows.Media.Brushes.Blue;
                        break;

                    case "_green":
                        text.Foreground = System.Windows.Media.Brushes.Green;
                        break;
                }
            }
        }

        private void ProcessSpiritManaConsumed(DictBundle b)
        {
            foreach (var k in spirits.Keys)
            {
                var txt = spirits[k].used;
                if (txt == null)
                    continue;

                if (txt.Text != "0")
                    txt.Text = "0";
            }

            foreach (var k in b.dict.Keys)
            {
                // "Arcastar_Epos|spirit1" "used:1 max:5"

                string who;
                string effect;
                string used_and_max = b.dict[k];

                MainWindow.Parse2Ex(k, out who, out effect, '|');

                string logicalKey = who + "|" + effect;

                SpiritInfo spiritInfo;

                if (!spirits.TryGetValue(logicalKey, out spiritInfo))
                    continue;

                string used;
                string max;

                SplitUsedAndMax(used_and_max, out used, out max);

                spiritInfo.used.Text = used;
            }
        }

        public static void SplitUsedAndMax(string v, out string used, out string max)
        {
            used = "";
            max = "";

            MainWindow.Parse2(v, out used, out max);

            if (used.StartsWith("used:"))
                used = used.Substring(5);
            else
                used = "";

            if (max.StartsWith("max:"))
                max = max.Substring(4);
            else
                max = "";
        }

        string PlayerName(string playerKey)
        {
            int l = playerKey.Length;
            int u = playerKey.LastIndexOf('_');

            if (u < 0)
                return playerKey;

            if (u >= l - 2)
                return playerKey;

            return playerKey.Substring(0, u);
        }

        string GroupName(string playerKey)
        {
            int l = playerKey.Length;
            int u = playerKey.LastIndexOf('_');

            if (u < 0)
                return "*Ungrouped";

            if (u >= l - 2)
                return "*Ungrouped";

            return playerKey.Substring(u+1);
        }

        void SetPlayerDropDown(DictBundle bundle)
        {
            var q = (from k in bundle.dict.Keys
                     where bundle.dict[k] != "<Folder>"
                     orderby bundle.dict[k] ascending
                     select PlayerName(bundle.dict[k])).Distinct();

            var sel = GetComboSelectionAsString(comboPlayers);
            comboPlayers.Items.Clear();
            comboPlayers.Items.Add("*Party");

            foreach (var k in q)
                comboPlayers.Items.Add(k);

            playerDict = bundle.dict;

            if (sel == "") sel = "*Party";

            int isel = comboPlayers.Items.IndexOf(sel);
            if (isel >= 0)
                comboPlayers.SelectedIndex = isel;
            else
                comboPlayers.SelectedIndex = 0;

            comboGroup_SelectionChanged(null, null);
        }

        void SetGroupDropDownParty()
        {
            comboGroup.Items.Clear();
            int idx = 0;

            comboGroup.Items.Add("*Active");

            // FIX THIS partiesDict might be null

            foreach (var k in from s in partiesDict.Keys orderby s select s)
            {
                var v = partiesDict[k];
                if (v != "<Folder>" || k == "..")
                {
                    continue;
                }

                if ("_/party"+k == partyPath)
                    idx = comboGroup.Items.Count;

                comboGroup.Items.Add(k);
            }

            comboGroup.SelectedIndex = idx;
        }

        void SetGroupDropDown(string player)
        {
            comboGroup.IsEnabled = true;

            var p = player + "_";

            var q1 = (from k in playerDict.Keys
                      let v = playerDict[k]
                      where v.StartsWith(p) || v == player
                      let g = GroupName(playerDict[k])
                      orderby g ascending
                      select g).Distinct();

            comboGroup.Items.Clear();

            foreach (var k in q1)
                comboGroup.Items.Add(k);

            if (comboGroup.Items.Count > 0)
                comboGroup.SelectedIndex = 0;
        }
        
        void SetCharactersDropDown(string player)
        {
            var q1 = from k in playerDict.Keys
                     where playerDict[k] == player
                     orderby k ascending
                     select k;

            comboParty.Items.Clear();

            foreach (var k in q1)
                comboParty.Items.Add(k);
        }
        
        void SetPartyDropDown()
        {
            comboParty.Items.Clear();

            if (partyDict == null)
            {
                return;
            } 

            var q = from k in partyDict.Keys
                     where partyDict[k].StartsWith("y")
                     orderby k ascending
                     select k;

            foreach (var k in q)
                comboParty.Items.Add(k);
        }

        void ProcessArmor(DictBundle b)
        {
            foreach (var key in b.dict.Keys)
            {
                if (armor.ContainsKey(key))
                    armor[key].Text = b.dict[key];
            }
        }

        void ProcessRunemagicUsed(DictBundle b)
        {
            foreach (var k in runemagic.Keys)
            {
                var txt = runemagic[k].used;
                if (txt == null)
                    continue;

                if (txt.Text != "0")
                    txt.Text = "0";
            }

            foreach (var k in b.dict.Keys)
            {
                // "Arcastar_Epos|shield" "1"

                string who;
                string effect;
                string used_and_max = b.dict[k];

                MainWindow.Parse2Ex(k, out who, out effect, '|');

                string logicalKey = who + "|" + effect;

                RunemagicInfo runemagicInfo;

                if (!runemagic.TryGetValue(logicalKey, out runemagicInfo))
                    continue;

                string used;
                string max;

                MainWindow.Parse2(used_and_max, out used, out max);

                SplitUsedAndMax(used_and_max, out used, out max);

                runemagicInfo.used.Text = used;
            }
        }

        void ProcessShugenja(DictBundle b)
        {
            foreach (var k in shugenja.Keys)
            {
                var txt= shugenja[k].charges;
                if (txt == null)
                    continue;

                if (txt.Text != "")
                    txt.Text = "";
            }

            // now apply the checks

            foreach (var k in b.dict.Keys)
            {               
                // "Tamori_Sanzo|00001"
                // "school:air cost:2 charges:1 air_shield_1"

                string school;
                string cost;
                string charges;
                string effect;

                PartyInfo.GetShugenjaParts(b.dict[k], out school, out cost, out charges, out effect);

                string who;
                string id;

                MainWindow.Parse2Ex(k, out who, out id, '|');

                string logicalKey = who + "|_" + school + "_school|" + effect;

                ShugenjaInfo shugenjaInfo;

                if (!shugenja.TryGetValue(logicalKey, out shugenjaInfo))
                    continue;

                shugenjaInfo.charges.Text = charges;
            }
        }

        void ProcessChecks(DictBundle b)
        {
            foreach (var k in checks.Keys)
            {
                var cb = checks[k].check;
                if (cb == null)
                    continue;

                if (cb.IsChecked == false)
                    continue;

                // only touch what must change... absense means no check
                // so all must be reset
                cb.IsChecked = false;
            }

            // now apply the checks

            foreach (var k in b.dict.Keys)
            {
                if (!checks.ContainsKey(k))
                    continue;

                var cb = checks[k].check;
                if (cb == null)
                    continue;

                cb.IsChecked = true;
            }
        }

        void ProcessConsumedFatigue(DictBundle b)
        {
            // absent items are nil so... they must be reset
            foreach (var m in fatigue.Keys)
            {
                if (fatigue[m].used == 0)
                    continue;

                fatigue[m].used = 0;
                fatigue[m].block.Text = "0";
            }

            string prefix = name + "|";
            int len = prefix.Length;

            var q = from w in b.dict.Keys
                    where w.StartsWith(prefix)
                    select new KeyVal { key = w.Substring(len), val = ExtractInt("used:", b.dict[w]) };


            foreach (KeyVal kv in q)
            {
                if (fatigue.ContainsKey(kv.key))
                {
                    fatigue[kv.key].used = kv.val;
                    fatigue[kv.key].block.Text = kv.val.ToString();
                }
            }
        }

        void ProcessConsumedMana(DictBundle b)
        {
            // absent items are nil so... they must be reset
            foreach (var m in mana.Keys)
            {
                if (mana[m].used == 0)
                    continue;

                mana[m].used = 0;
                mana[m].block.Text = "0";
            }

            var normalMana = name + "|mana";

            if (b.dict.ContainsKey(normalMana))
            {
                b.dict.Add(name + "|mana|normal", b.dict[normalMana]);
                b.dict.Remove(normalMana);
            }
            
            string prefix = name + "|mana|";
            int len = prefix.Length;

            var q = from w in b.dict.Keys
                    where w.StartsWith(prefix)
                    select new KeyVal { key = w.Substring(len), val = ExtractInt("used:", b.dict[w]) };


            foreach (KeyVal kv in q)
            {
                if (mana.ContainsKey(kv.key))
                {
                    mana[kv.key].used = kv.val;
                    mana[kv.key].block.Text = kv.val.ToString();
                }
            }           
        }

        void ProcessWounds(DictBundle b)
        {
            // absent items are nil so... they must be reset
            foreach (var m in wounds.Keys)
            {
                if (wounds[m].used == 0)
                    continue;

                wounds[m].used = 0;
                wounds[m].block.Text = "0";
            } 
            
            int len = name.Length + 1;

            var q = from w in b.dict.Keys
                    where w.StartsWith(name + "|")
                    select new KeyVal { key = w.Substring(len), val = ExtractInt("damage:", b.dict[w]) };


            foreach (KeyVal kv in q)
            {
                if (wounds.ContainsKey(kv.key))
                {
                    wounds[kv.key].used = kv.val;
                    wounds[kv.key].block.Text = kv.val.ToString();
                }
            }           
        }

        void AddConsumptionRow(Grid g, string loc, int max, int used, string cmdBase, Dictionary<string, ConsumeableStat> dict)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            int col = 0;

            var t1 = MakeTextCell(row, col++, loc);
            var t2 = MakeTextCell(row, col++, max.ToString());
            var t3 = MakeTextCell(row, col++, used.ToString());
          
            var b1 = MakeButton(row, col++, "+");
            var b2 = MakeButton(row, col++, "-");

            if (cmdBase == "!wound")
            {
                // skip the space field
                col++;
                // these are hit locations
                var t4 = MakeTextCell(row, col++, "");
                g.Children.Add(t4);
                t4.HorizontalAlignment = HorizontalAlignment.Center;

                if (armor.ContainsKey(loc))
                    armor.Remove(loc);

                armor[loc] = t4;

                AddBuffCommand(t4, String.Format("@{0} _armor/{1}", name, loc));

                if (loc == "_life")
                {
                    AddBuffCommand(t1, String.Format("@{0} LIFE", name));
                    AddBuffCommand(t2, String.Format("@{0} LIFE", name));
                }
            }

            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t2.HorizontalAlignment = HorizontalAlignment.Center;
            t3.HorizontalAlignment = HorizontalAlignment.Center;

            g.Children.Add(t1);
            g.Children.Add(t2);
            g.Children.Add(t3);
            g.Children.Add(b1);
            g.Children.Add(b2);

            dict[loc].block = t3;

            b1.Click += (sender, e) =>
            {
                var cmd = "";
                if (cmdBase == "!mana" && loc == "normal")
                    cmd = cmdBase + " " + name + " 1";
                else                   
                    cmd = cmdBase + " " + name + " "+ loc+ " 1";

                DeferCmd(cmd);

                dict[loc].used++;
                t3.Text = dict[loc].used.ToString();
            };

            b2.Click += (sender, e) =>
            {
                var cmd = "";
                if (cmdBase == "!mana" && loc == "normal")
                    cmd = cmdBase + " " + name + " -1";
                else
                    cmd = cmdBase + " " + name + " " + loc + " -1";

                if (dict[loc].used <= 0)
                    return;

                DeferCmd(cmd);

                dict[loc].used--;
                t3.Text = dict[loc].used.ToString();
            };
        }

        void DeferSpiritManaCmd(string cmd, string effect)
        {
            // check if there is a deferred command to merge with
            if (deferredCmd != null && deferredCmd != "")
            {
                if (deferredEffect != effect)
                {
                    ExecuteDeferredCommand();
                }

                // the newer command trumps the older one
            }

            var number = ++deferredNumber;
            deferredCmd = cmd;
            deferredSchool = "";
            deferredEffect = effect;

            Main.DelayAction(600, () =>
            {
                // don't do it if the command is different than what we queued up, it's stale
                if (deferredNumber == number && cmd == deferredCmd)
                {
                    ExecuteDeferredCommand();
                }
            });
        }


        void DeferRunemagicCmd(string cmd, string effect)
        {
            // check if there is a deferred command to merge with
            if (deferredCmd != null && deferredCmd != "")
            {
                if (deferredEffect != effect)
                {
                    ExecuteDeferredCommand();
                }

                // the newer command trumps the older one
            }

            var number = ++deferredNumber;
            deferredCmd = cmd;
            deferredSchool = "";
            deferredEffect = effect;

            Main.DelayAction(600, () =>
            {
                // don't do it if the command is different than what we queued up, it's stale
                if (deferredNumber == number && cmd == deferredCmd)
                {
                    ExecuteDeferredCommand();
                }
            });
        }
        
        void DeferShugenjaCmd(string cmd, string school, string effect)
        {
            // check if there is a deferred command to merge with
            if (deferredCmd != null && deferredCmd != "")
            {
                if (deferredEffect != effect || deferredSchool != school)
                {
                    ExecuteDeferredCommand();
                }

                // the newer command trumps the older one
            }

            var number = ++deferredNumber;
            deferredCmd = cmd;
            deferredSchool = school;
            deferredEffect = effect;

            Main.DelayAction(600, () =>
            {
                // don't do it if the command is different than what we queued up, it's stale
                if (deferredNumber == number && cmd == deferredCmd)
                {
                    ExecuteDeferredCommand();
                }
            });
        }
        
        void DeferCmd(string cmd)
        {
            // check if there is a deferred command to merge with
            if (deferredCmd != null && deferredCmd != "")
            {
                string head;
                string tail;
                MainWindow.RParse2Ex(cmd, out head, out tail, ' ');

                if (head == "" || tail == "" || !deferredCmd.StartsWith(head))
                {
                    // doesn't match, run it now.
                    ExecuteDeferredCommand();
                }
                else
                {
                    var s1 = deferredCmd.Substring(head.Length + 1);
                    var s2 = tail;

                    int c1;
                    int c2;

                    if (Int32.TryParse(s1, out c1) && Int32.TryParse(s2, out c2))
                    {
                        c1 += c2;
                        cmd = String.Format("{0} {1}", head, c1);
                    }
                    else
                    {
                        // can't do the add, run it now...
                        ExecuteDeferredCommand();
                    }
                }
            }

            var number = ++deferredNumber;
            deferredCmd = cmd;

            Main.DelayAction(600, () => 
            {
                // don't do it if the command is different than what we queued up, it's stale
                if (deferredNumber == number && cmd == deferredCmd)
                {
                    ExecuteDeferredCommand();
                }
            });          
        }

        void ExecuteDeferredCommand()
        {
            if (deferredCmd != null && deferredCmd != "")
            {
                Main.SendChat(deferredCmd);
                Main.SendHost(deferredCmd);
                deferredCmd = "";
                deferredSchool = "";
                deferredEffect = "";
            }
        }

        int ExtractInt(string prefix, string data)
        {
            int val = 0;

            int index = data.IndexOf(prefix);

            if (index >= 0)
            {
                var s = data.Substring(index + prefix.Length);

                index = s.IndexOf(' ');
                if (index >= 0)
                    s = s.Substring(0, index);

                if (Int32.TryParse(s, out val))
                    return val;
            }

            return val;
        }

        void ProcessMisc(DictBundle b)
        {
            if (b.dict.ContainsKey("wizardry") && b.dict["wizardry"] != "0")
                isWizard = true;

            ProcessFatigue(b);

            var g = fSquadMode ? g6 : g1;

            AddBlankRow(g);
            AddSkillHeader(g, "Misc");
            AddMiscInfo(b, "dex_srm");
            AddMiscInfo(b, "siz_srm");
            AddMiscInfo(b, "melee_srm");
            AddMiscInfo(b, "movement");
            AddMiscInfo(b, "endurance");
            AddMiscInfo(b, "encumberence");
            AddMiscInfo(b, "fatigue");
            AddMiscInfo(b, "free_con");
            AddMiscInfo(b, "free_int");
            AddMiscInfo(b, "wizardry");
            
            AddBlankRow(g); 
            AddSkillHeader(g, "Stat Caps"); 
            AddMiscInfo(b, "MAX_STR");
            AddMiscInfo(b, "MAX_CON");
            AddMiscInfo(b, "MAX_SIZ");
            AddMiscInfo(b, "MAX_INT");
            AddMiscInfo(b, "MAX_POW");
            AddMiscInfo(b, "MAX_DEX");
            AddMiscInfo(b, "MAX_APP");

            AddBlankRow(g); 
            AddSkillHeader(g, "Bonuses");
            AddMiscInfo(b, "magic");
            AddMiscInfo(b, "perception");
            AddMiscInfo(b, "agility");
            AddMiscInfo(b, "manipulation");
            AddMiscInfo(b, "stealth");
            AddMiscInfo(b, "communication");
            AddMiscInfo(b, "knowledge");
            AddMiscInfo(b, "alchemy");
            AddMiscInfo(b, "attack");
            AddMiscInfo(b, "parry");

            AddInformativeStat(gTop, "Name", name, System.Windows.HorizontalAlignment.Left, bold: true);

            if (!fSquadMode)
            {
                AddTopInfo(b, "species");
                AddTopInfo(b, "religion");
            }

            AddBlankRow(gTop);

            if (!fSquadMode)
            {
                var results = MainWindow.mainWindow.partyInfo1.FindDossierDetails(name);

                if (results.Count > 0)
                    AddBlankRow(gBottom);

                foreach (var result in results)
                {
                    string head, tail;
                    MainWindow.Parse2Ex(result, out head, out tail, ':');
                    tail = tail.Trim();

                    if (tail != "")
                        AddInformativeStat(gBottom, head, tail, System.Windows.HorizontalAlignment.Left, bold: true);
                    else
                        AddInformativeStat(gBottom, "detail", result, System.Windows.HorizontalAlignment.Left, bold: true);
                }
            }
        }

        void AddMiscInfo(DictBundle b, string key)
        {
            var g = fSquadMode ? g6 : g1;

            if (b.dict.ContainsKey(key))
            {
                AddInformativeStat(g, key, b.dict[key], System.Windows.HorizontalAlignment.Right, bold: false);
            }
        }

        void AddTopInfo(DictBundle b, string key)
        {
            if (b.dict.ContainsKey(key))
            {
                AddInformativeStat(gTop, key, b.dict[key], System.Windows.HorizontalAlignment.Left, bold:true);
            }
        }

        void ProcessFatigue(DictBundle b)
        {
            fatigue = new Dictionary<string, ConsumeableStat>();

            var cs = new ConsumeableStat();
            cs.name = "normal";

            int val = 0;
            if (b.dict.ContainsKey("fatigue"))
                Int32.TryParse(b.dict["fatigue"], out val);
            cs.max = val;

            fatigue.Add("normal", cs);

            cs = new ConsumeableStat();
            cs.name = "hard";
            cs.max = val;

            fatigue.Add("hard", cs);

            AddHeaderRow(g5, "", "", "", "");
            AddHeaderRow(g5, "Fatigue Type", "Full", "Used", "");

            ProcessConsumable(b, fatigue, "!fatigue");
        }

        void ProcessHitLocations(DictBundle b)
        {
            wounds = new Dictionary<string, ConsumeableStat>();
            armor = new Dictionary<string, TextBlock>();

            AddHeaderRow(g5, "", "", "", "");
            AddHeaderRow(g5, "Location", "Full", "Wounds", "Armor");

            // get the last added child
            var t1 = (FrameworkElement)g5.Children[g5.Children.Count - 1];

            AddBuffCommand(t1, String.Format("@{0} PROT", name));

            ProcessConsumable(b, wounds, "!wound");
        }

        void ProcessConsumable(DictBundle b, Dictionary<string, ConsumeableStat> consumable, string cmdBase)
        {
            if (consumable.Count == 0)
            {
                // fatigue comes in pre-populated, all others need to be computed
                foreach (var k in b.dict.Keys)
                {
                    int val = 0;

                    Int32.TryParse(b.dict[k], out val);

                    var cs = new ConsumeableStat();
                    cs.name = k;
                    cs.max = val;

                    consumable.Add(k, cs);
                }
            }

            var q2 = from w in consumable.Keys
                     orderby w ascending
                     select w;

            foreach (var loc in q2)
            {
                var w = consumable[loc];
                AddConsumptionRow(g5, loc, w.max, w.used, cmdBase, consumable);
            } 
        }

        void ProcessMana(DictBundle b)
        {
            mana = new Dictionary<string, ConsumeableStat>();

            AddHeaderRow(g5, "", "", "", "");
            AddHeaderRow(g5, "Mana Type", "Full", "Used", "");

            if (b.dict.ContainsKey("mpts_per_day"))
                b.dict.Remove("mpts_per_day");

            if (b.dict.ContainsKey("total_magic_points"))
            {
                b.dict.Add("normal", b.dict["total_magic_points"]);
                b.dict.Remove("total_magic_points");
            }

            ProcessConsumable(b, mana, "!mana");
        }    

        void ProcessWeapons()
        {
            Main.SendHost(String.Format("dir _wounds"));
            Main.SendHost(String.Format("dir _mana"));
            Main.SendHost(String.Format("dir _shugenja"));
            Main.SendHost(String.Format("dir _runemagic"));
            Main.SendHost(String.Format("dir _spiritmana"));
            Main.SendHost(String.Format("dir _checks"));
            Main.SendHost(String.Format("dir _fatigue"));

            var q = from w in weapons
                    orderby w.attack + w.parry descending
                    select w;

            if (fSquadMode)
                AddBlankRow(g4);

            AddWeaponHeaderRow(g4, "Name", "Attack", "Parry ", "Damage", "AP", "SR");

            foreach (var w in q)
            {
                AddWeaponRow(g4, w.name, w.attack.ToString(), w.parry.ToString(), w.dmg, w.ap.ToString(), w.sr.ToString());
            }

            FadeIn();
        }

        void AddBlankRow(Grid g)
        {
            int row = g.RowDefinitions.Count;

            g.RowDefinitions.Add(new RowDefinition());

            var c = new Canvas();
            c.Height = 20;
            c.Width = 20;
            c.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(c, row);
            Grid.SetColumn(c, 0);

            g.Children.Add(c);
        }

        void ProcessOthersSpells(DictBundle bundle)
        {
            var q = from k in bundle.dict.Keys
                    where bundle.dict[k] != "<Folder>"
                    orderby k ascending
                    select k;

            var grid = g3;

            string whoPrev = "";

            foreach (var k in q)
            {
                string who, what;
                MainWindow.Parse2Ex(k, out who, out what, ';');

                if (whoPrev != who)
                {
                    whoPrev = who;
                    AddBlankRow(grid);
                    AddSkillHeader(grid, who);
                }

                AddOtherCasterRow(bundle, grid, k, who, what);
            }
        }

        void ProcessSpiritInventory(DictBundle bundle)
        {

            var q = from k in bundle.dict.Keys
                    where bundle.dict[k] != "<Folder>"
                    orderby k ascending
                    select k;

            var grid = g5;

            bool header = false;

            foreach (var k in q)
            {
                if (!header)
                {
                    AddHeaderRow(g5, "", "", "", "");
                    AddHeaderRow(g5, "Spirit Inventory", "POW", "Used", "SC%");
                    
                    header = true;
                } 

                AddSpiritRow(bundle, grid, k);
            }
        }


        void StandardSkillClass(DictBundle bundle, string path, Grid grid, bool addBlank)
        {
            if (fSquadMode) addBlank = true;

            SkillType skillType = SkillType.Non_Checkable_Skill;
            
            switch (path)
            {
                case "agility":
                case "manipulation":
                case "stealth":
                case "perception":
                case "special":
                case "magic":
                case "knowledge":
                case "alchemy":
                case "communication":
                case "_herocast":
                case "_wizardry":
                    skillType = SkillType.Checkable_Skill;
                    break;

                case "_music":
                case "_battlemagic":
                    skillType = SkillType.Non_Checkable_Skill;
                    break;

                case "_spells":
                case "_stored_spells":
                    skillType = (isSorceror || isWizard) ?  SkillType.Checkable_Skill:  SkillType.Non_Checkable_Skill;
                    break;

                case "_runemagic":
                    skillType = SkillType.Runemagic;
                    break;

                case "_one_use":
                    skillType = SkillType.OneUse_Runemagic;
                    break;
            }


            if (path.EndsWith("_school"))
                skillType = SkillType.Shugenja;


            var q = from k in bundle.dict.Keys
                    where bundle.dict[k] != "<Folder>"
                    orderby SortTrans(skillType, k) ascending
                    select k;

            bool header = false;           

            foreach (var k in q)
            {
                if (!header)
                {
                    if (addBlank)
                        AddBlankRow(grid);

                    AddSkillHeader(grid, path);
                    header = true;
                }

                AddRow(bundle, grid, k, skillType);
            }
        }

        string SortTrans(SkillType spellType, string k)
        {
            if (spellType != SkillType.Shugenja)
                return k;

            string head, tail;
            MainWindow.RParse2Ex(k, out head, out tail, '_');

            return tail + "_" + head;
        }

        void AddInformativeStat(Grid g, string key, string value, HorizontalAlignment align, bool bold)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            while (key.StartsWith("_"))
                key = key.Substring(1);

            key = key.Substring(0, 1).ToUpper() + key.Substring(1);
            key = key.Replace("_", " ") + ":";

            var t1 = new TextBlock();
            t1.Text = key;
            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t1.VerticalAlignment = VerticalAlignment.Stretch;
            if (bold) t1.FontWeight = FontWeights.Bold;
            t1.Padding = new Thickness(0, 0, 15, 0);

            Grid.SetRow(t1, row);
            Grid.SetColumn(t1, 1);
            g.Children.Add(t1);

            var t2 = new TextBlock();
            t2.Text = value;
            t2.HorizontalAlignment = align;
            t2.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(t2, row);
            Grid.SetColumn(t2, 2);
            g.Children.Add(t2);
        }
        
        void AddSkillHeader(Grid g, string key)
        {
            int row = g.RowDefinitions.Count;

            g.RowDefinitions.Add(new RowDefinition());

            while (key.StartsWith("_"))
                key = key.Substring(1);

            key = key.Substring(0, 1).ToUpper() + key.Substring(1);
            key = key.Replace("_", " ") +":";

            var t1 = new TextBlock();
            t1.Text = key;
            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t1.VerticalAlignment = VerticalAlignment.Stretch;
            t1.FontWeight = FontWeights.Bold;
            Grid.SetRow(t1, row);
            Grid.SetColumn(t1, 0);
            Grid.SetColumnSpan(t1, 2);
            g.Children.Add(t1);
        }


        void AddOtherCasterRow(DictBundle b, Grid g, string key, string who, string what)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            var keyText = new TextBlock();
            keyText.Text = ProcessKey(what);
            keyText.HorizontalAlignment = HorizontalAlignment.Left;
            keyText.VerticalAlignment = VerticalAlignment.Center;
            keyText.Margin = new Thickness(0, 0, 5, 0);

            StackPanel sp = null;

            sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            Grid.SetRow(sp, row);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);
            sp.Children.Add(keyText);

            Button btn = new Button();
            btn.MinHeight = 5;
            var btnText = new TextBlock();
            btn.Content = btnText;
            btnText.Text = CellString(b, key);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, 2);
            g.Children.Add(btn);

            string saveKey = MakeSaveKey(b.path, key);
          
            btn.Click += (sender, e) =>
            {
                var skill = who + ";" + what;
                var value = btnText.Text;

                if (fTrainingMode)
                {
                    var item = "/" + key + "/";
                    Train(item);
                    return;
                }

                var skillStrip = new SkillStrip();
                skillStrip.Init(name: name, skill: skill, sr: null, skillPct: value, note: null);
                Main.readyRolls.AppendStripContents(name, skillStrip.Children, saveKey);
            };
        }

        void AddSpiritRow(DictBundle b, Grid g, string key)
        {
            if (g.ColumnDefinitions.Count < 6)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition());
            } 
            
            int col = 0;
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            var skillKey = b.path.Replace("/", "|") + "|" + key;
            skillKey = skillKey.Replace("|_spirits|", "|");

            SpiritInfo spiritInfo = null;

            spiritInfo = new SpiritInfo();
            spirits.Add(skillKey, spiritInfo);
            spiritInfo.key = skillKey;

            var value = CellString(b, key);

            string ctr;
            string sc;
            string pow;
            string stored;
            MainWindow.Parse2(value, out sc, out ctr);
            MainWindow.Parse2(ctr, out pow, out stored);

            pow = pow.Substring(4);
            sc = sc.Substring(3);
            stored = stored.Substring(7);

            var keyText = MakeTextCell(row, col++, String.Format("{0} in {1}", ProcessKey(key), stored));
            keyText.VerticalAlignment = VerticalAlignment.Center;
            
            var tx2 = MakeTextCell(row, col++, pow);
            tx2.Margin = new Thickness(2, 0, 0, 0);
            tx2.HorizontalAlignment = HorizontalAlignment.Center;
            tx2.VerticalAlignment = VerticalAlignment.Center;

            // add mana and +/- buttons
            var tx = MakeTextCell(row, col++, "0");
            tx.Margin = new Thickness(15, 0, 0, 0);
            tx.HorizontalAlignment = HorizontalAlignment.Center;
            tx.VerticalAlignment = VerticalAlignment.Center;

            var b1 = MakeButton(row, col++, "+");
            b1.VerticalAlignment = VerticalAlignment.Center;
            var b2 = MakeButton(row, col++, "-");
            b2.VerticalAlignment = VerticalAlignment.Center;

            col += 3;
            var scText = MakeTextCell(row, col++, sc + "%");
            scText.Margin = new Thickness(0, 0, 0, 0);
            scText.HorizontalAlignment = HorizontalAlignment.Center;
            scText.VerticalAlignment = VerticalAlignment.Center;

            g.Children.Add(keyText);
            g.Children.Add(tx);
            g.Children.Add(tx2);
            g.Children.Add(b1);
            g.Children.Add(b2);
            g.Children.Add(scText);

            spiritInfo.used = tx;

            b1.Click += (sender, e) => { IncrementSpiritManaUsed(spiritInfo); };
            b2.Click += (sender, e) => { DecrementSpiritManaUsed(spiritInfo); };

        }

        void AddRow(DictBundle b, Grid g, string key, SkillType skillType)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            var skillKey = b.path.Replace("/", "|") + "|" + key;

            CheckBox cb = null;

            ShugenjaInfo shugenjaInfo = null;

            if (skillType == SkillType.Shugenja)
            {
                shugenjaInfo = new ShugenjaInfo();
                shugenja.Add(skillKey, shugenjaInfo);
                shugenjaInfo.key = skillKey;
            }

            RunemagicInfo runemagicInfo = null;

            if (skillType == SkillType.Runemagic)
            {
                skillKey = skillKey.Replace("|_runemagic|", "|");
                runemagicInfo = new RunemagicInfo();
                runemagic.Add(skillKey, runemagicInfo);
                runemagicInfo.key = skillKey;
            }

            if (skillType == SkillType.OneUse_Runemagic)
            {
                skillKey = skillKey.Replace("|_one_use|", "|one_use_");
                runemagicInfo = new RunemagicInfo();
                runemagic.Add(skillKey, runemagicInfo);
                runemagicInfo.key = skillKey;
            }

            CheckableSkill checkableSkill = null;

            if (skillType == SkillType.Checkable_Skill || skillType == SkillType.Stat)
            {
                cb = new CheckBox();
                Grid.SetRow(cb, row);
                Grid.SetColumn(cb, 0);
                g.Children.Add(cb);
                cb.VerticalAlignment = VerticalAlignment.Center;

                checkableSkill = new CheckableSkill();
                checkableSkill.check = cb;
                checkableSkill.key = b.path.Replace("/", "|") + "|" + key;              

                checks[skillKey] = checkableSkill;

                cb.Click += (sender, e) =>
                {
                    int ibreak = checkableSkill.key.IndexOf('|');
                    if (ibreak < 0)
                        return;

                    var item = checkableSkill.key.Substring(ibreak + 1);
                    item = item.Replace("|", "/");

                    var label = String.Format("check {0} /{1}/", name, item);
                    if (cb.IsChecked != true)
                        label = label + " remove:yes";

                    var cmd = "!" + label;
                    Main.SendChat(label);
                    Main.SendHost(cmd);
                };
            }

            var keyText = new TextBlock();
            keyText.Text = ProcessKey(key);
            keyText.HorizontalAlignment = HorizontalAlignment.Left;
            keyText.VerticalAlignment = VerticalAlignment.Center;
            keyText.Margin = new Thickness(0, 0, 5, 0);

            if (checkableSkill != null)
                checkableSkill.text = keyText;

            StackPanel sp = null;

            if (skillType != SkillType.Stat)
            {
                Grid.SetRow(keyText, row);
                Grid.SetColumn(keyText, 1);
                g.Children.Add(keyText);

                MakeHelpRightKey(keyText, b.path + "/" + key, name);
            }
            else
            {
                sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Grid.SetRow(sp, row);
                Grid.SetColumn(sp, 1);
                g.Children.Add(sp);
                sp.Children.Add(keyText);

            }

            Button btn = new Button();
            btn.MinHeight = 5;
            var btnText = new TextBlock();
            btn.Content = btnText;
            btnText.Text = CellString(b, key);
            btn.VerticalAlignment = VerticalAlignment.Center;

            if (skillType == SkillType.Stat)
            {
                AddBuffCommand(btn, String.Format("@{0} {1}", name, key));
            }
            else
            {
                var path = StripNameFromKey(b.path + "/" + key, name);
                AddBuffCommand(btn, String.Format("@{0} {1}", name, path));
            }

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, 2);
            g.Children.Add(btn);

            if (skillType == SkillType.Shugenja)
            {
                if (g.ColumnDefinitions.Count < 5)
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                }

                // add charges and +/- buttons
                int col = 3;
                var tx = MakeTextCell(row, col++, "0");
                tx.Margin = new Thickness(15, 0, 0, 0);
                tx.VerticalAlignment = VerticalAlignment.Center;

                var b1 = MakeButton(row, col++, "+");
                b1.VerticalAlignment = VerticalAlignment.Center;
                var b2 = MakeButton(row, col++, "-");
                b2.VerticalAlignment = VerticalAlignment.Center;

                g.Children.Add(tx);
                g.Children.Add(b1);
                g.Children.Add(b2);

                shugenjaInfo.charges = tx;

                b1.Click += (sender, e) => { IncrementCharges(shugenjaInfo); };
                b2.Click += (sender, e) => { DecrementCharges(shugenjaInfo); };
            }


            if (skillType == SkillType.Runemagic || skillType == SkillType.OneUse_Runemagic)
            {
                if (g.ColumnDefinitions.Count < 6)
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                }

                // add charges and +/- buttons
                int col = 3;
                var tx = MakeTextCell(row, col++, "0");
                tx.Margin = new Thickness(15, 0, 0, 0);
                tx.VerticalAlignment = VerticalAlignment.Center;

                var tx2 = MakeTextCell(row, col++, "of " + btnText.Text);
                tx2.Margin = new Thickness(2, 0, 0, 0);
                tx2.VerticalAlignment = VerticalAlignment.Center;

                var b1 = MakeButton(row, col++, "+");
                b1.VerticalAlignment = VerticalAlignment.Center;
                var b2 = MakeButton(row, col++, "-");
                b2.VerticalAlignment = VerticalAlignment.Center;

                g.Children.Add(tx);
                g.Children.Add(tx2);
                g.Children.Add(b1);
                g.Children.Add(b2);

                runemagicInfo.used = tx;
                btnText.Text = "95";

                if (tx2.Text == "of 95") tx2.Text = "of ?";

                b1.Click += (sender, e) => { IncrementRunemagicUsed(runemagicInfo); };
                b2.Click += (sender, e) => { DecrementRunemagicUsed(runemagicInfo); };
            }

            string saveKey = MakeSaveKey(b.path, key);

            if (skillType == SkillType.Stat)
            {
                string mult = "*5";
                switch (keyText.Text)
                {
                    case "INT":
                    case "POW":
                    case "CON":
                    case "SIZ":
                    case "APP":
                    case "STR":
                        mult = "*1";
                        break;
                }

                keyText.MinWidth = 30;
                    
                Button btnStatMult = new Button();
                btnStatMult.MinHeight = 5;
                btnStatMult.MinWidth = 50;
                btnStatMult.Margin = new Thickness(5, 0, 0, 0);
                var tStatMult = new TextBlock();
                btnStatMult.Content = tStatMult;
                tStatMult.Text = keyText.Text + mult;
                sp.Children.Add(btnStatMult);
                btnStatMult.VerticalAlignment = VerticalAlignment.Center;

                btnStatMult.Click += (sender, e) =>
                {
                    var stat = keyText.Text;
                    var value = btnText.Text;

                    if (fTrainingMode)
                    {
                        return;
                    }

                    var powStrip = new PowStrip();
                    powStrip.Init(name: name, stat: stat + " test", value: value + mult);
                    Main.readyRolls.AppendStripContents(name, powStrip.Children, saveKey);
                };

                btn.Click += (sender, e) =>
                {
                    var stat = keyText.Text;
                    var value = btnText.Text;

                    if (fTrainingMode)
                    {
                        var item = "/" + key + "/";
                        Train(item);
                        return;
                    }

                    var powStrip = new PowStrip();
                    powStrip.Init(name: name, stat: stat + " vs. " + stat, value: value);
                    Main.readyRolls.AppendStripContents(name, powStrip.Children, saveKey);

                };
            }
            else
            {
                btn.Click += (sender, e) =>
                {
                    var skill = keyText.Text;
                    var value = btnText.Text;

                    if (fTrainingMode)
                    {
                        var item = "/" + key + "/";
                        Train(item);
                        return;
                    }

                    var skillStrip = new SkillStrip();
                    skillStrip.Init(name: name, skill: skill, sr: null, skillPct: value, note: null);
                    Main.readyRolls.AppendStripContents(name, skillStrip.Children, saveKey);
                };
            }
        }

        void IncrementCharges(ShugenjaInfo shugenjaInfo)
        {
            string who;
            int nPts;
            int nCharges;
            string school;
            string effect;

            if (!TryGetShugenjaParts(shugenjaInfo, out who, out nPts, out school, out effect, out nCharges))
                return;

            nCharges++;

            if (nCharges < 1 || nCharges > 3)
                return;

            var id = MainWindow.mainWindow.GetPartyInfo().FindShugenjaId(who, school, effect);

            if (id == "")
            {
                DeferShugenjaCmd(String.Format("!shugenja {0} {1} school:{2} cost:{3} charges:{4}", who, effect, school, nPts*nPts+1, nCharges), school, effect);
            }
            else
            {
                DeferShugenjaCmd(String.Format("!shugenja {0} charges:{1} spell:{2}", who, nCharges, id), school, effect);
            }

            shugenjaInfo.charges.Text = nCharges.ToString();
        }


        void IncrementSpiritManaUsed(SpiritInfo spiritInfo)
        {
            string who;
            string effect;
            int nUsed;

            if (!TryGetSpiritManaParts(spiritInfo, out who, out effect, out nUsed))
                return;

            nUsed++;

            DeferSpiritManaCmd(String.Format("!spiritmana {0} {1} used:{2}", who, effect, nUsed), effect);

            spiritInfo.used.Text = nUsed.ToString();
        }

        void DecrementSpiritManaUsed(SpiritInfo spiritInfo)
        {

            string who;
            string effect;
            int nUsed;

            if (!TryGetSpiritManaParts(spiritInfo, out who, out effect, out nUsed))
                return;

            nUsed--;

            if (nUsed < 0)
                return;

            DeferSpiritManaCmd(String.Format("!spiritmana {0} {1} used:{2}", who, effect, nUsed), effect);

            spiritInfo.used.Text = nUsed.ToString();
        }

        void IncrementRunemagicUsed(RunemagicInfo runemagicInfo)
        {
            string who;
            string effect;
            int nUsed;

            if (!TryGetRunemagicParts(runemagicInfo, out who, out effect, out nUsed))
                return;

            nUsed++;

            DeferRunemagicCmd(String.Format("!runemagic {0} {1} used:{2}", who, effect, nUsed), effect);

            runemagicInfo.used.Text = nUsed.ToString();
        }

        void DecrementRunemagicUsed(RunemagicInfo runemagicInfo)
        {

            string who;
            string effect;
            int nUsed;

            if (!TryGetRunemagicParts(runemagicInfo, out who, out effect, out nUsed))
                return;

            nUsed--;

            if (nUsed < 0)
                return;

            DeferRunemagicCmd(String.Format("!runemagic {0} {1} used:{2}", who, effect, nUsed), effect);

            runemagicInfo.used.Text = nUsed.ToString();
        }


        static bool TryGetSpiritManaParts(SpiritInfo spiritInfo, out string who, out string spiritId, out int nUsed)
        {
            // e.g. of key "Arcastar_Epos|spell"

            who = "";
            spiritId = "";
            nUsed = 0;

            MainWindow.Parse2Ex(spiritInfo.key, out who, out spiritId, '|');

            if (who == "" || spiritId == "")
                return false;

            Int32.TryParse(spiritInfo.used.Text, out nUsed); // "" is ok for zero                

            return true;
        }
        
        
        static bool TryGetRunemagicParts(RunemagicInfo runemagicInfo, out string who, out string spell, out int nUsed)
        {
            // e.g. of key "Arcastar_Epos|spell"

            who = "";
            spell = "";
            nUsed  = 0;

            MainWindow.Parse2Ex(runemagicInfo.key, out who, out spell, '|');

            if (who == "" || spell == "")
                return false;

            Int32.TryParse(runemagicInfo.used.Text, out nUsed); // "" is ok for zero                

            return true;
        }
        
        static bool TryGetShugenjaParts(ShugenjaInfo shugenjaInfo, out string who, out int nPts, out string school, out string spell, out int nCharges)
        {
            // e.g. of key "Tamori_Sanzo|_air_school|air_power_0"

            who = "";
            spell = "";
            school = "";
            nPts = 0;
            nCharges = 0;

            string schoolAndSpell;
            MainWindow.Parse2Ex(shugenjaInfo.key, out who, out schoolAndSpell, '|');
            MainWindow.Parse2Ex(schoolAndSpell, out school, out spell, '|');

            if (who == "" || school == "" || spell == "")
                return false;

            string pts;
            string basename;
            MainWindow.RParse2Ex(spell, out basename, out pts, '_');

            if (!Int32.TryParse(pts, out nPts))
                return false;

            Int32.TryParse(shugenjaInfo.charges.Text, out nCharges); // "" is ok for zero                

            school = school.Substring(1, school.Length - 8); // strip leading _ and trailing _school

            return true;
        } 

        void DecrementCharges(ShugenjaInfo shugenjaInfo)
        {
            string who;
            int nPts;
            int nCharges;
            string school;
            string effect;

            if (!TryGetShugenjaParts(shugenjaInfo, out who, out nPts, out school, out effect, out nCharges))
                return;

            nCharges--;

            if (nCharges < 0 || nCharges > 2)
                return;


            var id = MainWindow.mainWindow.GetPartyInfo().FindShugenjaId(who, school, effect);

            if (id == "")
                return;

            if (nCharges == 0)
            {
                DeferShugenjaCmd(String.Format("!shugenja {0} spell:{1} remove:yes", who, id), school, effect);
                shugenjaInfo.charges.Text = "";
            }
            else
            {
                DeferShugenjaCmd(String.Format("!shugenja {0} charges:{1} spell:{2}", who, nCharges, id), school, effect);
                shugenjaInfo.charges.Text = nCharges.ToString();
            }
        }


        public static string StripNameFromKey(string key, string name)
        {
            if (key.Length > name.Length + 1)
            {
                key = key.Substring(name.Length + 1);
            }

            return key;
        }
            
        public static string MakeHelpRightKey(TextBlock textBlock, string key, string name)
        {
            if (key.Length > name.Length + 1)
            {
                key = key.Substring(name.Length + 1);

                if (key.EndsWith("_0") || key.EndsWith("_1") || key.EndsWith("_2") || key.EndsWith("_3") || key.EndsWith("_4") ||
                    key.EndsWith("_5") || key.EndsWith("_6") || key.EndsWith("_7") || key.EndsWith("_8") || key.EndsWith("_9"))
                    key = key.Substring(0, key.Length - 2);

                if (key.EndsWith("_10") || key.EndsWith("_11") || key.EndsWith("_12") || key.EndsWith("_13") || key.EndsWith("_14") ||
                    key.EndsWith("_15") || key.EndsWith("_16") || key.EndsWith("_17") || key.EndsWith("_18") || key.EndsWith("_19"))
                    key = key.Substring(0, key.Length - 3);

                textBlock.MouseRightButtonUp += (sender, e) =>
                {
                    Main.SendHost("help " + key);
                };
            }

            return key;
        }

        string MakeSaveKey(string p, string key)
        {
            return "\\"+p.Replace('/', '\\') + "\\" + key;
        }

        void Train(string item)
        {
            string sessionHours = textSessionHours.Text;
            string gain = textGain.Text;
            string bkPct = bookPct.Text;
            string bkGain = bookGain.Text;

            string stopVal = textLimit.Text;

            string stop = "";

            if (limCount.IsChecked == true)
                stop = "count:";
            else if (limHours.IsChecked == true)
                stop = "hours:";
            else
                stop = "skill:";

            string cmd = String.Format("!train {0} session:{1} {2}{3} {4}", name, sessionHours, stop, stopVal, item);

            if (gain != "")
                cmd = cmd + " gain:" + gain;

            if (bkPct != "")
                cmd = cmd + " book:"+bkPct;

            if (bkGain != "")
                cmd = cmd + " bookgain:" + bkGain;

            Main.SendChat(".");
            Main.SendHost(cmd);
            Main.SendHost("remaining "+name);
        }

        void AddWeaponHeaderRow(Grid g, string k1, string k2, string k3, string k4, string k5, string k6)
        {
            int row = g.RowDefinitions.Count;

            g.RowDefinitions.Add(new RowDefinition());

            int col = 0;

            var t1 = MakeTextCell(row, col++, k1);
            var t2 = MakeTextCell(row, col++, k2);
            col++;
            var t3 = MakeTextCell(row, col++, k3);
            col++;
            var t4 = MakeTextCell(row, col++, k4);
            var t5 = MakeTextCell(row, col++, k5);
            var t6 = MakeTextCell(row, col++, k6);

            t1.FontWeight = FontWeights.Bold;
            t2.FontWeight = FontWeights.Bold;
            t3.FontWeight = FontWeights.Bold;
            t4.FontWeight = FontWeights.Bold;
            t5.FontWeight = FontWeights.Bold;
            t6.FontWeight = FontWeights.Bold;

            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t2.HorizontalAlignment = HorizontalAlignment.Center;
            t3.HorizontalAlignment = HorizontalAlignment.Center;
            t4.HorizontalAlignment = HorizontalAlignment.Left;
            t5.HorizontalAlignment = HorizontalAlignment.Center;
            t6.HorizontalAlignment = HorizontalAlignment.Center;

            g.Children.Add(t1);
            g.Children.Add(t2);
            g.Children.Add(t3);
            g.Children.Add(t4);
            g.Children.Add(t5);
            g.Children.Add(t6);
        }
        

        void AddHeaderRow(Grid g, string k1, string k2, string k3, string k4)
        {
            int row = g.RowDefinitions.Count;

            g.RowDefinitions.Add(new RowDefinition());

            int col = 0;

            var t1 = MakeTextCell(row, col++, k1);
            var t2 = MakeTextCell(row, col++, k2);
            var t3 = MakeTextCell(row, col++, k3);
            
            col++;
            col++;
            col++;

            var t4 = MakeTextCell(row, col++, k4);

            t1.FontWeight = FontWeights.Bold;
            t2.FontWeight = FontWeights.Bold;
            t3.FontWeight = FontWeights.Bold;
            t4.FontWeight = FontWeights.Bold;

            t4.Margin = new Thickness(5, 0, 5, 0);

            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t2.HorizontalAlignment = HorizontalAlignment.Center;
            t3.HorizontalAlignment = HorizontalAlignment.Center;
            t4.HorizontalAlignment = HorizontalAlignment.Center;

            g.Children.Add(t1);
            g.Children.Add(t2);
            g.Children.Add(t3);
            g.Children.Add(t4);
        }
        
        void AddWeaponRow(Grid g, string wpn, string pct, string parry, string dmg, string ap, string sr)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition());

            int col = 0;

            var t1 = MakeTextCell(row, col++, wpn);
            var b1 = MakeButton(row, col++, pct);
            var c1 = WeaponCheck(row, col++, wpn, "attack");
            var b2 = MakeButton(row, col++, parry);
            var c2 = WeaponCheck(row, col++, wpn, "parry");
            var t4 = MakeTextCell(row, col++, dmg);
            var t5 = MakeTextCell(row, col++, ap);
            var t6 = MakeTextCell(row, col++, sr);

            c1.text = b1.Content as TextBlock;
            c2.text = b2.Content as TextBlock;
            
            AddBuffCommand(b1, String.Format("@{0} _wpn/{1}/{2}", name, wpn, "attack"));
            AddBuffCommand(b2, String.Format("@{0} _wpn/{1}/{2}", name, wpn, "parry"));
            
            AddBuffCommand(t4, String.Format("@{0} _wpn/{1}/{2}", name, wpn, "dmg"));
            AddBuffCommand(t5, String.Format("@{0} _wpn/{1}/{2}", name, wpn, "ap"));
            AddBuffCommand(t6, String.Format("@{0} _wpn/{1}/{2}", name, wpn, "sr"));

            t4.HorizontalAlignment = HorizontalAlignment.Left;
            t5.HorizontalAlignment = HorizontalAlignment.Center;
            t6.HorizontalAlignment = HorizontalAlignment.Center;

            g.Children.Add(t1);
            g.Children.Add(b1);
            g.Children.Add(b2);
            g.Children.Add(t4);
            g.Children.Add(t5);
            g.Children.Add(t6);
            g.Children.Add(c1.check);
            g.Children.Add(c2.check);

            b1.Click += (sender, e) =>
            {
                if (fTrainingMode)
                {
                    var item = "/" + wpn + "/attack";
                    Train(item);
                }
                else
                {
                    string saveKey = MakeSaveKey(name + "/_wpn/" + wpn, "attack");

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                    Main.readyRolls.AppendStripContents(name, attackStrip.Children, saveKey);
                }
            };

            b2.Click += (sender, e) =>
            {
                if (fTrainingMode)
                {
                    var item = "/" + wpn + "/parry";
                    Train(item);
                }
                else
                {
                    string saveKey = MakeSaveKey(name + "/_wpn/" + wpn, "parry");

                    var parryStrip = new ParryStrip();
                    parryStrip.Init(name, parryChoice: wpn, ap: ap, parryPct: parry);
                    Main.readyRolls.AppendStripContents(name, parryStrip.Children, saveKey);
                }
            };
        }

        void AddBuffCommand(FrameworkElement el, string args)
        {
            if (el.ContextMenu == null)
            {
                el.ContextMenu = new ContextMenu();
            }

            var m = el.ContextMenu;

            var buff = new MenuItem();
            m.Items.Add(buff);

            buff.Header = "Buff " + args;
            buff.Click += (sender, e) =>
            {
                ManyKey dlg = new ManyKey("Buff " + args, "Amount:", "");
                if (dlg.ShowDialog() == true)
                {
                    string v = dlg.Results[0];
                    if (!v.StartsWith("+") && !v.StartsWith("-"))
                        v = "+" + v;

                    var msg = String.Format("buff {0}{1}", args, v);
                    var cmd = "!" + msg;
                    Main.SendChat(msg);
                    Main.SendHost(cmd);
                }
            };

            var unbuff = new MenuItem();
            m.Items.Add(unbuff);
            unbuff.Header = "Unbuff " + args;
            unbuff.Click += (sender, e) =>
            {
                var msg = String.Format("buff {0} remove:yes", args);
                var cmd = "!" + msg;
                Main.SendChat(msg);
                Main.SendHost(cmd);
            };
        }

        CheckableSkill WeaponCheck(int row, int col, string wpn, string skillName)
        {
            var cb = new CheckBox();
            Grid.SetRow(cb, row);
            Grid.SetColumn(cb, col);
            cb.VerticalAlignment = VerticalAlignment.Center;
            cb.Margin = new Thickness(5, 0, 0, 0);

            var skill = new CheckableSkill();
            skill.key = String.Format("{0}|_wpn|{1}|{2}", name, wpn, skillName);
            skill.check = cb;

            if (checks.ContainsKey(skill.key))
                checks.Remove(skill.key);

            checks.Add(skill.key, skill);

            cb.Click += (sender, e) =>
            {
                int ibreak = skill.key.IndexOf('|');
                if (ibreak < 0)
                    return;

                var item = skill.key.Substring(ibreak + 1);
                item = item.Replace("|", "/");

                var label = String.Format("check {0} /{1}/", name, item);
                if (cb.IsChecked != true)
                    label = label + " remove:yes";

                var cmd = "!" + label;
                Main.SendChat(label);
                Main.SendHost(cmd);
            };

            return skill;
        }

        Button MakeButton(int row, int col, string text)
        {
            var t1 = new TextBlock();
            t1.Text = text;
            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t1.VerticalAlignment = VerticalAlignment.Stretch;

            Button btn = new Button();
            btn.Margin = new Thickness(5, 0, 0, 0);
            btn.MinHeight = 5;
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            btn.Content = t1;
            return btn;
        }

        TextBlock MakeTextCell(int row, int col, string text)
        {
            var t1 = new TextBlock();
            t1.Text = text;
            t1.Margin = new Thickness(5, 0, 0, 0);
            t1.HorizontalAlignment = HorizontalAlignment.Left;
            t1.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(t1, row);
            Grid.SetColumn(t1, col);
            return t1;
        }
        
        string ProcessKey(string key)
        {
            while (key.Contains("__"))
            {
                key = key.Replace("__", "_");
            }

            key = key.Replace("@", "");

            if (key.EndsWith("_"))
                key = key.Substring(0, key.Length - 1);

            return key;
        }

        string GetComboSelectionAsString(ComboBox combo)
        {
            if (combo == null)
                return "";

            if (combo.SelectedIndex == -1)
                return "";

            var o = combo.SelectedItem;

            var s = o as string;

            if (s != null)
                return s;

            return "";
        }

        void comboGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var player = GetComboSelectionAsString(comboPlayers);
            if (player == "")
                return;

            var group = GetComboSelectionAsString(comboGroup);

            if (player == "*Party")
            {
                if (group == "*Active")
                    partyPath = partyPathActiveParty;
                else
                    partyPath = "_party/" + group;

                if (partyPath != null && partyPath != "")
                {
                    Main.SendHost(String.Format("dir {0}", partyPath));
                }
            }
            else
            {
                var s = player;

                if (group != "" && group != "*Ungrouped")
                    s = player + "_" + group;

                SetCharactersDropDown(s);
            }
        }

        void comboPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = GetComboSelectionAsString(comboPlayers);
            if (s == "")
                return;

            if (s == "*Party")
            {
                SetGroupDropDownParty();
                SetPartyDropDown();
            }
            else
            {
                SetGroupDropDown(s);
            }
        }

        void comboParty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = GetComboSelectionAsString(comboParty);
            if (s == "")
                return;

            InitializeForName(s);
        }

        void Refresh_Click(object sender, RoutedEventArgs e)
        {
            InitializeForName(name);
        }

        void Squad_Click(object sender, RoutedEventArgs e)
        {
            AddSquadMember(name);
            ShowSquad();
        }

        public static void AddSquadMember(string name)
        {
            var sheet = new VirtualSheet();
            sheet.InitializeForSquad(name);
        }

        void ShowSquad_Click(object sender, RoutedEventArgs e)
        {
            ShowSquad();
        }

        public static void ShowSquad()
        {
            var root = MainWindow.mainWindow.SquadRoot;
            root.Visibility = Visibility.Visible;
            MainWindow.mainWindow.vs1.Visibility = Visibility.Hidden;
        }

        void ClearSquad_Click(object sender, RoutedEventArgs e)
        {
            ClearSquad();
        }

        public static void ClearSquad()
        {
            squadCol = 0;
            MainWindow.mainWindow.Squad.Children.Clear();
        }

        public void Refresh()
        {
            InitializeForName(name);
        }

        void InitializeForSquad(string name)
        {
            if (squadCol > 6)
                return;

            fTrainingMode = false;
            fSquadMode = true;

            var squad = MainWindow.mainWindow.Squad;
            topDockPanel.Children.Clear();
            scrollviewer.Content = null;
            
            m1.Children.Clear();
            h1.Children.Clear();
            v1.Children.Clear();

            int col = squadCol++;
            int row = 0;

            AddSubGrid(squad, gTop, col, row++);
            AddSubGrid(squad, g1, col, row++);
            AddSubGrid(squad, g4, col, row++);
            AddSubGrid(squad, g5, col, row++);
            AddSubGrid(squad, g3, col, row++);
            AddSubGrid(squad, g2, col, row++);
            AddSubGrid(squad, g6, col, row++);
            AddSubGrid(squad, gBottom, col, row++);

            squad.Margin = new Thickness(5, 0, 5, 0);

            squad.Width = Double.NaN;
            squad.Height = Double.NaN;
            squad.VerticalAlignment = VerticalAlignment.Stretch;

            squad.Children.Add(this);
            this.Width = 1;
            this.Height = 1;
            this.Visibility = Visibility.Collapsed;

            InitializeForName(name);
        }

        void AddSubGrid(Grid squad, Grid g, int col, int row)
        {
            squad.Children.Add(g); 
            Grid.SetRow(g, row); 
            Grid.SetColumn(g, col);
            g.Margin = new Thickness(15, 0, 5, 0);
        }


        void Train_Click(object sender, RoutedEventArgs e)
        {
            fTrainingMode = true;

            panel1.Visibility = Visibility.Collapsed;
            panel4.Visibility = Visibility.Visible;
            panel5.Visibility = Visibility.Visible;
        }

        void TrainingDone_Click(object sender, RoutedEventArgs e)
        {
            fTrainingMode = false;

            panel1.Visibility = Visibility.Visible;
            panel4.Visibility = Visibility.Collapsed;
            panel5.Visibility = Visibility.Collapsed;
        }

        internal void SetRemainingHours(int p)
        {
            textSessionHours.Text = p.ToString();
        }

        internal void DoSearch(string str)
        {
            str = str.ToLower();

            SearchInGrid(g1, str);
            SearchInGrid(g2, str);
            SearchInGrid(g3, str);
            SearchInGrid(g4, str);
            SearchInGrid(g5, str);
            SearchInGrid(g6, str);
        }

        private void SearchInGrid(Grid g, string str)
        {
            int rows = g.RowDefinitions.Count;
            var bits = new bool[rows];

            if (str == "")
            {
                foreach (var c in g.Children)
                {
                    var ui = c as UIElement;
                    if (ui == null)
                        continue;

                    if (ui is VirtualSheet)
                        continue;

                    ui.Visibility = Visibility.Visible;
                }
                return;
            }

            foreach (var c in g.Children)
            {
                var ui = c as UIElement;
                if (ui == null)
                    continue;

                if (Grid.GetColumn(ui) > 1)
                    continue;

                int row = Grid.GetRow(ui);

                if (ui is StackPanel)
                {
                    var panel = ui as StackPanel;
                    if (panel.Children.Count == 0)
                        continue;

                    ui = panel.Children[0];
                }

                var textbox = ui as TextBlock;
                if (textbox == null)
                    continue;

                var text = textbox.Text.ToLower();

                if (text.Contains(str) || textbox.FontWeight == FontWeights.Bold)
                    bits[row] = true;
            }

            foreach (var c in g.Children)
            {
                var ui = c as UIElement;
                if (ui == null)
                    continue;

                if (ui is VirtualSheet)
                    continue;

                int row = Grid.GetRow(ui);

                if (bits[row])
                {
                    ui.Visibility = Visibility.Visible;
                }
                else
                {
                    ui.Visibility = Visibility.Collapsed;
                }
            }
        }

        internal void LoadPartyAsSquad(string party)
        {
            pendingSquadDir = "_party/" + party;
            Main.SendHost(String.Format("dir {0}", pendingSquadDir));
        }

        void LoadSquadFromParty(DictBundle bundle)
        {
            var partyDict = bundle.dict;

            var q = from k in partyDict.Keys
                    where partyDict[k].StartsWith("y")
                    orderby k ascending
                    select k;

            ClearSquad();
            foreach (var member in q)
            {
                AddSquadMember(member);
            }
            ShowSquad();
        }

    }
}