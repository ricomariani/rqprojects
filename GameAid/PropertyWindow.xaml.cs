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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FontDialogSample;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for PropertyWindow.xaml
    /// </summary>
    public partial class PropertyWindow : Window
    {
        public PropertyWindow()
        {
            InitializeComponent();
            this.Owner = MainWindow.mainWindow;
            ClearRows();
        }

        internal void InitFromContext()
        {
            var map = MainWindow.mainWindow.map1;
            if (map != null)
            {
                map.SetPropertiesForHandleTarget();
            }
        }

        public void ClearRows()
        {
            lb.Items.Clear();
            this.Title = "Properties: No Selection";
            fillRect = null;
            strokeRect = null;
        }

        public void AddStringProperty(string k, string v, Action<string> func)
        {
            var t = AddRow(k,v);
            t.PreviewKeyDown += new KeyEventHandler(StringField_KeyDown);
            t.Tag = func;
        }

        public void AddBooleanProperty(string k, bool v, Action<bool> func)
        {
            var t = AddRow(k, v.ToString());
            t.PreviewKeyDown += new KeyEventHandler(BoolField_KeyDown);

            if (func == null)
                t.IsEnabled = false;

            t.Tag = func;
        }

        public void AddDoubleProperty(string k, double v, Action<double> func)
        {
            var t = AddRow(k, v.ToString());
            t.PreviewKeyDown += new KeyEventHandler(DoubleField_KeyDown);

            if (func == null)
                t.IsEnabled = false;

            if (Double.IsNaN(v) || Double.IsNegativeInfinity(v) || Double.IsPositiveInfinity(v))
                t.IsEnabled = false;

            t.Tag = func;
        }

        public void AddFontProperty(string k, TextBlock text, Action<TextBlock> func)
        {
            var button = AddFontRow(k, text);
            button.Click += new RoutedEventHandler(font_Click);
            button.Tag = func;
        }

        string GetFontName(TextBlock t)
        {
            return t.FontFamily.ToString() + " " + (t.FontSize / 1.333333333333333).ToString();
        }

        void font_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
                return;

            TextBlock t = button.Content as TextBlock;

            if (t == null)
                return;

            Action<TextBlock> func = button.Tag as Action<TextBlock>;

            if (func == null)
                return;

            var dlg = new FontChooser();
            dlg.Owner = MainWindow.mainWindow;

            dlg.SetPropertiesFromObject(t);
            dlg.PreviewSampleText = "The quick brown fox jumps over the lazy white dog.";

            if (dlg.ShowDialog() == true)
            {
                dlg.ApplyPropertiesToObject(t);

                t.Text = GetFontName(t);

                func(t);
            }
        }

        public void AddBrushProperty(string k, Brush b, Action<Brush> func)
        {
            if (b == null)
            {
                b = new SolidColorBrush(Colors.Transparent);
            }

            var swatch = AddBrushRow(k, b);           
            swatch.MouseDown += new MouseButtonEventHandler(Swatch_MouseDown);
            swatch.Tag = func;
        }

        void Swatch_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                return;
            }

            Rectangle rect = sender as Rectangle;

            Action<Brush> func = rect.Tag as Action<Brush>;

            if (func == null)
                return;

            var dlg = new ColorPickerDialog();

            if (rect.Fill is SolidColorBrush)
                dlg.StartingColor = (rect.Fill as SolidColorBrush).Color;
            else
                dlg.StartingColor = Colors.White;

            if (dlg.ShowDialog() == true)
            {
                rect.Fill = new SolidColorBrush(dlg.SelectedColor);
                func(rect.Fill);
            }
        }

        Rectangle fillRect = null;
        Rectangle strokeRect = null;

        Rectangle AddBrushRow(string k, Brush b)
        {
            TextBlock b1 = new TextBlock();
            b1.Text = k;
            b1.Width = 150;
            b1.HorizontalAlignment = HorizontalAlignment.Left;
            b1.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(b1, 0);
            Grid.SetColumn(b1, 0);

            Rectangle b2 = new Rectangle();
            b2.Fill = b;
            b2.VerticalAlignment = VerticalAlignment.Center;
            b2.HorizontalAlignment = HorizontalAlignment.Left;
            b2.Width = 50;
            b2.Height = 20;
            b2.Stroke = new SolidColorBrush(Colors.Black);
            b2.StrokeThickness = 1;

            if (k == "Background" || k == "Fill")
            {
                b2.ContextMenu = this.FindResource("BrushMenu") as ContextMenu;
                fillRect = b2;
            }

            if (k == "Foreground" || k == "Stroke")
            {
                b2.ContextMenu = this.FindResource("StrokeMenu") as ContextMenu;
                strokeRect = b2;
            }

            Grid.SetRow(b2, 0);
            Grid.SetColumn(b2, 1);

            ColumnDefinition colDef1 = new ColumnDefinition();
            colDef1.MinWidth = 150;

            ColumnDefinition colDef2 = new ColumnDefinition();
            colDef2.MinWidth = 150;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(colDef1);
            g.ColumnDefinitions.Add(colDef2);
            g.Children.Add(b1);
            g.Children.Add(b2);

            lb.Items.Add(g);

            return b2;
        }

        Button AddFontRow(string k, TextBlock text)
        {
            TextBlock b1 = new TextBlock();
            b1.Text = k;
            b1.Width = 150;
            b1.HorizontalAlignment = HorizontalAlignment.Left;
            b1.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(b1, 0);
            Grid.SetColumn(b1, 0);

            Button b2 = new Button();
            b2.HorizontalAlignment = HorizontalAlignment.Left;
            b2.VerticalAlignment = VerticalAlignment.Center;
            b2.MinWidth = 100;

            TextBlock b3 = new TextBlock();
            b3.Text = GetFontName(text);
            b2.Content = b3;

            FontChooser.TransferFontProperties(text, b3);

            Grid.SetRow(b2, 0);
            Grid.SetColumn(b2, 1);

            ColumnDefinition colDef1 = new ColumnDefinition();
            colDef1.MinWidth = 150;

            ColumnDefinition colDef2 = new ColumnDefinition();
            colDef2.MinWidth = 150;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(colDef1);
            g.ColumnDefinitions.Add(colDef2);
            g.Children.Add(b1);
            g.Children.Add(b2);

            lb.Items.Add(g);

            return b2;
        }
        
        TextBox AddRow(string k, string v)
        {
            TextBlock b1 = new TextBlock();
            b1.Text = k;
            b1.Width = 150;
            b1.HorizontalAlignment = HorizontalAlignment.Left;
            b1.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(b1, 0);
            Grid.SetColumn(b1, 0);

            TextBox b2 = new TextBox();
            b2.Text = v;
            b2.HorizontalAlignment = HorizontalAlignment.Left;
            b2.VerticalAlignment = VerticalAlignment.Center;
            b2.MinWidth = 150;

            Grid.SetRow(b2, 0);
            Grid.SetColumn(b2, 1);

            ColumnDefinition colDef1 = new ColumnDefinition();
            colDef1.MinWidth = 150;

            ColumnDefinition colDef2 = new ColumnDefinition();
            colDef2.MinWidth = 150;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(colDef1);
            g.ColumnDefinitions.Add(colDef2);
            g.Children.Add(b1);
            g.Children.Add(b2);

            lb.Items.Add(g);

            return b2;
        }

        void StringField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Tab)
                return;

            if (e.Key == Key.Enter)
                e.Handled = true;

            DispatchStringAction(sender);
        }

        void BoolField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Tab)
                return;

            if (e.Key == Key.Enter)
                e.Handled = true;

            DispatchBoolAction(sender);
        }

        void DoubleField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Tab)
                return;

            if (e.Key == Key.Enter)
                e.Handled = true;

            DispatchDoubleAction(sender);
        }

        static void DispatchStringAction(object sender)
        {
            TextBox b = sender as TextBox;
            if (b == null)
                return;

            Action<string> func = b.Tag as Action<string>;

            if (func == null)
                return;

            func(b.Text);
        }
        
        static void DispatchBoolAction(object sender)
        {
            TextBox b = sender as TextBox;
            if (b == null)
                return;

            bool value;

            if (!Boolean.TryParse(b.Text, out value))
                return;

            Action<bool> func = b.Tag as Action<bool>;

            if (func == null)
                return;

            func(value);
        }

        static void DispatchDoubleAction(object sender)
        {
            TextBox b = sender as TextBox;
            if (b == null)
                return;

            double value;

            if (!Double.TryParse(b.Text, out value))
                return;

            Action<double> func = b.Tag as Action<double>;

            if (func == null)
                return;

            func(value);
        }

        void NoChange_Click(object sender, RoutedEventArgs e)
        {
            // do nothing
        }

        void SendBrushResource(Brush brush)
        {
            Rectangle r = fillRect;
            if (r == null)
                return;

            Action<Brush> func = r.Tag as Action<Brush>;

            if (func == null)
                return;

            fillRect.Fill = brush;
            func(brush);
        }

        void Brush_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as Rectangle;

            SendBrushResource(r.Fill);
        }

        void NoBackground_Click(object sender, RoutedEventArgs e)
        {
            Rectangle r = fillRect;
            if (r == null)
                return;

            Action<Brush> func = r.Tag as Action<Brush>;

            if (func == null)
                return;

            r.Fill = new SolidColorBrush(Colors.Transparent);
            func(null);
        }


        void SendStrokeResource(Brush brush)
        {
            Rectangle r = strokeRect;
            if (r == null)
                return;

            Action<Brush> func = r.Tag as Action<Brush>;

            if (func == null)
                return;

            strokeRect.Fill = brush;
            func(brush);
        }


        void Stroke_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            var sp = m.Header as StackPanel;

            if (sp == null)
                return;

            var r = sp.Children[1] as Rectangle;

            SendStrokeResource(r.Fill);
        }

        void NoStroke_Click(object sender, RoutedEventArgs e)
        {
            Rectangle r = strokeRect;
            if (r == null)
                return;

            Action<Brush> func = r.Tag as Action<Brush>;

            if (func == null)
                return;

            r.Fill = new SolidColorBrush(Colors.Transparent);
            func(null);
        }
    }
}
