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
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for SkillStrip.xaml
    /// </summary>
    public partial class SkillStrip : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        public string Character { get; set; }

        string AdjustedName(string s)
        {
            if (!s.StartsWith("#"))
                return s;

            int i = s.IndexOf(' ');
            if (i > 0)
                return s.Substring(0, i);

            return s;
        }
        
        public UIElementCollection Children { get { return mainGrid.Children; } }

        public SkillStrip()
        {
            InitializeComponent();
        }

        internal void Init(string name, string skill, string sr, string skillPct, string note)
        {
            if (note == null || note == "")
            {
                mainGrid.Children.Remove(this.note);
            }
            else
            {
                this.note.Text = note;
            }

            if (sr != null && sr != "")
            {
                skill += " (SR" + sr + ")";
            }

            Character = name;
            this.skill.Text = skill;
            this.skillPct.Text = skillPct;

            var popupMenu = new ContextMenu();

            for (int i = 1; i <= 10; i++)
            {
                int srx = i;
                MenuItem mi = new MenuItem();
                mi.Header = "Roll on SR" + i.ToString();
                mi.Click += new RoutedEventHandler((x, y) =>
                {
                    if (skillTarget.Text == "")
                        MessageBox.Show("No target text set.  You must set a target to pre-roll.");
                    else
                        Main.readyRolls.PreRoll(srx, () => { roll_Click(null, null); });
                });

                popupMenu.Items.Add(mi);
            }

            buttonRoll.ContextMenu = popupMenu;
        }

        void roll_Click(object sender, RoutedEventArgs e)
        {
            var cmd = "!pct " + skillPct.Text;
            var desc = String.Format("{0} tries {1}", AdjustedName(Character), skill.Text);
            if (this.skillTarget.Text != "")
            {
                desc += " target: " + this.skillTarget.Text;
            }

            Main.SendChat(desc);
            Main.SendHost(cmd);
        }

        void clear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(skill);
            Main.readyRolls.RemoveRows(row, 1);
        }
    }
}
