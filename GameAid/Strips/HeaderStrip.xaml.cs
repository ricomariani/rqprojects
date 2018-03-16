using System;
using System.Windows;
using System.Windows.Controls;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for HeaderStrip.xaml
    /// </summary>
    public partial class HeaderStrip : UserControl
    {
        public HeaderStrip()
        {
            InitializeComponent();
        }

        public UIElementCollection Children { get { return mainGrid.Children; } }
    }
}
