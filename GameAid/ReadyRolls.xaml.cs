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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for ReadyRolls.xaml
    /// </summary>
    public partial class ReadyRolls : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }
        string defaultLoc = "";

        public ReadyRolls()
        {
            InitializeComponent();

            var s1 = new AttackStrip();
            var loc1 = s1.comboLoc;

            foreach (var el in s1.comboLoc.Items)
            {
                var item = el as ComboBoxItem;
                if (item == null) continue;
                var c = item.Content as string;
                if (c == null) continue;

                if (!c.EndsWith("_missile"))
                {
                    locCombo.Items.Add(c);
                }
            }

            SetComboToValue(locCombo, "humanoid");
        }

        void Clear_Click(object sender, RoutedEventArgs e)
        {
            mainGrid.Children.Clear();
            mainGrid.RowDefinitions.Clear();
        }

        void Convert_to_Squad_Click(object sender, RoutedEventArgs e)
        {
            VirtualSheet.ClearSquad();

            foreach (var name in GetNames())
            {
                VirtualSheet.AddSquadMember(name);
            }

            VirtualSheet.ShowSquad();
        }

        public int MaxRow
        {
            get
            {
                int row = -1;
                foreach (var child in mainGrid.Children)
                {
                    var el = child as UIElement;
                    if (el == null)
                    {
                        continue;
                    }

                    var r = Grid.GetRow(el);
                    if (r > row)
                    {
                        row = r;
                    }
                }

                return row + 1;
            }
        }

        public void RemoveRows(int row, int count)
        {
            var toRemove = new List<UIElement>();

            foreach (var child in mainGrid.Children)
            {
                var el = child as UIElement;
                if (el == null)
                {
                    continue;
                }

                var r = Grid.GetRow(el);
                if (r < row) {
                }
                else if (r < row + count) {
                    toRemove.Add(el);
                }
                else 
                {
                    r -= count;
                    Grid.SetRow(el, r);
                }
            }

            foreach (var child in toRemove)
            {
                mainGrid.Children.Remove(child);
            }

            mainGrid.RowDefinitions.RemoveRange(row, count);

            if (mainGrid.RowDefinitions.Count == 1)
            {
                mainGrid.Children.Clear();
                mainGrid.RowDefinitions.Clear();
            }
        }

        void AddRowDef()
        {
            var rd = new RowDefinition();
            rd.Height = new GridLength(25);
            mainGrid.RowDefinitions.Add(rd);
        }

        void InsertRow(int row)
        {
            AddRowDef();

            foreach (var child in mainGrid.Children)
            {
                var el = child as UIElement;
                if (el == null)
                {
                    continue;
                }

                var r = Grid.GetRow(el);
                if (r >= row) 
                {                    
                    Grid.SetRow(el, r+1);
                }
            }
        }

        void MoveChildren(UIElementCollection origin, int row, string saveKey)
        {
            while (origin.Count > 0)
            {
                var child = origin[0];
                origin.RemoveAt(0);
                Grid.SetRow(child, row);

                Button b = child as Button;

                if (b != null && b.Content is String)
                {
                    if (b.Content.ToString() == "Clear")
                    {
                        Style redButton = MainWindow.mainWindow.FindResource("RedButton") as Style;
                        b.VerticalAlignment = VerticalAlignment.Center;
                        b.HorizontalAlignment = HorizontalAlignment.Left;
                        b.Margin = new Thickness(5, 0, 15, 0);
                        b.Content = "x";
                        b.Height = 15;
                        b.Width = 15;
                        b.LayoutTransform = new ScaleTransform(.75, .75);
                        b.Style = redButton;
                        b.Tag = saveKey;
                    }

                    if (b.Content.ToString() == "Roll")
                    {
                        b.Click += new RoutedEventHandler(
                            (x, y) =>
                            {
                                Style redButton = MainWindow.mainWindow.FindResource("RedButton") as Style;
                                b.Style = redButton;
                            });
                    }
                }

                var t = child as TextBlock;

                if (t != null && saveKey != "" && Grid.GetColumn(t) == 2)
                {
                    var helpKey = saveKey.Replace("\\", "/").Substring(1);
                    var iSlash = helpKey.IndexOf('/');
                    if (iSlash > 0)
                    {
                        var name = helpKey.Substring(0, iSlash);
                        VirtualSheet.MakeHelpRightKey(t, helpKey, name);
                    }
                }

                mainGrid.Children.Add(child);

                var cb = child as ComboBox;

                if (cb == null)
                    continue;

                SetComboDefaultLoc(cb);
            }

            if (saveKey != null && saveKey != "")
            {
                var b = new Button();

                Grid.SetRow(b, row);
                Grid.SetColumn(b, 10);
                b.VerticalAlignment = VerticalAlignment.Center;
                b.HorizontalAlignment = HorizontalAlignment.Left;
                b.Margin = new Thickness(5, 0, 5, 0);
                b.Content = "Check";
                b.Click += new RoutedEventHandler(
                    (object o, RoutedEventArgs args) => {
                        var chk = saveKey.Substring(1).Replace("\\", "/");
                        string name, key;
                        MainWindow.Parse2Ex(chk, out name, out key, '/');
                        string cmd = "!check " + name + " /" + key + "/";
                        MainWindow.mainWindow.SendChat(cmd);
                        MainWindow.mainWindow.SendHost(cmd);
                    });
                mainGrid.Children.Add(b);
            }
        }

        int FindMonsterTag(string name)
        {
            name = "#" + name + " ";
            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                if (Grid.GetColumn(el) != 2)
                {
                    continue;
                }

                int r = Grid.GetRow(el);

                if (el.Text.StartsWith(name))
                {
                    return Grid.GetRow(el);
                }
            }

            return -1;
        }

        void ResetRollButtons()
        {
            Style blueButton = MainWindow.mainWindow.FindResource("BlueButton") as Style;

            foreach (var child in mainGrid.Children)
            {
                Button b = child as Button;

                if (b == null)
                    continue;

                if (!(b.Content is String))
                    continue;

                if (b.Content.ToString() != "Roll")
                    continue;

                b.Style = blueButton;
            }
        }

        int FindNameTag(string name)
        {
            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                if (Grid.GetColumn(el) != 2)
                {
                    continue;
                }

                int r = Grid.GetRow(el);

                if (el.Text == name)
                {
                    return Grid.GetRow(el);
                }
            }

            return -1;
        }

        public IEnumerable<string> GetNames()
        {
            var dict = new Dictionary<string, bool>();
            
            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                if (Grid.GetColumn(el) != 2)
                {
                    continue;
                }

                if (Grid.GetColumnSpan(el) != 7)
                {
                    continue;
                }

                var t = el.Text;

                if (dict.ContainsKey(t))
                    continue;

                dict[t] = true;

                yield return t;
            }
        }
        
        
        public int FindNameAfter(int row)
        {
            int result = Int32.MaxValue;

            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                int r = Grid.GetRow(el);

                if (r <= row)
                {
                    continue;
                }

                if (r >= result)
                {
                    continue;
                }

                if (Grid.GetColumn(el) != 2)
                {
                    continue;
                }

                if (Grid.GetColumnSpan(el) != 7)
                {
                    continue;
                }

                result = r;
            }

            return result;
        }

        public void AddMonsterRollsToMenu(string name, ContextMenu menu)
        {
            int found = FindMonsterTag(name);
            if (found < 0)
                return;

            int end = FindNameAfter(found);

            string desc = FindNameAtRow(found);
            var mi = new MenuItem();
            mi.Header = desc;
            menu.Items.Add(mi);

            AddRollsRangeToMenu(menu, found, end);
        }

        public void AddPlayerRollsToMenu(string name, ContextMenu menu)
        {
            int found = FindNameTag(name);
            if (found < 0)
                return;

            int end = FindNameAfter(found);

            AddRollsRangeToMenu(menu, found, end);
        }

        void AddRollsRangeToMenu(ContextMenu menu, int found, int end)
        {
            bool fFirst = true;

            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                int c = Grid.GetColumn(el);

                if (c != 2)
                    continue;

                int r = Grid.GetRow(el);

                if (r <= found || r >= end)
                {
                    continue;
                }

                Button button = FindRollButton(r);
                if (button == null)
                    continue;

                MenuItem mi = new MenuItem();
                var roll = "Roll " + el.Text.Replace("_", " ");
                if (roll.EndsWith("AP)")) 
                    roll += " parry";

                mi.Header = roll; 
                mi.Click += new RoutedEventHandler(
                    (o, e) =>
                    {
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, this));
                    });

                if (fFirst)
                {
                    fFirst = false;
                    menu.Items.Add(new Separator());
                }

                menu.Items.Add(mi);
            }
        }

        string FindNameAtRow(int row)
        {
            foreach (var child in mainGrid.Children)
            {
                var el = child as TextBlock;
                if (el == null)
                {
                    continue;
                }

                int c = Grid.GetColumn(el);

                if (c != 2)
                    continue;

                int r = Grid.GetRow(el);

                if (r != row)
                {
                    continue;
                }

                return el.Text;
            }

            return "";
        }

        Button FindRollButton(int row)
        {
            foreach (var child in mainGrid.Children)
            {
                var el = child as Button;
                if (el == null)
                {
                    continue;
                }

                int c = Grid.GetColumn(el);

                if (c != 1)
                    continue;

                int r = Grid.GetRow(el);

                if (r != row)
                {
                    continue;
                }

                return el;
            }

            return null;
        }

        public void AppendStripContents(string name, UIElementCollection origin, string saveKey = "")
        {
            int row = MaxRow;

            if (row == 0)
            {
                AddRowDef();
                var header = new HeaderStrip();
                MoveChildren(header.Children, 0, saveKey: "");
                row++;
            }

            int found = FindNameTag(name);

            if (found < 0)
            {
                AddRowDef();
                var namerow = new NameStrip();
                namerow.Init(name);
                MoveChildren(namerow.Children, row++, saveKey: "");

                AddRowDef();
                MoveChildren(origin, row, saveKey);
            }
            else
            {
                int insertion = FindNameAfter(found);

                if (insertion == Int32.MaxValue)
                {
                    AddRowDef();
                    MoveChildren(origin, row, saveKey);
                }
                else
                {
                    AddRowDef();
                    InsertRow(insertion);
                    MoveChildren(origin, insertion, saveKey);
                }
            }
        }

        void Fumble_Melee_Click(object sender, RoutedEventArgs e)
        {
            var cmd = "!fumble";
            Main.SendChat(cmd);
            Main.SendHost(cmd);
        }

        void Fumble_Natural_Click(object sender, RoutedEventArgs e)
        {
            var cmd = "!fumble natural";
            Main.SendChat(cmd);
            Main.SendHost(cmd);
        }

        void Fumble_Missile_Click(object sender, RoutedEventArgs e)
        {
            var cmd = "!fumble missile";
            Main.SendChat(cmd);
            Main.SendHost(cmd);
        }

        void Manual_Click(object sender, RoutedEventArgs e)
        {
            var cmdrow = new CommandStrip();
            cmdrow.Init("Manual  (character and command)", "!pct 95");
            AppendStripContents(cmdrow.Character, cmdrow.Children);
        }

        void LoadRolls_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenSaveDlg(fSave: false);
            dlg.Title = "Load Rolls";

            if (dlg.ShowDialog() != true)
                return;

            string k = dlg.selText.Text;

            if (k.Length == 0)
                return;

            k = k.Replace("/", "_").Replace(" ", "_");

            var loadPath = "_gameaid/_savedRolls/" + k;

            Main.SendHost(String.Format("eval {0}", loadPath));
        }

        List<KeyValuePair<int, Action>> pendingRolls = new List<KeyValuePair<int, Action>>();

        int srLast;
        string roundLast;

        internal void PreRoll(int sr, Action func)
        {
            // store the action and run it on the given SR
            var pair = new KeyValuePair<int, Action>(sr, func);
            pendingRolls.Add(pair);
            RunPendingRolls();
        }

        internal void RunPendingRolls()
        {
            for (int i = 0; i < pendingRolls.Count; i++)
            {
                var pair = pendingRolls[i];
                if (pair.Key > srLast)
                    continue;

                var desc = String.Format("preroll for SR{0}", pair.Key);
                Main.SendChat(desc);
                pair.Value();
                pendingRolls.RemoveAt(i);
                i--;
            }
        }

        public void Consider(DictBundle b)
        {
            if (b.dict == null)
            {
                return;
            }

            if (b.path == "_gameaid/_remote")
            {
                string srKey = Main.mychannel + "|sr";
                string roundKey = Main.mychannel + "|round";

                int sr = 0;
                if (b.dict.ContainsKey(srKey) && Int32.TryParse(b.dict[srKey], out sr))
                {
                    srLast = sr;
                    RunPendingRolls();
                }

                string round = "";
                if (b.dict.ContainsKey(roundKey))
                {
                    round = b.dict[roundKey];
                    if (round != roundLast)
                    {
                        roundLast = round;
                        ResetRollButtons();
                    }
                }


                if (Main.gm_mode)
                    return;

                string locKey = Main.mychannel + "|loc";

                if (!b.dict.ContainsKey(locKey) || b.dict[locKey] == "")
                    return;

                var newLoc = b.dict[locKey].ToLower();

                if (newLoc == defaultLoc)
                    return;

                defaultLoc = newLoc;

                string regLoc, missileLoc;

                if (defaultLoc.Contains("_missile"))
                {
                    regLoc = defaultLoc.Replace("_missile", "");
                    missileLoc = defaultLoc;
                }
                else 
                {
                    regLoc = defaultLoc;
                    missileLoc = defaultLoc + "_missile";
                }

                SetComboToValue(locCombo, regLoc);

                foreach (var ch in mainGrid.Children)
                {
                    var cb = ch as ComboBox;

                    if (cb == null)
                        continue;

                    SetComboDefaultLoc(cb);
                }
            }
        }

        public void Consider(EvalBundle b)
        {
            if (b.dict == null)
            {
                return;
            }

            if (!b.dict.ContainsKey("purpose") || b.dict["purpose"] != "readyrolls")
            {
                return;
            }

            mainGrid.Children.Clear();
            mainGrid.RowDefinitions.Clear();

            var q = from el in b.dict.Keys
                    orderby el
                    select el;

            foreach (string k in q)
            {
                string v = b.dict[k];

                if (k.Length < 3)
                {
                    continue;
                }

                if (!k.StartsWith("\\"))
                {
                    continue;
                }

                int n = k.IndexOf('\\', 1);

                if (n < 2)
                {
                    continue;
                }

                string name = k.Substring(1, n - 1);

                string wpn = "";

                int w = k.IndexOf("\\_wpn\\");

                if (w > 0)
                {
                    wpn = k.Substring(w+6);
                    w = wpn.IndexOf('\\');
                    if (w > 0)
                    {
                        wpn = wpn.Substring(0, w);
                    }
                    else
                    {
                        wpn = "";
                    }
                }

                int t = k.LastIndexOf('\\');
                if (t < 0)
                {
                    continue;
                }

                string tail = k.Substring(t + 1);

                if (k.EndsWith("\\parry") && wpn != "")
                {
                    var parryStrip = new ParryStrip();
                    var apKey = k.Replace("\\parry", "\\ap");
                    if (!b.dict.ContainsKey(apKey))
                    {
                        continue;
                    }

                    var parryAP = b.dict[apKey];
                    var parryPct = b.dict[k];

                    parryStrip.Init(name, parryChoice: wpn, ap: parryAP, parryPct: parryPct);
                    AppendStripContents(name, parryStrip.Children, saveKey: k);
                }
                else if (k.EndsWith("\\attack") && wpn != "")
                {
                    var srKey = k.Replace("\\attack", "\\sr");
                    if (!b.dict.ContainsKey(srKey))
                    {
                        continue;
                    }
                    var dmgKey = k.Replace("\\attack", "\\dmg");
                    if (!b.dict.ContainsKey(dmgKey))
                    {
                        continue;
                    }

                    var sr = b.dict[srKey];
                    var pct = v;
                    var dmg = b.dict[dmgKey];

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                    AppendStripContents(name, attackStrip.Children, saveKey: k);
                }
                else if (wpn == "") switch (tail)
                {
                    case "STR":
                    case "CON":
                    case "SIZ":
                    case "INT":
                    case "POW":
                    case "DEX":
                    case "APP":
                        string stat = tail;
                        var powStrip = new PowStrip();
                        powStrip.Init(name: name, stat: stat + " vs. " + stat, value: v);
                        AppendStripContents(name, powStrip.Children, saveKey: k);
                        break;

                    default:
                        string skill = tail;

                        var skillStrip = new SkillStrip();
                        skillStrip.Init(name: name, skill: skill, sr: null, skillPct: v, note: null);
                        AppendStripContents(name, skillStrip.Children, saveKey: k);
                        break;
                }
            }

        }

        void SetComboDefaultLoc(ComboBox cb)
        {
            if (defaultLoc == null || defaultLoc == "")
                return;

            SetComboToValue(cb, defaultLoc);
        }

        void SetComboToValue(ComboBox cb, string value)
        {
            int idx = 0;
            for (idx = 0; idx < cb.Items.Count; idx++)
            {
                var item = cb.Items[idx] as ComboBoxItem;
                if (item == null)
                {
                    var s1 = cb.Items[idx] as string;
                    if (s1 == null) continue;
                    if (s1 == value)
                        break;

                    continue;
                }

                var s = item.Content as String;

                if (s == null)
                    continue;

                if (s == value)
                    break;
            }

            if (idx < cb.Items.Count)
            {
                cb.SelectedItem = idx;
                cb.Text = value;
            }
        }

        void SaveRolls_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenSaveDlg(fSave: true);
            dlg.Title = "Save Rolls";

            if (dlg.ShowDialog() != true)
                return;

            string k = dlg.selText.Text;

            if (k.Length == 0)
                return;

            k = k.Replace("/", "_").Replace(" ", "_");

            var savePath = "_gameaid/_savedRolls/" + k;

            Main.SendHost(String.Format("del {0}", savePath));
            Main.SendHost(String.Format("n {0} {1} {2}", savePath, "purpose", "readyrolls"));

            foreach (var child in mainGrid.Children)
            {
                var el = child as Button;
                if (el == null)
                {
                    continue;
                }

                if (Grid.GetColumn(el) != 0)
                {
                    continue;
                }

                var tag = el.Tag as string;

                if (tag == null || !tag.StartsWith("\\"))
                {
                    continue;
                }

                Main.SendHost(String.Format("n {0} {1} {2}", savePath, tag, "<eval>"));

                if (tag.EndsWith("\\attack"))
                {
                    Main.SendHost(String.Format("n {0} {1} {2}", savePath, tag.Replace("\\attack", "\\dmg"), "<eval>"));
                    Main.SendHost(String.Format("n {0} {1} {2}", savePath, tag.Replace("\\attack", "\\sr"), "<eval>"));
                }

                if (tag.EndsWith("\\parry"))
                {
                    Main.SendHost(String.Format("n {0} {1} {2}", savePath, tag.Replace("\\parry", "\\ap"), "<eval>"));
                }
            }
        }

        void Loc_Click(object sender, RoutedEventArgs e)
        {
            var locText = locCombo.Text;
            if (String.IsNullOrEmpty(locText))
                return;

            Main.SendChat(String.Format("melee location roll"));
            Main.SendHost(String.Format("!loc {0}", locText));
        }

        void Loc_Missile_Click(object sender, RoutedEventArgs e)
        {
            var locText = locCombo.Text;
            if (String.IsNullOrEmpty(locText))
                return;

            locText += "_missile";

            Main.SendChat(String.Format("missile location roll"));
            Main.SendHost(String.Format("!loc {0}", locText));
        }

        void Loc_SetDefault_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            var locText = item.Header as string;
            if (String.IsNullOrEmpty(locText))
                return;

            locText = locText.Replace("Set Default Location: ", "");
            locText = locText.Replace("__", "_");
            Main.SendChat(String.Format("!gameaid loc {0}", locText));
            Main.SendHost(String.Format("!gameaid loc {0}", locText));
        }

        void Reset_Click(object sender, RoutedEventArgs e)
        {
            ResetRollButtons();
        }

        void Manage_ContextMenuOpening(object sender, RoutedEventArgs e)
        {
            var loc = locCombo.Text;

            for (int i = 0; i < m_manage.Items.Count; i++)
            {
                var item = m_manage.Items[i] as MenuItem;
                if (item == null) continue;
                var locText = item.Header as string;
                if (locText == null) continue;
                locText = locText.Replace("_", "__");

                if (locText.StartsWith("Set Default Location:"))
                {
                    item.Header = "Set Default Location: " + loc;
                    item = m_manage.Items[i+1] as MenuItem;
                    if (item != null)
                        item.Header = "Set Default Location: " + loc + "__missile";
                    return;
                }
            }
        }  
    }
}
