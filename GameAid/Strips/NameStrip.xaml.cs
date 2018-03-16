using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for NameStrip.xaml
    /// </summary>
    public partial class NameStrip : UserControl
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        public NameStrip()
        {
            InitializeComponent();
        }

        public UIElementCollection Children { get { return mainGrid.Children; } }

        void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            int row = Grid.GetRow(name);

            int row2 = Main.readyRolls.FindNameAfter(row);

            if (row2 == Int32.MaxValue)
            {
                row2 = Main.readyRolls.MaxRow;
            }

            Main.readyRolls.RemoveRows(row, row2 - row);
        }

        internal void Init(string name)
        {
            this.name.Text = name;
            this.name.FontWeight = FontWeights.Bold; 
        }
    }
}
