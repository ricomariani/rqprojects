using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for PowStrip.xaml
    /// </summary>
    public partial class PowStrip : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        string Character { get; set; }

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

        public PowStrip()
        {
            InitializeComponent();
        }

        internal void Init(string name, string stat, string value)
        {
            Character = name;
            this.skill.Text = stat;
            this.skillValue.Text = value;
        }

        void skill_Click(object sender, RoutedEventArgs e)
        {
            string cmd;

            if (skillValue.Text.Contains("%"))
            {
                cmd = "!roll " + skillValue.Text;
            }
            else if (skillValue.Text.Contains("*"))
            {
                cmd = "!pct " + skillValue.Text;
            }
            else
            {
                cmd = "!pow " + skillValue.Text;
            }

            Main.SendChat(String.Format("{0} tries {1}", AdjustedName(Character), skill.Text));
            Main.SendHost(cmd);
        }

        void clear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(skill);
            Main.readyRolls.RemoveRows(row, 1);
        }

        void skillValue_KeyNotify(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var stat = skill.Text.Substring(0, 3);

            if (skillValue.Text.Contains("%") || skillValue.Text.Contains("*"))
            {
                this.skill.Text = stat + " test";
            }
            else
            {
                this.skill.Text = stat + " vs. " + stat;
            }
        }
    }
}
