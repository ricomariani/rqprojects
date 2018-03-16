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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for PartyInfo.xaml
    /// </summary>
    public partial class PartyInfo : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        Dictionary<string, string> partiesDict;
        Dictionary<string, string> partyDict;
        Dictionary<string, string> checkDict;
        Dictionary<string, string> fatigueDict;
        Dictionary<string, string> folderDict;
        Dictionary<string, string> lootDict;
        Dictionary<string, string> campDict;
        Dictionary<string, string> presenceDict;
        Dictionary<string, string> shugenjaDict;
        Dictionary<string, string> manaDict;
        Dictionary<string, string> runemagicDict;
        Dictionary<string, string> spiritmanaDict;
        Dictionary<string, string> usedDict;
        Dictionary<string, string> buffDict;
        Dictionary<string, string> whoDict;
        Dictionary<string, string> woundsDict;

        public Dictionary<string, string> DossierDict
        {
            get { return whoDict; }
        }

        public Dictionary<string, string> PartyDict
        {
            get { return partyDict; }
        }


        public PartyInfo()
        {
            InitializeComponent();
        }

        public void CustomInit(string selection)
        {
            int i = displayOption.Items.IndexOf(selection);
            displayOption.SelectedIndex = i;
            displayOption.Text = selection;
        }

        public string FindShugenjaId(string who, string school, string spell)
        {
            string prefix = who+"|";
            string schoolContent = "school:" + school + " ";
            string spellContent = " " + spell;

            foreach (var k in shugenjaDict.Keys)
            {
                if (!k.StartsWith(prefix))
                    continue;

                var v = shugenjaDict[k];

                if (!v.StartsWith(schoolContent))
                    continue;

                if (!v.EndsWith(spell))
                    continue;

                string head, id;
                MainWindow.Parse2Ex(k, out head, out id, '|');

                return id;
            }

            return "";
        }

        public string FindDossierInfo(string key)
        {
            string result;

            if (whoDict.TryGetValue(key, out result))
                return result;

            return "";
        }

        public List<string> FindDossierDetails(string who)
        {
            var results = new List<string>();

            foreach (var k in whoDict.Keys)
            {
                if (!k.StartsWith(who))
                    continue;

                if (k.Length < who.Length + 2)
                    continue;

                if (k[who.Length] != '|')
                    continue;

                char c = k[who.Length + 1];
                if (c < '0' || c > '9')
                    continue;

                results.Add(whoDict[k]);
            }

            return results;
        }
        
        public void Consider(DictBundle bundle)
        {
            if (bundle.path == folderPath)
            {
                folderDict = bundle.dict;
                if (displayOption.Text == "Folders") {
                    UpdateData("Folders");
                }
            }

            if (bundle.path == partyPath)
            {
                partyDict = bundle.dict;
            }           
            else switch (bundle.path)
            {
                case "_loot":
                    lootDict = bundle.dict;
                    if (displayOption.Text != "Loot") return;
                    break;

                case "_presence":
                    presenceDict = bundle.dict;
                    if (displayOption.Text != "Presence") return;
                    break;

                case "_shugenja":
                    shugenjaDict = bundle.dict;
                    if (displayOption.Text != "Shugenja") return;
                    break;
                
                case "_camp":
                    campDict = bundle.dict;
                    if (displayOption.Text != "Camp") return;
                    break;

                case "_wounds":
                    woundsDict = bundle.dict;
                    if (displayOption.Text != "Wounds") return;
                    break;

                case "_runemagic":
                    runemagicDict = bundle.dict;
                    if (displayOption.Text != "Runemagic") return;
                    break;

                case "_spiritmana":
                    spiritmanaDict = bundle.dict;
                    if (displayOption.Text != "Spirit Mana") return;
                    break;

                case "_mana":
                    manaDict = bundle.dict;
                    if (displayOption.Text != "Mana") return;
                    break;

                case "_used":
                    usedDict = bundle.dict;
                    if (displayOption.Text != "Consumables") return;
                    break;

                case "_fatigue":
                    fatigueDict = bundle.dict;
                    if (displayOption.Text != "Fatigue") return;
                    break;

                case "_buffs":
                    buffDict = bundle.dict;
                    if (displayOption.Text != "Buffs") return;
                    break;

                case "_who":
                    whoDict = bundle.dict;
                    if (displayOption.Text != "Party Dossier") return;
                    break;

                case "_checks":
                    checkDict = bundle.dict;
                    if (displayOption.Text != "Checks") return;
                    break;

                case "_party":
                    partiesDict = bundle.dict;
                    if (bundle.dict.ContainsKey(Main.botchannel))
                    {
                        var pp = partyPath;
                        partyPath = bundle.dict[Main.botchannel];

                        if (pp != partyPath)
                        {
                            Main.SendHost(String.Format("dir {0}", partyPath));
                        }
                    }

                    if (displayOption.Text != "Parties") return;
                    break;

                default:
                    return;
            }

            UpdateData(displayOption.Text);
        }

        void UpdateData(string s)
        {
            switch (s)
            {
                case "Party Members":
                    DisplayMembers();
                    break;
                case "Consumables":
                    DisplayConsumables();
                    break;
                case "Parties":
                    DisplayParties(partiesDict);
                    break;
                case "Wounds":
                    DisplayWounds();
                    break;
                case "Loot":
                    DisplayLoot();
                    break;
                case "Camp":
                    DisplayCamp();
                    break;
                case "Shugenja":
                    DisplayShugenja();
                    break;
                case "Presence":
                    DisplayPresence();
                    break;
                case "Mana":
                    DisplayMana();
                    break;
                case "Runemagic":
                    DisplayRunemagic();
                    break;
                case "Spirit Mana":
                    DisplaySpiritMana();
                    break;
                case "Folders":
                    DisplayFolders();
                    break;
                case "Fatigue":
                    DisplayFatigue();
                    break;
                case "Buffs":
                    DisplayBuffs();
                    break;
                case "Party Dossier":
                    DisplayDossier();
                    break;
                case "Checks":
                    DisplayChecks();
                    break;
            }
        }

        void DisplayMembers()
        {
            if (partyDict == null)
            {
                return;
            }

            desc.Text = partyPath;
            NewGrid(columns: 1, description: partyPath);

            foreach (var k in from s in partyDict.Keys orderby s select s)
            {
                if (!IsInParty(k))
                {
                    continue;
                }
                var t1 = new TextBlock();
                t1.Text = k;
                t1.Margin = new Thickness(5, 0, 0, 0);

                AddLastRowCell(t1, 0);
                AddRowDef();
            }
        }

        bool IsInParty(string k)
        {
            if (partyDict == null)
            {
                return false;
            }

            if (!partyDict.ContainsKey(k))
            {
                return false;
            }

            var v = partyDict[k];

            return k != ".." && (v.StartsWith("y") || v.StartsWith("Y"));
        }

        void DisplayParties(Dictionary<string, string> dict)
        {
            if (dict == null)
            {
                return;
            }

            NewGrid(columns: 1, description: "all available");

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                var v = dict[k];
                if (v != "<Folder>" || k == "..")
                {
                    continue;
                }

                var t1 = new TextBlock();
                t1.Text = k;
                t1.Margin = new Thickness(5, 0, 0, 0);

                AddLastRowCell(t1, 0);
                AddRowDef();
            }
        }

        void DisplayChecks()
        {
            FormatBundleForChecks(checkDict);
        }

        void FormatBundleById(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            NewGrid(columns: 3, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;
                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 15, 0);
                var t2 = new TextBlock();
                t2.Text = tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 15, 0);
                var t3 = new TextBlock();
                t3.Text = dict[k];

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);
                AddRowDef();
            }
        }

        void FormatBundleForChecks(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            NewGrid(columns: 2, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;
                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = tail.Replace('|', '/');

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);

                AddRowDef();
            }
        }

        void NewGrid(int columns, string description)
        {
            ClearSelection();

            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            desc.Text = description;

            for (int i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });
            }
        }

        const double row_height = 18.0;

        void AddRowDef()
        {
            var rd = new RowDefinition();
            rd.Height = new GridLength(row_height);
            grid.RowDefinitions.Add(rd);
        }

        Rectangle rectSelection;
        int rowSelection = -1;

        void ClearSelection()
        {
            FadeOutSelection();

            rowSelection = -1;
            if (rectSelection != null)
            {
                canvas.Children.Remove(rectSelection);
                rectSelection = null;
            }
        }

        void SetSelection(int row)
        {
            // already correct
            if (rowSelection == row)
            {
                return;
            }

            ClearSelection();

            if (row >= 0)
            {
                Rectangle rect = RowRectangle(row);
                canvas.Children.Add(rect);
                rectSelection = rect;
            }

            rowSelection = row;
        }

        Rectangle RowRectangle(int row)
        {
            Rectangle rect = new Rectangle();
            rect.HorizontalAlignment = HorizontalAlignment.Left;
            rect.VerticalAlignment = VerticalAlignment.Top;
            rect.IsHitTestVisible = false;

            rect.Stroke = Brushes.Blue;
            rect.Fill = Brushes.Transparent;

            rect.Width = grid.ActualWidth;
            rect.Height = row_height;
            rect.Margin = new Thickness(0, row * row_height, 0, 0);
            return rect;
        }

        void FadeOutSelection()
        {
            if (rowSelection < 0)
            {
                return;
            }

            Rectangle rect = RowRectangle(rowSelection);
            canvas.Children.Add(rect);

            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(rect, new NameScope());

            // Register the name with the page to which the element belongs.
            rect.RegisterName("element", rect);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(250));

            GameMap.Anim2Point(story, dur, "element", FrameworkElement.OpacityProperty, 1, 0);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    canvas.Children.Remove(rect);
                });

            story.Begin(rect);
        }

        void FormatBundleForFolders(Dictionary<string, string> dict)
        {
            NewGrid(columns: 2, description: folderPath);

            if (dict == null)
            {
                return;
            }

            string[] keys = dict.Keys.ToArray();
            Array.Sort(keys, (string left, string right) =>
            {
                bool f1 = dict[left] == "<Folder>";
                bool f2 = dict[right] == "<Folder>";

                if (f1 && !f2)
                    return -1;

                if (f2 && !f1)
                    return 1;

                int r = String.Compare(left, right, true);
                if (r != 0) return r;
                return String.Compare(left, right);
            });

            foreach (string k in keys)
            {
                var t1 = new TextBlock();
                t1.Text = k;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = dict[k];

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);

                AddRowDef();
            }
        }

        void FormatBundleForConsumables(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            NewGrid(columns: 4, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                string h = null;
                if (tail.EndsWith("_have")) {
                    h = "have";
                    tail = tail.Substring(0, tail.Length-5);
                }
                else if (tail.EndsWith("_used")) {
                    h = "used";
                    tail = tail.Substring(0, tail.Length-5);
                }
                else {
                    continue;
                }

                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;

                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 50, 0);
                var t2 = new TextBlock();
                t2.Text = tail;
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();
                t3.Text = h;
                t3.Margin = new Thickness(0, 0, 25, 0);
                var t4 = new TextBlock();
                t4.Text = dict[k];

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);
                AddLastRowCell(t4, 3);

                AddRowDef();
            }
        }

        void FormatBundleWithValue(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            NewGrid(columns: 3, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                // new name, add a line
                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;

                // fill out the name, key and value
                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();
                t3.Text = dict[k];

                // and add them to the table
                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);

                AddRowDef();
            }
        }

        void AddLastRowCell(FrameworkElement el, int col)
        {
            Grid.SetColumn(el, col);
            Grid.SetRow(el, grid.RowDefinitions.Count);
            grid.Children.Add(el);
        }

        void FormatBundleForLoot(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            NewGrid(columns: 4, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;

                string desc = dict[k];
                string d1, d2;
                int n1 = desc.IndexOf(" enc:");
                int n2 = desc.IndexOf(" room:");
                int n;

                if (n1 < 0 || n2 < 0)
                    n = Math.Max(n1, n2);
                else
                    n = Math.Min(n1, n2);

                if (n > 0)
                {
                    d1 = desc.Substring(0, n);
                    d2 = desc.Substring(n + 1);
                }
                else
                {
                    d1 = desc;
                    d2 = "";
                }

                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = "item:" + tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();
                t3.Text = d1;
                t3.Margin = new Thickness(0, 0, 25, 0);

                var t4 = new TextBlock();
                t4.Text = d2;

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);
                AddLastRowCell(t4, 3);

                AddRowDef();
            }
        }

        void FormatBundleForCamp(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            NewGrid(columns: 3, description: partyPath);

            foreach (var name in from s in dict.Keys orderby s select s)
            {
                string v = dict[name];

                if (v == "<Folder>")
                    continue;

                if (name == "..")
                    continue;

                string task = "none";
                string shoot = "none";

                var kTask = "task:";
                var kShoot = "shoot:";

                string s1;
                string s2;
                MainWindow.Parse2(v, out s1, out s2);

                if (s1.StartsWith(kTask))
                    task = s1.Substring(kTask.Length);
                if (s2.StartsWith(kTask))
                    task = s2.Substring(kTask.Length);

                if (s1.StartsWith(kShoot))
                    shoot = s1.Substring(kShoot.Length);
                if (s2.StartsWith(kShoot))
                    shoot = s2.Substring(kShoot.Length);

                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = kTask + task;
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();
                t3.Text = kShoot + shoot;
                t3.Margin = new Thickness(0, 0, 25, 0);

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);

                AddRowDef();
            }
        }

        string ShugenjaSortOrder(string k)
        {
            string name;
            string tail;

            if (!ExtractMemberName(k, out name, out tail))
            {
                return "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz";
            }

            string desc = shugenjaDict[k];
            string school;
            string cost;
            string charges;
            string effect;

            GetShugenjaParts(desc, out school, out cost, out charges, out effect);

            return name + "|" + school + "|" + effect + "|" + tail;
        }

        void FormatBundleForShugenja(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string prevSchool = "";
            string name;
            string tail;
            int totalCost = 0;

            NewGrid(columns: 6, description: partyPath);

            foreach (var k in from s in dict.Keys orderby ShugenjaSortOrder(s) select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                string desc = dict[k];
                string school;
                string cost;
                string charges;
                string effect;

                GetShugenjaParts(desc, out school, out cost, out charges, out effect);

                if (school != prevSchool && prevSchool != "")
                {
                    FootSchool(prevName, prevSchool, totalCost);
                    totalCost = 0; 
                }

                prevSchool = school;

                if (name != prevName && prevName != "")
                {
                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;

                int nCost = 0;
                Int32.TryParse(cost, out nCost);

                totalCost += nCost;

                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                
                var t2 = new TextBlock();
                t2.Text = "spell:" + tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 5, 0);

                var t3 = new TextBlock();
                t3.Text = "school:" + school;
                t3.Margin = new Thickness(0, 0, 5, 0);
               
                var t4 = new TextBlock();
                t4.Text = "cost:" + cost;
                t4.Margin = new Thickness(0, 0, 5, 0);

                var t5 = new TextBlock();
                t5.Text = "charges:" + charges;
                t5.Margin = new Thickness(0, 0, 25, 0); 
                
                var t6 = new TextBlock();
                t6.Text = effect;

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);
                AddLastRowCell(t4, 3);
                AddLastRowCell(t5, 4);
                AddLastRowCell(t6, 5);

                AddRowDef();
            }

            if (prevSchool != "")
            {
                FootSchool(prevName, prevSchool, totalCost);
            }
        }

        int FootSchool(string prevName, string prevSchool, int totalCost)
        {
            var tt1 = new TextBlock();
            tt1.Text = "total";
            tt1.Margin = new Thickness(0, 0, 25, 0);

            var tt2 = new TextBlock();
            tt2.Text = "school:" + prevSchool;
            tt2.Margin = new Thickness(0, 0, 5, 0);

            var tt3 = new TextBlock();
            tt3.Text = "cost:" + totalCost.ToString();
            tt3.Margin = new Thickness(0, 0, 5, 0);

            AddLastRowCell(tt1, 1); // skip name
            AddLastRowCell(tt2, 2); 
            AddLastRowCell(tt3, 3);

            string limit;

            if (whoDict.TryGetValue(prevName + "|mana|" + prevSchool, out limit))
            {
                var tt4 = new TextBlock();
                tt4.Text = "limit:" + limit;
                tt4.Margin = new Thickness(0, 0, 5, 0);
                AddLastRowCell(tt4, 4);
            }

            AddRowDef();

            totalCost = 0;
            return totalCost;
        }
        
        public static void GetShugenjaParts(string desc, out string school, out string cost, out string charges, out string effect)
        {
            const string schoolStr = "school:";
            const string costStr = "cost:";
            const string chargesStr = "charges:";

            school = "unknown";
            cost = "0";
            charges = "0";

            string car, ctr;

            MainWindow.Parse2(desc, out car, out ctr);
            if (car.StartsWith(schoolStr))
            {
                school = car.Substring(schoolStr.Length);
                desc = ctr;
            }

            MainWindow.Parse2(desc, out car, out ctr);
            if (car.StartsWith(costStr))
            {
                cost = car.Substring(costStr.Length);
                desc = ctr;
            }

            MainWindow.Parse2(desc, out car, out ctr);
            if (car.StartsWith(chargesStr))
            {
                charges = car.Substring(chargesStr.Length);
                desc = ctr;
            }

            effect = desc;

            if (effect == "")
                effect = "unknown";
        }


        private void FootWounds(string prevName, int totalWounds)
        {
            string limit = "?";
            whoDict.TryGetValue(prevName + "|life", out limit);
            
            var t1 = new TextBlock();
            t1.Text = "total";
            t1.Margin = new Thickness(0, 0, 25, 0);

            var t2 = new TextBlock();
            t2.Text = "wounds:" + totalWounds.ToString() + " total_life:" + limit.ToString();
            t2.Margin = new Thickness(0, 0, 5, 0);

            AddLastRowCell(t1, 1); // skip name
            AddLastRowCell(t2, 2);

            AddRowDef();
        }
               
        void FormatBundleForWounds(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;

            int totalWounds = 0;

            NewGrid(columns: 3, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                // new name, add a line
                if (name != prevName && prevName != "")
                {
                    FootWounds(prevName, totalWounds);
                    totalWounds = 0;

                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                prevName = name;

                // fill out the name, key and value
                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();

                var dmg = dict[k];
                t3.Text = dmg;

                if (dmg.StartsWith("damage:"))
                {
                    int nWounds = 0;
                    for (int i = 7; i < dmg.Length;i++ )
                    {
                        if (dmg[i] >= '0' && dmg[i] <= '9')
                        {
                            nWounds = nWounds * 10 + dmg[i] - '0';
                        }
                        else
                        {
                            break;
                        }
                    }
                    totalWounds += nWounds;
                }

                // and add them to the table
                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);

                AddRowDef();
            }

            if (prevName != "")
            {
                FootWounds(prevName, totalWounds);
            }

        }

        void FootPresence(string prevName, int totalCost)
        {
            var t1 = new TextBlock();
            t1.Text = "total";
            t1.Margin = new Thickness(0, 0, 25, 0);

            var t2 = new TextBlock();
            t2.Text = "cost:" + totalCost.ToString();
            t2.Margin = new Thickness(0, 0, 5, 0);

            AddLastRowCell(t1, 1); // skip name
            AddLastRowCell(t2, 2);

            string limit;

            if (whoDict.TryGetValue(prevName + "|presence_limit", out limit))
            {
                var t3 = new TextBlock();
                t3.Text = "limit:" + limit;
                t3.Margin = new Thickness(0, 0, 5, 0);
                AddLastRowCell(t3, 3);
            }

            AddRowDef();
        }

        void FormatBundleForPresence(Dictionary<string, string> dict)
        {
            if (dict == null || partyDict == null)
            {
                return;
            }

            string prevName = "";
            string name;
            string tail;
            int totalCost = 0;

            NewGrid(columns: 4, description: partyPath);

            foreach (var k in from s in dict.Keys orderby s select s)
            {
                if (!ExtractMemberName(k, out name, out tail))
                {
                    continue;
                }

                if (!IsInParty(name))
                {
                    continue;
                }

                string desc = dict[k];
                string cost;
                string effect;
                GetPresenceParts(desc, out effect, out cost);

                if (name != prevName && prevName != "")
                {
                    FootPresence(prevName, totalCost);
                    totalCost = 0;

                    var t = new TextBlock();
                    t.Text = "---";
                    AddLastRowCell(t, 0);
                    AddRowDef();
                }

                int nCost = 0;
                Int32.TryParse(cost, out nCost);
                totalCost += nCost;

                prevName = name;

                var t1 = new TextBlock();
                t1.Text = name;
                t1.Margin = new Thickness(0, 0, 25, 0);
                var t2 = new TextBlock();
                t2.Text = "spell:" + tail.Replace('|', '/');
                t2.Margin = new Thickness(0, 0, 25, 0);
                var t3 = new TextBlock();
                t3.Text = "cost:"+cost;
                t3.Margin = new Thickness(0, 0, 25, 0);
                var t4 = new TextBlock();
                t4.Text = effect;

                AddLastRowCell(t1, 0);
                AddLastRowCell(t2, 1);
                AddLastRowCell(t3, 2);
                AddLastRowCell(t4, 3);

                AddRowDef();
            }

            if (prevName != "")
            {
                FootPresence(prevName, totalCost);
            }
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

        bool ExtractMemberName(string k, out string name, out string tail)
        {
            int n = k.IndexOf('|');
            if (n < 0)
            {
                name = "";
                tail = "";
                return false;
            }

            name = k.Substring(0, n);
            tail = k.Substring(n + 1);
            return true;
        }

        void DisplayDossier()
        {
            FormatBundleById(whoDict);
        }

        void DisplayBuffs()
        {
            FormatBundleById(buffDict);
        }

        void DisplayLoot()
        {
            FormatBundleForLoot(lootDict);
        }

        void DisplayCamp()
        {
            FormatBundleForCamp(campDict);
        }

        void DisplayShugenja()
        {
            FormatBundleForShugenja(shugenjaDict);
        }
        
        void DisplayPresence()
        {
            FormatBundleForPresence(presenceDict);
        }

        void DisplayFolders()
        {
            FormatBundleForFolders(folderDict);
        }

        void DisplayConsumables()
        {
            FormatBundleForConsumables(usedDict);
        }
        
        void DisplayFatigue()
        {
            FormatBundleWithValue(fatigueDict);
        }

        void DisplayRunemagic()
        {
            FormatBundleWithValue(runemagicDict);
        }

        void DisplaySpiritMana()
        {
            FormatBundleWithValue(spiritmanaDict);
        }
        
        void DisplayMana()
        {
            FormatBundleWithValue(manaDict);
        }

        void DisplayWounds()
        {
            FormatBundleForWounds(woundsDict);
        }

        void displayOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string s = GetComboString(displayOption);

            if (s != "")
            {
                UpdateData(s);
            }
        }

        static string GetComboString(ComboBox c)
        {
            if (c.SelectedIndex == -1)
                return "";

            var o = c.SelectedItem;

            var ci = o as ComboBoxItem;

            if (ci == null)
                return "";

            var s = ci.Content as string;

            if (s == null)
                return "";

            return s;
        }

        void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(grid);

            if (p.X >= canvas.ActualWidth || p.Y >= canvas.ActualHeight) return;

            int row = GetRowFromMouse(e);

            SetSelection(row);
        }

        int GetRowFromMouse(MouseEventArgs e)
        {
            Point p = e.GetPosition(grid);

            int row = (int)(p.Y / row_height);

            if (row < 0 || row >= grid.RowDefinitions.Count)
            {
                row = -1;
            }

            var item = ValueAt(row, 0);

            if (item == "---" || item == "")
            {
                return -1;
            }

            return row;
        }

        int latchedRow = -1;
        string latchedField0;
        string latchedField1;
        string latchedField2;
        string latchedField3;
        string latchedField4;
        string latchedField5;
        string folderPath = "/";
        string partyPath = "";

        public string FolderPath { get { return folderPath; } }
        public string PartyPath { get { return partyPath; } }

        void ScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right)
            {
                return;
            }

            int row = GetRowFromMouse(e);

            SetSelection(row);

            latchedRow = rowSelection;
            latchedField0 = ValueAt(latchedRow, 0);
            latchedField1 = ValueAt(latchedRow, 1);
            latchedField2 = ValueAt(latchedRow, 2);
            latchedField3 = ValueAt(latchedRow, 3);
            latchedField4 = ValueAt(latchedRow, 4);
            latchedField5 = ValueAt(latchedRow, 5);

            string s = GetComboString(displayOption);

            if (s == "")
                return;

            switch (s)
            {
                case "Party Members":
                    ShowMembersMenu();
                    break;
                case "Consumables":
                    ShowConsumablesMenu();
                    break;
                case "Parties":
                    ShowPartiesMenu();
                    break;
                case "Wounds":
                    ShowWoundMenu();
                    break;
                case "Camp":
                    ShowCampMenu();
                    break;
                case "Shugenja":
                    ShowShugenjaMenu();
                    break;
                case "Presence":
                    ShowPresenceMenu();
                    break;
                case "Loot":
                    ShowLootMenu();
                    break;
                case "Runemagic":
                    ShowRunemagicMenu();
                    break;
                case "Spirit Mana":
                    ShowSpiritManaMenu();
                    break;
                case "Mana":
                    ShowManaMenu();
                    break;
                case "Folders":
                    ShowFoldersMenu();
                    break;
                case "Fatigue":
                    ShowFatigueMenu();
                    break;
                case "Buffs":
                    ShowGenericMenu(GetBuffMeta());
                    break;
                case "Party Dossier":
                    ShowGenericMenu(GetDossierMeta());
                    break;
                case "Checks":
                    ShowCheckMenu();
                    break;
            }
        }

        void ShowCampMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            if (rowSelection >= 0)
            {
                m = new MenuItem();
                m.Header = "Edit";
                m.Click += new RoutedEventHandler(CampTask_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void CampTask_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var task = latchedField1;
            var shoot = latchedField2;

            var kTask = "task:";
            var kShoot = "shoot:";

            if (task.StartsWith(kTask))
                task = task.Substring(kTask.Length);
            else
                task = "none";

            if (shoot.StartsWith(kShoot))
                shoot = shoot.Substring(kShoot.Length);
            else
                shoot = "none";

            if (who == "" || task == "" || shoot == "")
            {
                return;
            }

            ManyKey dlg = new ManyKey("Update Camping Tasks for " + who, "Task:", task, "Shoot:", shoot);

            if (dlg.ShowDialog() == true)
            {
                var taskNew = dlg.Results[0].Replace(" ", "_").Replace(":", "_");
                var shootNew = dlg.Results[1].Replace(" ", "_").Replace(":", "_");

                if (taskNew == "" || shootNew == "")
                {
                    return;
                }

                if (taskNew != task) Main.SendHost(String.Format("!camp {0} {1}", who, taskNew));
                if (shootNew != shoot) Main.SendHost(String.Format("!camp shoot {0} {1}", who, shootNew));
            }
        }


        void ShowShugenjaMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(ShugenjaNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler(ShugenjaRemove_Click);
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Update";
                m.Click += new RoutedEventHandler(ShugenjaUpdate_Click);
                menu.Items.Add(m);

                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Add Charge";
                m.Click += new RoutedEventHandler(ShugenjaAddCharge_Click);
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Remove Charge";
                m.Click += new RoutedEventHandler(ShugenjaRemoveCharge_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void ShugenjaNew_Click(object sender, RoutedEventArgs e)
        {
            ManyKey dlg = new ManyKey("New Spell", "Who:", "", "School:", "", "Cost:", "", "Charges:", "", "Effect:", "");

            if (dlg.ShowDialog() == true)
            {
                string who = dlg.Results[0].Replace(" ", "_");
                string school = dlg.Results[1].Replace(" ", "_");
                string cost = dlg.Results[2].Replace(" ", "_");
                string charges = dlg.Results[3].Replace(" ", "_");
                string effect = dlg.Results[4];

                Main.SendHost(String.Format("!shugenja {0} {1} school:{2} cost:{3} charges:{4}", who, effect, school, cost, charges));
            }
        }

        void ShugenjaRemove_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;

            if (spellid != "")
            {
                Main.SendHost(String.Format("!shugenja remove:yes {0} {1}", who, spellid));
            }
        }

        void ShugenjaUpdate_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;
            var school = RemovePrefix("school:", latchedField2);
            var cost = RemovePrefix("cost:", latchedField3);
            var charges = RemovePrefix("charges:", latchedField4);
            var effect = latchedField5;

            if (who == "" || spellid == "" || cost == "" || school == "" || charges == "" || effect == "")
            {
                return;
            }

            ManyKey dlg = new ManyKey(3, "Update Spell for " + who, "Who:", who, "School:", school, "Cost:", cost, "Charges:", charges, "Effect:", effect);

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                school = dlg.Results[1].Replace(" ", "_");
                cost = dlg.Results[2].Replace(" ", "_");
                charges = dlg.Results[3].Replace(" ", "_");
                effect = dlg.Results[4];

                if (who == "" || spellid == "" || cost == "" || school == "" || charges == "" || effect == "")
                {
                    return;
                }

                int nCharges;
                if (!Int32.TryParse(charges, out nCharges))
                {
                    return;
                }

                if (nCharges <0 || nCharges >3)
                {
                    return;
                }

                if (nCharges != 0)
                    Main.SendHost(String.Format("!shugenja {0} {1} school:{2} cost:{3} charges:{4} {5}", who, effect, school, cost, charges, spellid));
                else
                    Main.SendHost(String.Format("!shugenja remove:yes {0} {1}", who, spellid));
            }
        }

        void ShugenjaRemoveCharge_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;
            var school = RemovePrefix("school:", latchedField2);
            var cost = RemovePrefix("cost:", latchedField3);
            var charges = RemovePrefix("charges:", latchedField4);
            var effect = latchedField5;

            if (who == "" || spellid == "" || cost == "" || school == "" || charges == "" || effect == "")
            {
                return;
            }

            int nCharges;
            if (!Int32.TryParse(charges, out nCharges))
            {
                return;
            }

            if (nCharges < 0 || nCharges > 3)
            {
                return;
            }

            nCharges--;

            if (nCharges != 0)
                Main.SendHost(String.Format("!shugenja {0} {1} school:{2} cost:{3} charges:{4} {5}", who, effect, school, cost, nCharges, spellid));
            else
                Main.SendHost(String.Format("!shugenja remove:yes {0} {1}", who, spellid));       
        }

        void ShugenjaAddCharge_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;
            var school = RemovePrefix("school:", latchedField2);
            var cost = RemovePrefix("cost:", latchedField3);
            var charges = RemovePrefix("charges:", latchedField4);
            var effect = latchedField5;

            if (who == "" || spellid == "" || cost == "" || school == "" || charges == "" || effect == "")
            {
                return;
            }

            int nCharges;
            if (!Int32.TryParse(charges, out nCharges))
            {
                return;
            }

            if (nCharges < 0 || nCharges > 2)
            {
                return;
            }

            nCharges++;

            Main.SendHost(String.Format("!shugenja {0} {1} school:{2} cost:{3} charges:{4} {5}", who, effect, school, cost, nCharges, spellid));
        }

        static string RemovePrefix(string prefix, string target)
        {
            if (!target.StartsWith(prefix))
                return "";

            return target.Substring(prefix.Length);          
        }

        void ShowPresenceMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(PresenceNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler(PresenceRemove_Click);
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Update";
                m.Click += new RoutedEventHandler(PresenceUpdate_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void PresenceNew_Click(object sender, RoutedEventArgs e)
        {
            ManyKey dlg = new ManyKey("New Spell", "Who:", "", "Effect:", "", "Cost:", "");

            if (dlg.ShowDialog() == true)
            {
                string who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string cost = dlg.Results[2].Replace(" ", "_");

                Main.SendHost(String.Format("!presence {0} {1} cost:{2}", who, what, cost));
            }
        }

        void PresenceRemove_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;

            if (spellid != "")
            {
                Main.SendHost(String.Format("!presence remove:yes {0} {1}", who, spellid));
            }
        }

        void PresenceUpdate_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var spellid = latchedField1;
            var cost = RemovePrefix("cost:", latchedField2);
            var effect = latchedField3;

            if (who == "" || spellid == "" || cost == "" || effect == "")
            {
                return;
            }

            ManyKey dlg = new ManyKey("Update Spell for " + who, "Effect:", effect, "Cost:", cost);

            if (dlg.ShowDialog() == true)
            {
                effect = dlg.Results[0];
                cost = dlg.Results[1].Replace(" ", "_").Replace(":", "_");

                if (effect == "" || cost == "")
                {
                    return;
                }

                Main.SendHost(String.Format("!presence {0} {1} cost:{2} {3}", who, effect, cost, spellid));
            }
        }

        void ShowLootMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(LootNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler(LootRemove_Click);
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Assign";
                m.Click += new RoutedEventHandler(LootAssign_Click);
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Update";
                m.Click += new RoutedEventHandler(LootUpdate_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void LootNew_Click(object sender, RoutedEventArgs e)
        {
            ManyKey dlg = new ManyKey("New Loot", "Who:", "", "What:", "", "Where:", "", "Enc:", "");

            if (dlg.ShowDialog() == true)
            {
                string who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string where = dlg.Results[2].Replace(" ", "_");
                string enc = dlg.Results[3].Replace(" ", "_");

                Main.SendHost(String.Format("!loot {0} {1} room:{2} enc:{3}", who, what, where, enc));
            }
        }

        void LootRemove_Click(object sender, RoutedEventArgs e)
        {
            var item = latchedField1;

            if (item != "")
            {
                Main.SendHost(String.Format("!loot remove:yes {0}", item));
            }
        }

        void LootAssign_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var item = latchedField1;

            if (who == "" || item == "")
            {
                return;
            }

            var dlg = new ManyKey("Assign Loot", "Owner:", who);

            if (dlg.ShowDialog() == true)
            {
                string v = dlg.Results[0];

                if (v == "")
                {
                    return;
                }

                Main.SendHost(String.Format("!loot {0} {1}", v, item));
            }
        }

        void LootUpdate_Click(object sender, RoutedEventArgs e)
        {
            var desc = latchedField2;
            var item = latchedField1;

            if (desc == "" || item == "")
            {
                return;
            }

            string d1;
            string enc;
            string room;
            GetLootParts("dummy " + latchedField3, out d1, out enc, out room);

            ManyKey dlg = new ManyKey("Update Item Details", "Description:", desc, "Enc:", enc, "Room:", room);

            if (dlg.ShowDialog() == true)
            {
                desc = dlg.Results[0];
                enc = dlg.Results[1].Replace(" ", "_").Replace(":", "_");
                room = dlg.Results[2].Replace(" ", "_").Replace(":", "_");

                if (desc == "" || enc == "" || room == "")
                {
                    return;
                }

                Main.SendHost(String.Format("!loot update {0} {1} enc:{2} room:{3}", item, desc, enc, room));
            }
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

        string ValueAt(int row, int col)
        {
            foreach (var obj in grid.Children)
            {
                var child = obj as TextBlock;

                if (child == null)
                    continue;

                if (Grid.GetColumn(child) != col || Grid.GetRow(child) != row)
                    continue;

                return child.Text;
            }

            return "";
        }

        void ShowCheckMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(CheckNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler(CheckRemove_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void CheckNew_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                focus = 1;
            }

            var dlg = new ManyKey(focus, "New Check", "Who:", who, "What", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];

                Main.SendHost(String.Format("!check {0} {1}", who, what));
            }
        }

        void CheckRemove_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            var what = latchedField1;

            Main.SendHost(String.Format("!check {0} {1} remove:yes", who, what));
        }

        public class NoteMeta
        {
            public string Command;
            public string New;
            public string Update;
        }

        NoteMeta GetBuffMeta()
        {
            var n = new NoteMeta();
            n.Command = "buff";
            n.New = "New Buff";
            n.Update = "Update Buff";

            return n;
        }

        NoteMeta GetDossierMeta()
        {
            var n = new NoteMeta();
            n.Command = "pc";
            n.New = "New Info";
            n.Update = "Update Info";

            return n;
        }
        
        void ShowGenericMenu(NoteMeta n)
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler((x, y) => GenericNew(n));
            menu.Items.Add(m);

            // do not allow updates of any id's that are not fully numeric, those are bound to be synthetic ids
            var id = latchedField1;
            foreach (char c in id)
            {
                if (c < '0' || c > '9')
                {
                    rowSelection = -1;
                    break;
                }
            }

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Update";
                m.Click += new RoutedEventHandler((x, y) => GenericUpdate(n));
                menu.Items.Add(m);

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler((x, y) => GenericRemove(n));
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void GenericUpdate(NoteMeta n)
        {
            if (latchedRow < 0)
                return;

            var who = latchedField0;
            var id = latchedField1;
            var what = latchedField2;

            var dlg = new ManyKey( n.Update + " (id:" + id + ")", "Who:", who, "What", what);

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                what = dlg.Results[1];

                Main.SendHost(String.Format("!{0} @{1} id:{2} {3}", n.Command, who, id, what));
            }
        }
        
        void GenericNew(NoteMeta n)
        {
            string who = "";
            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                focus = 1;
            }

            var dlg = new ManyKey(focus, n.New, "Who:", who, "What", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];


                Main.SendHost(String.Format("!{0} @{1} {2}", n.Command, who, what));
            }
        }

        void GenericRemove(NoteMeta n)
        {
            var who = latchedField0;
            var id = latchedField1;

            Main.SendHost(String.Format("!{0} @{1} id:{2} remove:yes", n.Command, who, id));
        }


        void ShowSpiritManaMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "Set Used Amount";
            m.Click += new RoutedEventHandler(SpiritManaUse_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Recover";
                m.Click += new RoutedEventHandler(SpiritManaRecover_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void SpiritManaUse_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string spirit = "";
            string used_and_max = "";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                spirit = latchedField1;
                used_and_max = latchedField2;

                focus = 2;
            }

            string used;
            string max;

            VirtualSheet.SplitUsedAndMax(used_and_max, out used, out max);

            ManyKey dlg = new ManyKey(focus, "Use Spirit Mana", "Who:", who, "What Spirit:", spirit, "How Much:", used);

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                spirit = dlg.Results[1];
                used = dlg.Results[2].Replace(" ", "_");

                Main.SendHost(String.Format("!spiritmana {0} {1} used:{2}", who, spirit, used));
            }
        }

        void SpiritManaRecover_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0)
                return;

            string who = latchedField0;
            string effect = latchedField1;

            Main.SendHost(String.Format("!spiritmana  {0} {1} used:0", who, effect));
        }

        void ShowRunemagicMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "Set Used Amount";
            m.Click += new RoutedEventHandler(RunemagicUse_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Recover";
                m.Click += new RoutedEventHandler(RunemagicRecover_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void RunemagicUse_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string effect = "";
            string used_and_max = "";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                effect = latchedField1;
                used_and_max = latchedField2;

                focus = 2;
            }

            string used;
            string max;

            VirtualSheet.SplitUsedAndMax(used_and_max, out used, out max);


            ManyKey dlg = new ManyKey(focus, "Use Runemagic", "Who:", who, "What Kind:", effect, "How Much:", used);

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                effect = dlg.Results[1];
                used = dlg.Results[2].Replace(" ", "_");

                Main.SendHost(String.Format("!runemagic {0} {1} used:{2}", who, effect, used));
            }
        }

        void RunemagicRecover_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0)
                return;

            string who = latchedField0;
            string effect = latchedField1;

            Main.SendHost(String.Format("!runemagic {0} {1} used:0", who, effect));
        }

        void ShowManaMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "Use";
            m.Click += new RoutedEventHandler(ManaUse_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Recover";
                m.Click += new RoutedEventHandler(ManaRecover_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void ManaUse_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string kind = "normal";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                kind = latchedField1.Replace("mana/", "").Replace("mana", "normal");
                focus = 2;
            }

            ManyKey dlg = new ManyKey(focus, "Use Mana", "Who:", who, "What Kind:", kind, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string amount = dlg.Results[2].Replace(" ", "_");

                if (what == "normal")
                {
                    Main.SendHost(String.Format("!mana {0} {1}", who, amount));
                }
                else
                {
                    Main.SendHost(String.Format("!mana {0} {1} {2}", who, what, amount));
                }
            }
        }

        void ManaRecover_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string kind = "normal";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                kind = latchedField1.Replace("mana/", "").Replace("mana", "normal");
                focus = 2;
            }

            ManyKey dlg = new ManyKey(focus, "Use Mana", "Who:", who, "What Kind:", kind, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string amount = dlg.Results[2].Replace(" ", "_");

                if (what == "normal")
                {
                    Main.SendHost(String.Format("!mana {0} -{1}", who, amount));
                }
                else
                {
                    Main.SendHost(String.Format("!mana {0} {1} -{2}", who, what, amount));
                }
            }
        }

        void ShowFatigueMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "Use";
            m.Click += new RoutedEventHandler(FatigueUse_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Recover";
                m.Click += new RoutedEventHandler(FatigueRecover_Click);
                menu.Items.Add(m);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void FatigueUse_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string kind = "normal";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                kind = latchedField1.Replace("fatigue/", "").Replace("fatigue", "normal");
                focus = 2;
            }

            ManyKey dlg = new ManyKey(focus, "Use Fatigue", "Who:", who, "What Kind:", kind, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string amount = dlg.Results[2].Replace(" ", "_");

                if (what == "normal")
                {
                    Main.SendHost(String.Format("!fatigue {0} {1}", who, amount));
                }
                else
                {
                    Main.SendHost(String.Format("!fatigue {0} {1} {2}", who, what, amount));
                }
            }
        }

        void FatigueRecover_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string kind = "normal";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                kind = latchedField1.Replace("fatigue/", "").Replace("fatigue", "normal");
                focus = 2;
            }

            ManyKey dlg = new ManyKey(focus, "Use Fatigue", "Who:", who, "What Kind:", kind, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                string what = dlg.Results[1];
                string amount = dlg.Results[2].Replace(" ", "_");

                if (what == "normal")
                {
                    Main.SendHost(String.Format("!fatigue {0} -{1}", who, amount));
                }
                else
                {
                    Main.SendHost(String.Format("!fatigue {0} {1} -{2}", who, what, amount));
                }
            }
        }

        void ShowMembersMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(MemberNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Remove";
                m.Click += new RoutedEventHandler(MemberRemove_Click);
                menu.Items.Add(m);

                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Add To Squad";
                m.Click += new RoutedEventHandler(MemberToSquad_Click);
                menu.Items.Add(m);

            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void MemberNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("New Party Member", "Name:", "");

            if (dlg.ShowDialog() == true)
            {
                string who = dlg.Results[0].Replace(" ", "_");

                Main.SendHost(String.Format("!party add {0}", who));
            }
        }

        void MemberRemove_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            Main.SendHost(String.Format("!party remove {0}", who));
        }

        void MemberToSquad_Click(object sender, RoutedEventArgs e)
        {
            var who = latchedField0;
            VirtualSheet.AddSquadMember(who);
        }

        void ShowPartiesMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New";
            m.Click += new RoutedEventHandler(PartiesNew_Click);
            menu.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Set As Current";
                m.Click += new RoutedEventHandler(PartyCurrent_Click);
                menu.Items.Add(m);

                menu.Items.Add(new Separator());

                m = new MenuItem();
                m.Header = "Load As Squad";
                m.Click += new RoutedEventHandler(PartyAsSquad_Click);
                menu.Items.Add(m);

            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void PartiesNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("New Party", "Name:", "");

            if (dlg.ShowDialog() == true)
            {
                string party = dlg.Results[0].Replace(" ", "_");

                Main.SendHost(String.Format("!party new {0}", party));
            }
        }

        void PartyCurrent_Click(object sender, RoutedEventArgs e)
        {
            var party = latchedField0;
            Main.SendHost(String.Format("!party _party/{0}", party));
        }

        void PartyAsSquad_Click(object sender, RoutedEventArgs e)
        {
            var party = latchedField0;
            Main.vs1.LoadPartyAsSquad(party);
        }

        void ShowWoundMenu()
        {
            var menu = new ContextMenu();

            MenuItem m0, m;

            if (rowSelection >= 0)
            {
                m0 = new MenuItem();
                m0.Header = "Add More Damage Here";
                m0.Click += new RoutedEventHandler(WoundAddDamage_Click);
                menu.Items.Add(m0);

                menu.Items.Add(new Separator());
            }

            m0 = new MenuItem();
            m0.Header = "Add New Damage";
            menu.Items.Add(m0);

            m = new MenuItem();
            m.Header = "r_leg";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "l_leg";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "abdomen";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "chest";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "r_arm";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "l_arm";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m = new MenuItem();
            m.Header = "head";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m0.Items.Add(new Separator());

            m = new MenuItem();
            m.Header = "life";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            m0.Items.Add(new Separator());

            m = new MenuItem();
            m.Header = "other";
            m.Click += new RoutedEventHandler(WoundAddDamage_Click);
            m0.Items.Add(m);

            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                m0 = new MenuItem();
                m0.Header = "Heal Damage";
                m0.Click += new RoutedEventHandler(WoundRecover_Click);
                menu.Items.Add(m0);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void WoundAddDamage_Click(object sender, RoutedEventArgs e)
        {
            string who = "";
            string where = "";

            int focus = 0;

            if (latchedRow >= 0)
            {
                who = latchedField0;
                where = latchedField1;
            }

            if (sender is MenuItem) {
                var cmd = (sender as MenuItem).Header as string;
                if (cmd != null && cmd != "other" && cmd != "Add More Damage Here")
                {
                    where = cmd;                   
                }
            }

            if (who != "") focus = 1;
            if (who != "" && where != "") focus = 2;

            ManyKey dlg = new ManyKey(focus, "Add Damage", "Who:", who, "Where:", where, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                where = dlg.Results[1];
                string amount = dlg.Results[2];
                Main.SendHost(String.Format("!wound {0} {1} {2}", who, where, amount));
            }
        }

        void WoundRecover_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0)
            {
                return;
            }

            string who = latchedField0;
            string where = latchedField1;

            ManyKey dlg = new ManyKey(2, "Heal Wounds", "Who:", who, "Where:", where, "How Much:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_");
                where = dlg.Results[1];
                string amount = dlg.Results[2].Replace(" ", "_");
                Main.SendHost(String.Format("!wound {0} {1} -{2}", who, where, amount));
            }
        }

        void ScrollViewer_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (rowSelection < 0 || displayOption.Text != "Folders") 
            {
                return;
            }

            string key = ValueAt(rowSelection, 0);
            string value = ValueAt(rowSelection, 1);

            if (key == "" || value == "")
                return;

            if (value == "<Folder>")
            {
                if (key == "..")
                {
                    int ix = folderPath.LastIndexOf('/');
                    if (ix >= 0)
                        folderPath = folderPath.Substring(0, ix);
                    else
                        folderPath = "/";
                }
                else if (folderPath == "/")
                    folderPath = key;
                else
                    folderPath = folderPath + "/" + key;

                RefreshCurrentDir();
            }
            else
            {
                ManyKey dlg = new ManyKey(1, "Edit Key", "Key:", key, "Value:", value);

                if (dlg.ShowDialog() == true)
                {
                    string kk = dlg.Results[0].Replace(" ", "_");
                    string vv = dlg.Results[1];

                    Main.SendHost(String.Format("n {0} {1} {2}", folderPath, kk, vv));
                }
            }
        }

        void RefreshCurrentDir()
        {
            grid.Children.Clear();
            ClearSelection();
            Main.SendHost(String.Format("dir {0}", folderPath));
        }

        void ShowFoldersMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "New Folder";
            m.Click += new RoutedEventHandler(FolderNew_Click);
            menu.Items.Add(m);

            m = new MenuItem();
            m.Header = "New Item";
            m.Click += new RoutedEventHandler(ItemNew_Click);
            menu.Items.Add(m);

            menu.Items.Add(new Separator());

            m = new MenuItem();
            m.Header = "New Map";
            m.Click += new RoutedEventHandler(MapNew_Click);
            menu.Items.Add(m);            
            
            if (rowSelection >= 0)
            {
                menu.Items.Add(new Separator());

                if (latchedField1 == "<Folder>")
                {
                    if (latchedField1 != "..")
                    {
                        m = new MenuItem();
                        m.Header = "Rename";
                        m.Click += new RoutedEventHandler(FolderRename_Click);
                        menu.Items.Add(m);

                        m = new MenuItem();
                        m.Header = "Delete";
                        m.Click += new RoutedEventHandler(FolderDelete_Click);
                        menu.Items.Add(m);
                    }
                }
                else
                {
                    m = new MenuItem();
                    m.Header = "Rename";
                    m.Click += new RoutedEventHandler(ItemRename_Click);
                    menu.Items.Add(m);

                    m = new MenuItem();
                    m.Header = "Edit";
                    m.Click += new RoutedEventHandler(ItemEdit_Click);
                    menu.Items.Add(m);

                    m = new MenuItem();
                    m.Header = "Delete";
                    m.Click += new RoutedEventHandler(ItemDelete_Click);
                    menu.Items.Add(m);

                }
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0)
            {
                return;
            }

            if (latchedField0 == "" || latchedField0 == ".." || latchedField1 != "<Folder>") 
            {
                return;
            }

            if (folderPath == "/") 
            {
                Main.SendHost(String.Format("del {0}", latchedField0));
            }
            else 
            {
                Main.SendHost(String.Format("del {0}/{1}", folderPath, latchedField0));
            }
        }

        void ItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0)
            {
                return;
            }

            if (latchedField0 == "" || latchedField0 == ".." || latchedField1 == "<Folder>") 
            {
                return;
            }

            Main.SendHost(String.Format("del {0} {1}", folderPath, latchedField0));
        }

        void FolderNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("New Folder", "Name:", "");

            if (dlg.ShowDialog() == true)
            {
                string name = dlg.Results[0].Replace(" ", "_").Replace("/", "_");

                if (folderPath != "/")
                {
                    name = folderPath + "/" + name;
                }

                if (folderPath == "_maps")
                {
                    Main.MakeNewMap(name);
                }
                else
                {
                    Main.SendHost(String.Format("n {0} {1} {2}", name, "Empty", "nil"));
                }               
            }
        }

        void MapNew_Click(object sender, RoutedEventArgs e)
        {
            Main.PromptNewMap();
        }

        void ItemNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("New Item", "Key:", "", "Value:", "");

            if (dlg.ShowDialog() == true)
            {
                string key = dlg.Results[0].Replace(" ", "_").Replace("/", "_");
                string value = dlg.Results[1];

                Main.SendHost(String.Format("n {0} {1} {2}", folderPath, key, value));               
            }
        }

        void ItemEdit_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0 || latchedField0 == "" || latchedField1 == "")
            {
                return;
            }

            var dlg = new ManyKey(1, "New Item", "Key:", latchedField0, "Value:", latchedField1);

            if (dlg.ShowDialog() == true)
            {
                string key = dlg.Results[0].Replace(" ", "_").Replace("/", "_");
                string value = dlg.Results[1];

                Main.SendHost(String.Format("n {0} {1} {2}", folderPath, key, value));
            }
        }

        void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0 || latchedField0 == "" || latchedField0 == ".." || latchedField1 != "<Folder>")
            {
                return;
            }

            var dlg = new ManyKey("Rename Folder", "New Name:", latchedField0);

            if (dlg.ShowDialog() == true)
            {
                string key = dlg.Results[0].Replace(" ", "_").Replace("/", "_");

                if (key == latchedField0)
                {
                    return;
                }

                string cmd;
                if (folderPath == "/")
                    cmd = String.Format("ren {0} {1}", latchedField0, key);
                else
                    cmd = String.Format("ren {0}/{1} {0}/{2}", folderPath, latchedField0, key);

                Main.SendHost(cmd);
            }
        }

        void ItemRename_Click(object sender, RoutedEventArgs e)
        {
            if (latchedRow < 0 || latchedField0 == "" || latchedField0 == ".." || latchedField1 == "<Folder>")
            {
                return;
            }

            var dlg = new ManyKey("Rename Item", "New Name:", latchedField0);

            if (dlg.ShowDialog() == true)
            {
                string key = dlg.Results[0].Replace(" ", "_").Replace("/", "_");

                if (key == latchedField0)
                {
                    return;
                }

                Main.SendHost(String.Format("del {0} {1}", folderPath, latchedField0));
                Main.SendHost(String.Format("n {0} {1} {2}", folderPath, key, latchedField1));               
            }
        }

        void ShowConsumablesMenu()
        {
            var menu = new ContextMenu();

            MenuItem m;

            m = new MenuItem();
            m.Header = "Use";
            m.Click += new RoutedEventHandler(UseItem_Click);
            menu.Items.Add(m);

            menu.Items.Add(new Separator());

            m = new MenuItem();
            m.Header = "Gain";
            m.Click += new RoutedEventHandler(GainItem_Click);
            menu.Items.Add(m);

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        void UseItem_Click(object sender, RoutedEventArgs e)
        {
            int focus = 0;
            if (latchedField0 != "") focus = 1;

            string who = latchedField0;
            string what = latchedField1;

            if (who != "") focus = 1;
            if (who != "" && what != "") focus = 2;

            ManyKey dlg = new ManyKey(focus, "Consume Item", "Who:", who, "What:", what, "Quantity:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_").Replace("/", "_");
                what = dlg.Results[1].Replace(" ", "_").Replace("/", "_");
                string quantity = dlg.Results[2].Replace(" ", "_").Replace("/", "_");

                Main.SendHost(String.Format("!use {0} {1} {2}", who, what, quantity));
            }
        }

        void GainItem_Click(object sender, RoutedEventArgs e)
        {
            int focus = 0;
            if (latchedField0 != "") focus = 1;

            string who = latchedField0;
            string what = latchedField1;

            if (who != "") focus = 2;
            if (who != "" && what != "") focus = 2;

            ManyKey dlg = new ManyKey(focus, "Gain Item", "Who:", who, "What:", what, "Quantity:", "");

            if (dlg.ShowDialog() == true)
            {
                who = dlg.Results[0].Replace(" ", "_").Replace("/", "_");
                what = dlg.Results[1].Replace(" ", "_").Replace("/", "_");
                string quantity = dlg.Results[2].Replace(" ", "_").Replace("/", "_");


                Main.SendHost(String.Format("!gain {0} {1} {2}", who, what, quantity));
            }
        }

        internal void AddManaEtcToMenu(string s, ContextMenu m)
        {
            string prefix = s + "|";
            bool fFirst = true;
            int sum = 0;
            AddDictKeyAndDataToMenu(m, prefix, ref fFirst, manaDict, out sum);
            AddDictKeyAndDataToMenu(m, prefix, ref fFirst, woundsDict, out sum);

            var lifeKey = prefix + "life";
            if (DossierDict.ContainsKey(lifeKey))
            {
                if (sum == 0)
                    AddOneDictItemToMenu(m, ref fFirst, String.Format("life: {0}", DossierDict[lifeKey]));
                else
                    AddOneDictItemToMenu(m, ref fFirst, String.Format("life: {0}, total wounds: {1}", DossierDict[lifeKey], sum));
            }

            fFirst = true;
            AddDictDataToMenu(m, prefix, "buff: ", ref fFirst, buffDict);

            fFirst = true;
            AddDictKeyAndDataToMenu(m, prefix, ref fFirst, usedDict, out sum);
        }

        static void AddDictKeyAndDataToMenu(ContextMenu m, string skipPrefix, ref bool fFirst, Dictionary<string, string> dict, out int sum)
        {
            sum = 0;
            foreach (var k in dict.Keys)
            {
                if (!k.StartsWith(skipPrefix))
                    continue;

                var value = dict[k];
                var key = k.Substring(skipPrefix.Length);

                if (value.StartsWith("damage:"))
                {
                    string s1, s2;
                    MainWindow.Parse2(value, out s1, out s2);
                    
                    int dmg = 0;
                    if (Int32.TryParse(s1.Substring(7), out dmg))
                    {
                        sum += dmg;
                    }
                }

                AddOneDictItemToMenu(m, ref fFirst, key +" "+ value);
            }
        }

        static void AddOneDictItemToMenu(ContextMenu m, ref bool fFirst, string item)
        {
            if (fFirst)
            {
                fFirst = false;
                m.Items.Add(new Separator());
            }

            MenuItem mi = new MenuItem();
            mi.Header = item.Replace("|", " ").Replace("_", " ");
            m.Items.Add(mi);
        }

        static bool AddDictDataToMenu(ContextMenu m, string prefix, string desc, ref bool fFirst, Dictionary<string, string> dict)
        {
            foreach (var k in dict.Keys)
            {
                if (!k.StartsWith(prefix))
                    continue;

                var value = dict[k];
                var key = k.Substring(prefix.Length);

                if (fFirst)
                {
                    fFirst = false;
                    m.Items.Add(new Separator());
                }

                MenuItem mi = new MenuItem();
                mi.Header = (desc + value).Replace("|", " ").Replace("_", " ");
                m.Items.Add(mi);
            }
            return fFirst;
        }

        void Refresh_Click(object sender, RoutedEventArgs e)
        {
            var dirs = Main.AddAllStandardDirs();

            foreach (string dir in dirs)
                Main.SendHost(String.Format("dir {0}", dir));
        }   
    }
}
