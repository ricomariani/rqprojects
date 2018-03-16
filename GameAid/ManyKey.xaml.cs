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
    public partial class ManyKey : Window
    {
        // this will be the output the clients will read
        public string[] Results;

        // here we hold the controls that were dynamically created for easy access
        List<TextBlock> labels = new List<TextBlock>();
        List<TextBox> blocks = new List<TextBox>();

        public ManyKey(int focus, string title, params string[] args)
        {
            InitializeGeneral(focus, title, args);
        }

        public ManyKey(string title, params string[] args)
        {
            // put focus on the first item
            InitializeGeneral(0, title, args);
        }

        void InitializeGeneral(int focus, string title, params string[] args)
        {
            InitializeComponent();

            // validate arguments
            if (args.Length < 2)
                throw new ArgumentException("at least one field must be specified");

            if (args.Length % 2 == 1)
                throw new ArgumentException("a label and value must be specified for each item");

            this.Title = title;

            this.Owner = GameAid.MainWindow.mainWindow;
            int row = 1;

            // create a new row of label and textbox for each item in the arguments
            // there are three rows in the base resource, two spacer rows and one row for the ok/cancel buttons
            // we start after the spacer row
            for (int i = 0; i < args.Length; i += 2)
            {
                grid.RowDefinitions.Insert(1, new RowDefinition());

                // the label is the even numbered array entry
                var label = new TextBlock();
                Grid.SetColumn(label, 1);
                Grid.SetRow(label, row);
                labels.Add(label);
                grid.Children.Add(label);
                label.Text = args[i];
                label.VerticalAlignment = VerticalAlignment.Center;

                // the starting text is the odd numbered
                var block = new TextBox();
                Grid.SetColumn(block, 3);
                Grid.SetRow(block, row);
                grid.Children.Add(block);
                blocks.Add(block);
                block.TextChanged += block_TextChanged;
                block.Text = args[i + 1];
                block.Margin = new Thickness(0, 5, 0, 0);
                block.VerticalAlignment = VerticalAlignment.Center;

                row++;
            }

            TextBox focusBlock = blocks[focus];
            focusBlock.Focus();
            focusBlock.SelectAll();

            Grid.SetRow(buttonOK, row);
            Grid.SetRow(buttonCancel, row);

            buttonOK.IsEnabled = IsOKEnabled();
        }

        void block_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (buttonOK != null)
            {
                buttonOK.IsEnabled = IsOKEnabled();
            }
        }

        bool IsOKEnabled()
        {

            foreach (var block in blocks)
                if (block.Text.Length == 0)
                    return false;

            return true;
        }

        void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
            this.Results = new string[blocks.Count];

            // one based index in the results for historical reasons
            for (int i = 0; i < blocks.Count; i++)
                this.Results[i] = blocks[i].Text;
        }
    }
}

