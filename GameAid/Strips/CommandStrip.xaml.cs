using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for CommandStrip.xaml
    /// </summary>
    public partial class CommandStrip : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        public string Character { get; set; }

        public UIElementCollection Children { get { return mainGrid.Children; } }

        public CommandStrip()
        {
            InitializeComponent();
        }

        internal void Init(string name, string cmd)
        {
            Character = name;
            this.player.Text = "Who?";
            this.command.Text = cmd;
        }

        void command_Click(object sender, RoutedEventArgs e)
        {
            var cmd = String.Format("!!{0}{1}", player.Text, command.Text);
            Main.SendChat(String.Format("{0} {1}", player.Text, command.Text));
            Main.SendHost(cmd);
        }

        void clear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(player);
            Main.readyRolls.RemoveRows(row, 1);
        }
    }
}
