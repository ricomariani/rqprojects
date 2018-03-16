using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for ParryStrip.xaml
    /// </summary>
    public partial class ParryStrip : UserControl
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

        public ParryStrip()
        {
            InitializeComponent();
        }

        internal void Init(string name, string parryChoice, string ap, string parryPct)
        {
            Character = name;

            if (ap != null && ap != "")
            {
                parryChoice += " (" + ap + "AP)";
            }

            this.parryChoice.Text = parryChoice;
            this.parryPct.Text = parryPct;
        }

        void buttonParry_Click(object sender, RoutedEventArgs e)
        {
            var cmd = "!pct " + parryPct.Text;

            if (parryTarget.Text != "")
            {
                Main.SendChat(String.Format("{0}, {1} parry target: {2}", AdjustedName(Character), parryChoice.Text, parryTarget.Text));
            }
            else
            {
                Main.SendChat(String.Format("{0}, {1} parry", AdjustedName(Character), parryChoice.Text));
            }
            Main.SendHost(cmd);
        }

        void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(parryChoice);
            Main.readyRolls.RemoveRows(row, 1);
        }
    }
}
