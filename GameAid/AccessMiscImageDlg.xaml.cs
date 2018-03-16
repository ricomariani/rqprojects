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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for AccessMiscImageDlg.xaml
    /// </summary>
    public partial class AccessMiscImageDlg : Window
    {
        public int ImageWidth = 100;

        public AccessMiscImageDlg()
        {
            InitializeComponent();
            this.m_search.Focus();
        }

        MainWindow Main { get { return MainWindow.mainWindow; } }

        void Fetch_Click(object sender, RoutedEventArgs e)
        {
            DoFetch();
        }

        void DoFetch()
        {
            var text = m_search.Text;

            if (text == null || text.Length == 0)
                return;

            m_results.Children.Clear();

            var p = new StackPanel();
            var t = new TextBlock();
            p.Orientation = Orientation.Vertical;
            p.Children.Add(t);
            t.Text = text;

            var url = text;

            var margin = new Thickness(0, 0, 0, 0);

            Image img = GameMap.CreateImageObject(url, ref margin, width: ImageWidth, tooltip: text);
            p.Children.Add(img);

            m_results.Children.Add(p);
        }

        void m_search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DoFetch();
        }

        void m_cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void m_ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        void m_width_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ChangeWidths();
        }

        void m_width_LostFocus(object sender, RoutedEventArgs e)
        {
            ChangeWidths();
        }

        void ChangeWidths()
        {
            int w = 0;
            if (!Int32.TryParse(m_width.Text, out w))
                return;

            ImageWidth = w;

            for (int i = 0; i < m_results.Children.Count; i++)
            {
                var p = m_results.Children[i] as StackPanel;
                if (p == null || p.Children.Count != 2)
                    continue;

                Image img = p.Children[1] as Image;
                if (img == null) continue;

                img.Width = ImageWidth;
            }
        }
    }
}
