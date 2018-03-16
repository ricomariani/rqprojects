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
    /// Interaction logic for ParseChargen.xaml
    /// </summary>
    public partial class ParseChargen : Window
    {
        MainWindow Main { get { return MainWindow.mainWindow; } }

        public System.Windows.Media.Brush fillBrush;
        public string ImageName;

        public ParseChargen()
        {
            InitializeComponent();
            data.SelectAll();
            data.Focus();

            fillBrush = fillRect.Fill;
        }

        void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        void Rectangle_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
                return;

            fillRect.ContextMenu.IsOpen = true;
        }

        void Disable_Click(object sender, RoutedEventArgs e)
        {
            fillBrush = null;
            fillRect.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkSlateGray);
            fillLabel.Text = "No Tiles";
        }

        void Fill_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as System.Windows.Shapes.Rectangle;

            fillBrush = r.Fill;
            fillRect.Fill = r.Fill;
            fillLabel.Text = "Color:";
        }

        void Access_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AccessLibraryDlg(fFiles:false);

            string textBase = dlg.GetLibraryResult();
            if (textBase == null)
                return;
           
            Thickness margin = new Thickness(0, 0, 0, 0);

            var text = "http://yourserver.com/uploads/Mapping/" + textBase;

            ImageName = text;
            Image img = GameMap.CreateImageObject(text, ref margin, 18, text);
            m_preview.Children.Clear();
            m_preview.Children.Add(img);
        }

        void Clear_Click(object sender, RoutedEventArgs e)
        {
            m_preview.Children.Clear();
            ImageName = null;
        }
    }
}
