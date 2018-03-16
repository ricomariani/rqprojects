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
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace GameAid
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MiniIRC : UserControl
    {
        Style redStyle;
        Style blueStyle;
        MainWindow Main { get { return MainWindow.mainWindow; } }
        
        public MiniIRC()
        {
            InitializeComponent();
        }

        public const int DoText = 1;
        public const int DoClearNames = 2;
        public const int DoAddName = 3;
        public const int DoRemoveName = 4;
        public const int DoTopic = 5;
        public const int DoNewNick = 6;
        public const int DoPopup = 7;

        List<string> history = new List<string>();
        int historyindex = 0;

        void ircInput_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        string t = ircInput.Text;
                        ircInput.Text = "";
                        string logtext = "";
                        if (t.StartsWith(":"))
                        {
                            string msg = t.Substring(1);
                            Main.SendIrcEmote(msg);
                            logtext = Main.nick + " " + msg;
                            
                        }
                        else if (t.StartsWith("/em "))
                        {
                            string msg = t.Substring(4);
                            Main.SendIrcEmote(msg);
                            logtext = Main.nick + " " + msg;
                        }
                        else if (t.StartsWith("/me "))
                        {
                            string msg = t.Substring(4);
                            Main.SendIrcEmote(msg);
                            logtext = Main.nick + " " + msg;
                        }
                        else if (t.StartsWith("/nick "))
                        {
                            string nick = t.Substring(6);
                            Main.SendIrc("nick " + nick);
                        }
                        else if (t.StartsWith("/join "))
                        {
                            string channel = t.Substring(6);
                            if (channel.StartsWith("#"))
                            {
                                Main.SendIrc("join " + channel);
                                // Main.SendHost("room " + channel);
                            }
                            SwitchToRoom(channel);
                        }
                        else if (t.StartsWith("/bot "))
                        {
                            string cmd = t.Substring(5);
                            Main.SendHost(cmd);
                        }
                        else
                        {
                            Main.SendIrcChat(t);
                            logtext = "<" + Main.nick + "> " + t;
                        }

                        if (logtext != "")
                        {
                            ircHistory.Text = ircHistory.Text  + logtext + "\r\n";
                            ircHistory.ScrollToEnd();
                        }

                        historyindex = 0;

                        if (history.Count == 0 || history[0] != t)
                            history.Insert(0, t);

                        if (history.Count > 50)
                            history.RemoveAt(50);
                    }
                    break;
            }           
        }

        void ircInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    if (historyindex < history.Count)
                    {
                        ircInput.Text = history[historyindex];
                        historyindex ++;
                        ircInput.SelectAll();
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (historyindex > 0)
                    {
                        historyindex--;
                        ircInput.Text = history[historyindex];
                        ircInput.SelectAll();
                    }
                    e.Handled = true;
                    break;
            }        
        }

        public class RoomData
        {
            public StringBuilder Text;
            public string Topic;
            public Dictionary<string, bool> Occupants;

            public RoomData()
            {
                Text = new StringBuilder();
                Occupants = new Dictionary<string, bool>();
                Topic = "";
            }
        }

        Dictionary<string, RoomData> roomData = new Dictionary<string, RoomData>();

        public void OnIrcEvent(object sender, ProgressChangedEventArgs e)
        {
            if (redStyle == null)
                redStyle = Main.FindResource("RedButton") as Style;

            if (blueStyle == null)
                blueStyle = Main.FindResource("BlueButton") as Style;

            string message;

            if (e.UserState is Exception)
            {
                Exception ex = e.UserState as Exception;

                message = ex.ToString() + "\r\n" + ex.StackTrace.ToString();

                ircHistory.Text = ircHistory.Text + message + "\r\n";
                ircHistory.ScrollToEnd();
                return;
            }

            if (!(e.UserState is KeyValuePair<string, string>))
                return;

            var pair = (KeyValuePair<string, string>)e.UserState;

            message = pair.Value;
            var target = pair.Key;

            if (target == "*")
            {
                target = Main.botchannel;
            }

            switch (e.ProgressPercentage)
            {
                case MiniIRC.DoPopup:
                    MainWindow.mainWindow.PopupString(target);
                    return;

                case MiniIRC.DoNewNick:
                    RenameButton(target, message);

                    foreach (var t in roomData.Keys)
                    {
                        if (roomData[t].Occupants.ContainsKey(target))
                        {
                            roomData[t].Occupants.Remove(target);
                            roomData[t].Occupants[message] = true;
                        }
                    }

                    // old name
                    ircNames.Items.Remove(target);

                    // new name
                    AddName(message);
                    return;

                case MiniIRC.DoText:
                    if (target != Main.mychannel)
                    {
                        EnsureRoomReady(target);
                        HighlightButton(target);
                        roomData[target].Text.AppendLine(message);
                    }
                    break;

                case MiniIRC.DoTopic:
                    EnsureRoomReady(target);
                    roomData[target].Topic = message;
                    break;
                     
                case MiniIRC.DoAddName:
                    EnsureRoomReady(target);
                    roomData[target].Occupants[message] = true;
                    break;

                case MiniIRC.DoClearNames:
                    EnsureRoomReady(target);
                    roomData[target].Occupants.Clear();
                    break;

                case MiniIRC.DoRemoveName:
                    EnsureRoomReady(target);
                    roomData[target].Occupants.Remove(message);
                    break;
            }

            // what follows should only happen if the channel we're changing is the one we're viewing.

            if (target != Main.mychannel)
                return;

            switch (e.ProgressPercentage)
            {
                case MiniIRC.DoText:
                    ircHistory.Text = ircHistory.Text + message + "\r\n";
                    ircHistory.ScrollToEnd();
                    break;

                case MiniIRC.DoTopic:
                    ircTopic.Text = message;
                    break;

                case MiniIRC.DoClearNames:
                    ircNames.Items.Clear();
                    break;

                case MiniIRC.DoAddName:
                    AddName(message);
                    break;

                case MiniIRC.DoRemoveName:
                    ircNames.Items.Remove(message);
                    break;
            }
        }

        void AddName(string message)
        {
            int i = 0;
            var items = ircNames.Items;
            for (i = 0; i < items.Count; i++)
            {
                if (String.Compare(items[i].ToString(), message, true) >= 0)
                    break;
            }

            items.Insert(i, message);
        }

        void EnsureRoomReady(string target)
        {
            if (target == "*")
                return;

            if (!roomData.ContainsKey(target))
            {
                roomData[target] = new RoomData();
                var textblock = new TextBlock();

                textblock.Text = target;
                var button = new Button();
                button.Content = textblock;
                button.VerticalAlignment = VerticalAlignment.Center;
                button.Margin = new Thickness(0, 0, 5, 0);
                button.Click += new RoutedEventHandler(buttonChannelChange_Click);

                button.PreviewMouseRightButtonDown += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e) => SuppressMenu(button, e));
                button.PreviewMouseRightButtonUp += new MouseButtonEventHandler((object sender, MouseButtonEventArgs e) => SuppressMenu(button, e));

                ircToolbar.Children.Add(button);

                if (target == Main.mychannel)
                {
                    button.LayoutTransform = new System.Windows.Media.ScaleTransform(1.25, 1.25);
                    textblock.FontWeight = FontWeights.Bold;
                }
                else
                {
                    button.LayoutTransform = new System.Windows.Media.ScaleTransform(.85, .85);
                    textblock.FontWeight = FontWeights.Normal;
                }

                if (target != "System")
                {
                    var menu = new ContextMenu();
                    var menuItem = new MenuItem();
                    var textBlock = new TextBlock();
                    textBlock.Text = "Close";
                    menuItem.Header = textBlock;
                    menu.Items.Add(menuItem);
                    menuItem.Click += new RoutedEventHandler((x, y) => { Button_CloseMenu(button); });
                    button.ContextMenu = menu;
                }
            }
        }

        void SuppressMenu(Button button, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                TextBlock textBlock = (TextBlock)button.Content;
                var channel = textBlock.Text;

                if (channel == Main.botchannel)
                    e.Handled = true;
            }
        }

        void Button_CloseMenu(Button button)
        {
            TextBlock textBlock = (TextBlock)button.Content;
            var channel = textBlock.Text;

            if (channel == Main.botchannel)
                return;

            SwitchToRoom("System");

            if (channel.StartsWith("#"))
            {
                Main.SendIrc("part " + channel);
            }

            ircToolbar.Children.Remove(button);

            if (roomData.ContainsKey(channel))
            {
                roomData.Remove(channel);
            }
        }

        void buttonChannelChange_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            TextBlock textBlock = (TextBlock)button.Content;
            var channel = textBlock.Text;

            if (channel == Main.mychannel)
                return;

            SwitchToRoom(channel);
            ircInput.Focus();

            if (channel.StartsWith("#"))
            {
                Main.SendHost("room " + channel);
            }
        }

        public void SwitchToRoom(string channel)
        {
            if (!roomData.ContainsKey(Main.mychannel))
            {
                roomData.Add(Main.mychannel, new RoomData());
            }

            // save the old text
            roomData[Main.mychannel].Text = new StringBuilder(ircHistory.Text);

            // get the new room ready
            EnsureRoomReady(channel);

            // set the text for the new room
            ircHistory.Text = roomData[channel].Text.ToString();
            ircHistory.ScrollToEnd();
            Main.mychannel = channel;

            if (channel == "System")
            {
                ircTopic.Text = "System Messages";
                ircNames.Visibility = Visibility.Collapsed;
            }
            else if (!channel.StartsWith("#"))
            {
                ircTopic.Text = "chat with " + channel;
                ircNames.Visibility = Visibility.Collapsed;
            }
            else
            {
                ircNames.Visibility = Visibility.Visible;
                ircTopic.Text = roomData[channel].Topic;
                ircNames.Items.Clear();

                List<string> l = new List<string>();
                foreach (var n in roomData[channel].Occupants.Keys)
                {
                    l.Add(n);
                }

                l.Sort();
                foreach (var n in l)
                {
                    ircNames.Items.Add(n);
                }
            }

            SelectButton(channel);
        }

        void SelectButton(string channel)
        {
            foreach (var child in ircToolbar.Children)
            {
                var button = child as Button;
                if (button == null)
                    continue;

                var textblock = button.Content as TextBlock;
                if (textblock == null)
                    continue;

                if (textblock.Text == channel)
                {
                    button.LayoutTransform = new System.Windows.Media.ScaleTransform(1.25, 1.25);
                    textblock.FontWeight = FontWeights.Bold;
                    button.Style = blueStyle;
                }
                else
                {
                    button.LayoutTransform = new System.Windows.Media.ScaleTransform(.85, .85); ;
                    textblock.FontWeight = FontWeights.Normal;
                }
            }
        }

        void HighlightButton(string channel)
        {
            foreach (var child in ircToolbar.Children)
            {
                var button = child as Button;
                if (button == null)
                    continue;

                var textblock = button.Content as TextBlock;
                if (textblock == null)
                    continue;

                if (textblock.Text == channel && channel != Main.mychannel)
                {
                    button.Style = redStyle;
                    break;
                }
            }
        }

        void RenameButton(string oldName, string newName)
        {
            foreach (var child in ircToolbar.Children)
            {
                var button = child as Button;
                if (button == null)
                    continue;

                var textblock = button.Content as TextBlock;
                if (textblock == null)
                    continue;

                if (textblock.Text == oldName)
                {
                    textblock.Text = newName;
                    break;
                }
            }

            if (roomData.ContainsKey(oldName))
            {
                var buffer = roomData[oldName].Text;
                roomData.Remove(newName);
                roomData.Remove(oldName);
                roomData[newName] = new RoomData();
                roomData[newName].Text = buffer;
            }

            if (Main.mychannel == oldName)
            {
                Main.mychannel = newName;
                ircTopic.Text = "chat with " + newName;
            }
        }

        void ircNames_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var who = ircNames.SelectedItem as string;
            if (who == null)
                return;

            if (who.StartsWith("@"))
                who = who.Substring(1);

            if (who != Main.nick)
            {
                SwitchToRoom(who);
                ircInput.Focus();
            }
        }
    }
}
