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
    /// Interaction logic for AccessLibraryDlg.xaml
    /// </summary>
    public partial class AccessLibraryDlg : Window
    {
        public int ImageWidth = 100;
        bool _fFiles;
        List<string> _fileList;

        public AccessLibraryDlg(bool fFiles)
        {
            _fFiles = fFiles;
            InitializeComponent();
            this.m_search.Focus();

            if (_fFiles)
            {
                _fileList = new List<string>();
                Main.SendHost("download-dir gameaid");
            }
        }

        MainWindow Main { get { return MainWindow.mainWindow; } }

        void Search_Click(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        void DoSearch()
        {
            var text = m_search.Text;

            if (text == null || text.Length == 0)
                return;

            text = text.Trim().ToLower();

            if (_fFiles)
            {
                m_results.Items.Clear();

                if (_fileList == null)
                    return;

                var matches = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var val in _fileList)
                {
                    var v = val.ToLower();

                    if (!MatchAll(v, matches))
                        continue;

                    var url = "http://yourserver.com/uploads/gameaid/" + val;
                    AddImage(val, url);
                }
            }
            else
            {
                Main.SendHost(String.Format("library {0}", text));
            }
        }

        static bool MatchAll(string val, string[] matches)
        {
            int i;
            for (i = 0; i < matches.Length; i++)
            {
                if (!val.Contains(matches[i]))
                    return false;
            }

            return true;
        }

        internal void Consider(DictBundle b)
        {
            m_results.Items.Clear();

            if (!_fFiles)
                return;

            if (b.path != "download-result")
                return;

            if (b.dict == null || !b.dict.ContainsKey("folder"))
                return;

            if (b.dict["folder"] != "gameaid")
                return;

            _fileList.Clear();

            for (int i = 0; i < b.dict.Count; i++)
            {
                string key = i.ToString();
                if (b.dict.ContainsKey(key))
                {
                    _fileList.Add(b.dict[key]);
                }
            }

            _fileList.Sort(StringComparer.InvariantCultureIgnoreCase);

            foreach (var val in _fileList)
            {
                var url = "http://yourserver.com/uploads/gameaid/" + val;
                AddImage(val, url);
            }
        }

        internal void Consider(EvalBundle b)
        {
            if (_fFiles)
                return;

            m_results.Items.Clear();

            for (int i = 0; i < b.dict.Count; i++)
            {
                string key = i.ToString();
                string val;

                if (!b.dict.TryGetValue(key, out val))
                    continue;

                var url = "http://yourserver.com/uploads/Mapping/" + val;
                AddImage(val, url);
            }
        }

        void AddImage(string val, string url)
        {
            var p = new StackPanel();
            var t = new TextBlock();
            p.Orientation = Orientation.Vertical;
            p.Children.Add(t);
            t.Text = val;

            var margin = new Thickness(0, 0, 0, 0);

            Image img = GameMap.CreateImageObject(url, ref margin, width: ImageWidth, tooltip: val);
            p.Children.Add(img);

            m_results.Items.Add(p);
        }

        void m_search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DoSearch();
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

            for (int i = 0; i < m_results.Items.Count; i++)
            {
                var p = m_results.Items[i] as StackPanel;
                if (p == null || p.Children.Count != 2)
                    continue;

                Image img = p.Children[1] as Image;
                if (img == null) continue;

                img.Width = ImageWidth;

            }
        }

        internal string GetLibraryResult()
        {
            Main.accessLibraryDlg = this;
            bool? b = ShowDialog();
            Main.accessLibraryDlg = null;

            if (b != true)
                return null;

            int i = m_results.SelectedIndex;

            if (i < 0)
                return null;

            var panel = m_results.Items[i] as StackPanel;

            if (panel == null || panel.Children.Count < 2)
                return null;

            var textBlock = panel.Children[0] as TextBlock;

            if (textBlock == null)
                return null;

            return textBlock.Text;
        }
    }
}
