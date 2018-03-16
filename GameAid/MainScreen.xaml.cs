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
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Linq;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool gm_mode;
        public string nick;
        public string server;
        public string mychannel = "#gameroom";
        public string botchannel = "#gameroom";

        public static MainWindow mainWindow;
        public static PropertyWindow propertyWindow;
        public static GameMap propertyGameMap;

        public OpenSaveDlg openSaveRollsDlg;
        public AccessLibraryDlg accessLibraryDlg;

        public MainWindow()
        {
            InitializeComponent();
 
            var login = new ManyKey("Login", "Nick:", "", "Server:", "myserver.com");
            if (login.ShowDialog() != true)
            {
                Exit_Click(null, null);
                return;
            }

            mainWindow = this;

            nick = login.Results[0];
            server = login.Results[1];

            nick = nick.Replace(" ", "_").Replace(",", "_").Replace("/", "_");

            if (nick.ToLower().StartsWith("gm"))
                gm_mode = true;

            CreateBackgroundWorker(HandleHost, OnHostEvent);
            CreateBackgroundWorker(HandleTimer, OnTimerElapsed);

            vs1.CustomInit();
            map1.CustomInit();
            map2.CustomInit();
            map2.SetGiveTarget(map1);
           
            partyInfo1.CustomInit("Wounds");
            partyInfo2.CustomInit("Consumables");
        }

        public PartyInfo GetPartyInfo()
        {
            return partyInfo1;
        }

        static BackgroundWorker CreateBackgroundWorker(DoWorkEventHandler w, ProgressChangedEventHandler p)
        {
            System.ComponentModel.BackgroundWorker bw = new System.ComponentModel.BackgroundWorker();
            bw.DoWork += w;
            bw.ProgressChanged += p;
            bw.WorkerReportsProgress = true;
            bw.RunWorkerAsync();
            return bw;
        }

        void OnTimerElapsed(object sender, ProgressChangedEventArgs e)
        {

            if (map1.AreRequestsSuspended() || map2.AreRequestsSuspended())
                return;

            var m1 = GetMapPath(map1);
            var m2 = GetMapPath(map2);

            if (map1.ForceRequest())
            {
                SendHost(String.Format("dir {0}", m1));
                return;
            }

            if (map2.ForceRequest())
            {
                SendHost(String.Format("dir {0}", m2));
                return;
            }

            var dirs = AddAllStandardDirs();

            AddDir(dirs, m1);
            AddDir(dirs, m2);

            foreach (string dir in dirs)
                SendHost(String.Format("qdir {0}", dir));
        }

        internal List<string> AddAllStandardDirs()
        {
            List<string> dirs = new List<string>();

            AddDir(dirs, "/");
            AddDir(dirs, "_who");
            AddDir(dirs, "_party");
            AddDir(dirs, "_gameaid/_players");
            AddDir(dirs, "_gameaid/_remote");
            AddDir(dirs, "_mana");
            AddDir(dirs, "_checks");
            AddDir(dirs, "_fatigue");
            AddDir(dirs, "_buffs");
            AddDir(dirs, "_maps");
            AddDir(dirs, "_loot");
            AddDir(dirs, "_presence");
            AddDir(dirs, "_shugenja");
            AddDir(dirs, "_runemagic");
            AddDir(dirs, "_spiritmana");
            AddDir(dirs, "_camp");
            AddDir(dirs, "_used");
            AddDir(dirs, "_wounds");

            AddPartyInfoDirs(dirs, partyInfo1);
            AddPartyInfoDirs(dirs, partyInfo2);

            return dirs;
        }

        static void AddPartyInfoDirs(List<string> dirs, PartyInfo p1)
        {
            if (p1.PartyPath != null)
            {
                AddDir(dirs, p1.PartyPath);
            }

            if (p1.FolderPath != null && p1.FolderPath != "")
            {
                AddDir(dirs, p1.FolderPath);
            }
        }

        static string GetMapPath(GameMap map)
        {
            var m1 = map.currentPath.Text;
            if (m1 == null || m1 == "")
                m1 = "default";

            m1 = "_maps/" + m1;
            return m1;
        }

        static void AddDir(List<string> dirs, string d)
        {
            if (!dirs.Contains(d))
                dirs.Add(d);
        }

        public void OnIrcEvent(object sender, ProgressChangedEventArgs e)
        {
            if (miniIrc != null) miniIrc.OnIrcEvent(sender, e);
        }

        public SpeechSynthesizer reader;

        public void OnHostEvent(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is Exception)
            {
                Exception ex = e.UserState as Exception;
                PopupString(ex.ToString() + "\r\n" + ex.StackTrace.ToString());
            }
            if (e.UserState is string)
            {
                /*
                string line = (string)e.UserState;

                textHistory.AppendText(line + "\r\n");
                textHistory.ScrollToEnd();
                 */
            }
            else if (e.UserState is EvalBundle)
            {
                EvalBundle b = (EvalBundle)e.UserState;
                ProcessEvals(b);
            }
            else if (e.UserState is DictBundle)
            {
                DictBundle b = (DictBundle)e.UserState;

                if (b.path == "download-result")
                {
                    OfferDownloads(b);
                    if (accessLibraryDlg != null)
                    {
                        accessLibraryDlg.Consider(b);
                    }
                }
                else
                {
                    if (b.path == "_gameaid/_filenames")
                    {
                        filenameDict = b.dict;
                    }

                    partyInfo1.Consider(b);
                    partyInfo2.Consider(b);
                    vs1.Consider(b);
                    readyRolls.Consider(b);

                    if (openSaveRollsDlg != null)
                        openSaveRollsDlg.Consider(b);

                    map1.Consider(b);
                    map2.Consider(b);

                    foreach (var sq in Squad.Children)
                    {
                        var sheet = sq as VirtualSheet;
                        if (sheet != null)
                            sheet.Consider(b);
                    }
                }
            }
            else if (e.UserState is DownloadFile)
            {
                DownloadFile file = e.UserState as DownloadFile;

                // Configure open file dialog box
                var dlg = new System.Windows.Forms.SaveFileDialog();
                dlg.Title = "Save Download";
                dlg.FileName = file.name;
                dlg.DefaultExt = ".xlsx"; // Default file extension
                dlg.Filter = "Character Sheets (.xlsx)|*.xlsx"; // Filter files by extension
                dlg.OverwritePrompt = true;
                dlg.CheckPathExists = true;

                // Show open file dialog box
                System.Windows.Forms.DialogResult result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllBytes(dlg.FileName, file.bytes);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message);
                        return;
                    }
                }
            }
            else if (e.UserState is AudioReport)
            {
                AudioReport audio = e.UserState as AudioReport;
                
                string url = "";

                switch (audio.desc)
                {
                    case "speak":
                        if (reader == null)
                        {
                            try
                            {
                                // tolerate reader acquisition failure more gracefully

                                reader = new SpeechSynthesizer();

                                // var l = new List<VoiceInfo>();
                                //
                                // foreach (InstalledVoice voice in reader.GetInstalledVoices())
                                // {
                                //     VoiceInfo info = voice.VoiceInfo;
                                //     string AudioFormats = "";
                                //     foreach (SpeechAudioFormatInfo fmt in info.SupportedAudioFormats)
                                //     {
                                //         AudioFormats += String.Format("{0}\n",
                                //         fmt.EncodingFormat.ToString());
                                //     }
                                // 
                                //     l.Add(info);
                                // 
                                // }
                                
                                reader.SelectVoice("Microsoft Zira Desktop");
                            }
                            catch
                            {

                            }


                        }

                        if (reader != null)
                        {
                            reader.SpeakAsync(audio.text);
                        }
                        break;

                    case "ownage":
                        switch (audio.killcount)
                        {
                            case 1:
                                url = "http://myserver.com/uploads/killshot/1.mp3";
                                break;
                            case 2:
                                url = "http://myserver.com/uploads/killshot/4.mp3";
                                break;
                            case 3:
                                url = "http://myserver.com/uploads/killshot/6.mp3";
                                break;
                            case 4:
                                url = "http://myserver.com/uploads/killshot/7.mp3";
                                break;
                            case 5:
                                url = "http://myserver.com/uploads/killshot/10.mp3";
                                break;
                            case 6:
                                url = "http://myserver.com/uploads/killshot/9.mp3";
                                break;
                            case 7:
                            default:
                                url = "http://myserver.com/uploads/killshot/14.mp3";
                                break;
                        }
                        break;

                    case "fumble":
                        url = "http://myserver.com/uploads/killshot/0.mp3";
                        break;

                    default:
                        url = "http://myserver.com/uploads/misc/" + audio.desc;
                        break;
                }

                if (url != "")
                {
                    var player = new System.Windows.Media.MediaPlayer();
                    player.Open(new Uri(url));
                    player.MediaOpened += new EventHandler((object s, EventArgs a) => { player.Play(); });
                    player.MediaEnded += new EventHandler((object s, EventArgs a) => { player.Close(); });
                }

            }
            else if (e.UserState is HoursReport)
            {
                HoursReport h = e.UserState as HoursReport;
                vs1.SetRemainingHours(h.hours);
            }
        }

        void ProcessEvals(EvalBundle b)
        {
            readyRolls.Consider(b);

            if (accessLibraryDlg != null && b.path == "library-search")
                accessLibraryDlg.Consider(b);

            if (b.path == "map1")
                map1.ConsiderEval(b);
            else if (b.path == "map2")
                map2.ConsiderEval(b);

            if (b.path == "command-result")
            {
                ProcessCommandResult(b);
            }
        }

        void ProcessCommandResult(EvalBundle b)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < 20; i++)
            {
                var k = i.ToString();
                if (!b.dict.ContainsKey(k))
                    break;

                if (i > 0) sb.Append("\r\n");
                sb.Append(b.dict[k]);
            }

            PopupString(sb.ToString());
        }

        internal void PopupString(string str)
        {
            Window window = new Window();

            double winHeight = 100;
            double winWidth = 800;

            //The following properties are used to create a irregular window
            window.AllowsTransparency = true;
            window.Background = Brushes.Transparent;
            window.WindowStyle = WindowStyle.None;

            window.ShowInTaskbar = false;
            window.Topmost = true;
            window.Height = winHeight;
            window.Width = winWidth;
            window.Opacity = 0;

            var pt = root.PointToScreen(Mouse.GetPosition(root));

            //Set postion to the mouse
            window.Left = pt.X+16;
            window.Top = pt.Y+16;

            var text = new TextBlock();
            var grid = new Grid();
            grid.Children.Add(text);
            grid.Width = window.Width;
            grid.Height = window.Height;

            window.Content = grid;
            window.ShowActivated = false;
            window.Show();
           
            FadeInWindow(window);

            grid.Background = new SolidColorBrush(Colors.PowderBlue);
            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            text.MaxHeight = winHeight;
            text.MaxWidth = winWidth;
            text.TextWrapping = TextWrapping.Wrap;
            text.Padding = new Thickness(5, 5, 5, 5);
            Grid.SetRow(text, 0);
            Grid.SetColumn(text, 0);

            this.DelayAction(20, () =>
            {
                window.Width = text.ActualWidth;
                window.Height = text.ActualHeight;
            });

            text.Text = str;
        }

        void FadeInWindow(Window window)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(window, new NameScope());

            // Register the name with the page to which the element belongs.
            window.RegisterName("element", window);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(100));

            GameMap.Anim2Point(story, dur, "element", Window.OpacityProperty, 0, 1);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    this.DelayAction(4000, () =>
                    {
                        FadeOutWindow(window);
                    });
                });

            story.Begin(window);
        }
        
        void FadeOutWindow(Window window)
        {
            // Create a storyboard to contain the animation.
            Storyboard story = new Storyboard();

            // Create a name scope for the page.
            NameScope.SetNameScope(window, new NameScope());

            // Register the name with the page to which the element belongs.
            window.RegisterName("element", window);

            Duration dur = new Duration(TimeSpan.FromMilliseconds(2000));

            GameMap.Anim2Point(story, dur, "element", Window.OpacityProperty, 1, 0);

            story.Completed += new EventHandler(
                (object sender2, EventArgs e2) =>
                {
                    window.Close();
                });

            story.Begin(window);
        }

        string lastPin = "-";
        Dictionary<string, string> filenameDict;
        BackgroundWorker ircWorker;

        void OfferDownloads(DictBundle b)
        {
            // gameaid folder is for images not spreadsheets
            if (b.dict.ContainsKey("folder") && b.dict["folder"] == "gameaid")
                return;

            // clear the download menu and put the default enter pin item back
            ResetDownloadMenu();

            var items = new Dictionary<string, MenuItem>();

            for (int i = 0; i < b.dict.Count; i++)
            {
                var k = i.ToString();
                if (b.dict.ContainsKey(k))
                {
                    var filename = b.dict[k];

                    // map the file name to the character group like "Halakiv" or whatever
                    string group;
                    if (!filenameDict.TryGetValue(filename, out group))
                        group = "Ungrouped";

                    MenuItem parent;
                    MenuItem m;
                    TextBlock t;
                    
                    // create a new group if needed and add them to the menu as they occur
                    if (!items.ContainsKey(group))
                    {
                        m = new MenuItem();
                        t = new TextBlock();
                        t.Text = group;
                        m.Header = t;
                        items[group] = m;
                        parent = m;
                    }
                    else
                    {
                        parent = items[group];
                    }

                    // now add the actual file download open to the appropriate group menu item
                    m = new MenuItem();
                    t = new TextBlock();
                    t.Text = filename;
                    m.Header = t;
                    m.Click += new RoutedEventHandler(FileDownload_Click);
                    parent.Items.Add(m);
                }
            }

            // now finally, order group the items so we can add them to the download menu
            var q = from p in items.Keys
                    orderby p
                    select items[p];

            foreach (MenuItem m in q)
            {
                download_menu.Items.Add(m);
            }
        }

        void ResetDownloadMenu()
        {
            download_menu.Items.Clear();

            // add back the standard items

            var m = new MenuItem();
            var t = new TextBlock();
            t.Text = "Enter PIN...";
            m.Header = t;
            m.Click += new RoutedEventHandler(DownloadSheet_Click);

            download_menu.Items.Add(m);
            download_menu.Items.Add(new Separator());
        }

        void FileDownload_Click(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;

            if (m == null)
                return;

            TextBlock t = m.Header as TextBlock;

            if (t == null)
                return;

            string file = t.Text;

            SendHost(String.Format("download {0} {1}", file, lastPin));
        }

        public void SendChat(string text)
        {
            SendHost("!echo " + nick + " " + text);
        }

        public void SendEmote(string text)
        {
            SendHost("!echo " + text);
        }

        void Exit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        void About_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AboutBox();
            dlg.Owner = mainWindow;
            dlg.HorizontalAlignment = HorizontalAlignment.Center;
            dlg.VerticalAlignment = VerticalAlignment.Center;
            dlg.ShowDialog();
        }

        void SorceryWizard_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SorceryWizard();
            dlg.Owner = mainWindow;
            dlg.HorizontalAlignment = HorizontalAlignment.Center;
            dlg.VerticalAlignment = VerticalAlignment.Center;
            dlg.ShowDialog();
        }

        void CheatSheet_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CheatSheet();
            dlg.Owner = mainWindow;
            dlg.HorizontalAlignment = HorizontalAlignment.Center;
            dlg.VerticalAlignment = VerticalAlignment.Center;
            dlg.ShowDialog();
        }

        void PropertyWindow_Click(object sender, RoutedEventArgs e)
        {
            propertyWindow = new PropertyWindow();
            propertyWindow.InitFromContext();
            propertyWindow.Show();
        }

        void AddBotToChatroom_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("What chatroom do you want the bot to join?", "Chatroom:", mychannel);

            if (dlg.ShowDialog() == true)
            { 
                string k = dlg.Results[0];

                if (!k.StartsWith("#"))
                {
                    k = "#" + k;
                }

                k.Replace(" ", "");
                SendHost(String.Format("join {0}", k));
            }
        }

        void ChangeNick_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("What do you want your new nickname to be?", "Nick:", nick);

            if (dlg.ShowDialog() == true)
            {
                nick = dlg.Results[0].Replace(" ", "_");

                SendIrc("nick " + nick);
                SendHost("nick " + nick);
            }
        }
        
        void ChangeChatroom_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("What chatroom do you want room commands to go to?", "Chatroom:", botchannel);

            if (dlg.ShowDialog() == true)
            { 
                string channel = dlg.Results[0].Replace(" ","_");

                if (!channel.StartsWith("#"))
                {
                    channel = "#" + channel;
                }

                SendHost(String.Format("room {0}", channel));
                SendIrc(String.Format("join {0}", channel));
                miniIrc.SwitchToRoom(channel);
                botchannel = channel;
                SendHost("dir _party");
                SendHost("dir _gameaid/_remote");
            }
        }
        
        void IRC_Click(object sender, RoutedEventArgs e)
        {
            if (ircWorker == null)
            {
                ircWorker = CreateBackgroundWorker(HandleIrc, OnIrcEvent);
            }

            MenuItem m = sender as MenuItem;

            if (m.IsChecked)
            {
                map2.Visibility = Visibility.Hidden;
                miniIrc.Visibility = Visibility.Visible;
            }
            else
            {
                map2.Visibility = Visibility.Visible;
                miniIrc.Visibility = Visibility.Hidden;
            }         
        }

        void ImportSheet_Click(object sender, RoutedEventArgs e)
        {
            OpenAndIterateSheets((string file, StringBuilder errors) => (new SheetImporter()).ImportSheet(file, errors));
        }

        void UploadSheet_Click(object sender, RoutedEventArgs e)
        {
            OpenAndIterateSheets((string file, StringBuilder errors) => UploadSheet(file, errors));
        }

        void VerifySheet_Click(object sender, RoutedEventArgs e)
        {
            OpenAndIterateSheets((string file, StringBuilder errors) => (new SheetImporter()).VerifySheet(file, errors));
        }

        void ImportSpirits_Click(object sender, RoutedEventArgs e)
        {
            OpenAndIterateSheets((string file, StringBuilder errors) => ImportSpirits(file, errors));
        }
        
        void SearchSheet_Click(object sender, RoutedEventArgs e)
        {
            ManyKey dlg = new ManyKey("Search Sheets", "Search For:", "");

            if (dlg.ShowDialog() == true)
            {
                var pattern = dlg.Results[0];
                OpenAndIterateSheets((string file, StringBuilder errors) => (new SheetImporter()).SearchSheet(file, pattern, errors));
            }
        }
        
        void UploadSheet(string path, StringBuilder errors)
        {
            var importer = new SheetImporter();
            var password = importer.ImportPassword(errors, path);

            if (password == "")
            {
                errors.AppendFormat("{0} has no password set in cell AA1\n", path);
                return;
            }

            byte[] bytes = null;
            try
            {
                 bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                errors.AppendLine(e.Message);
                return;
            }

            string file = System.IO.Path.GetFileName(path).Replace(" ", "_");
            password = password.Replace(" ", "_").Replace("/", "_");

            mainWindow.SendUpload(password, bytes, file);

            errors.AppendFormat("{0} uploaded to {1}.\n", path, password);

            // now scrape the sheet
            importer.ImportSheet(path, errors);
        }

        void ImportSpirits(string path, StringBuilder errors)
        {
            var importer = new SheetImporter();

            // now scrape the sheet
            importer.ImportSpirits(path, errors);
        }

        void DownloadSheet_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManyKey("Enter your download PIN/Password", "PIN:", "");

            if (dlg.ShowDialog() == true)
            {
                string k = dlg.Results[0];

                SendHost("dir _gameaid/_filenames");
                k = k.Replace("/", "_").Replace(" ", "_"); 
                SendHost(String.Format("download-dir {0}", k));

                lastPin = k;
            }
        }
      
        void OpenAndIterateSheets(Action<string, StringBuilder> action)
        {
            StringBuilder errors = new StringBuilder();

            // Configure open file dialog box
            var dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Title = "Select Sheet";
            dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".xlsx"; // Default file extension
            dlg.Filter = "Character Sheets (.xlsx)|*.xlsx"; // Filter files by extension
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Multiselect = true;

            // Show open file dialog box
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                foreach (var file in dlg.FileNames)
                {
                    action(file, errors);
                }

                var results = new ResultSummary();
                results.Owner = mainWindow;
                results.HorizontalAlignment = HorizontalAlignment.Center;
                results.VerticalAlignment = VerticalAlignment.Center;
                results.CustomInit(errors);
                results.ShowDialog();
            }
        }

        void Chargen_Parse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ParseChargen();
            dlg.Owner = mainWindow;
            dlg.HorizontalAlignment = HorizontalAlignment.Center;
            dlg.VerticalAlignment = VerticalAlignment.Center;
            if (dlg.ShowDialog() != true)
                return;

            var data = dlg.data.Text;

            var reader = new System.IO.StringReader(data);

            string line;

            // Number 43 An Average Experienced Dragon Kralorelan Samurai,Male T1: 9 TF: 20 Honor: 58

            var r1  = new Regex("^[^0-9]+([0-9]+) ([0-9A-Za-z ,-]+).* T1: [0-9]+ TF: [0-9]+ .*Hit Loc: (.*)$");
            var r2  = new Regex("^(........................). SR (...) (...)% (..................) ...%  ..AP$");
            var r3  = new Regex("^(........................). SR (...) (...)% (..................) (...)%  (..)AP .*$");
            var r4  = new Regex("^(........................). SR (...) (...)% ([0-9D+-]+) .*([0-9]+  Shots) .*");
            var r5  = new Regex("^(........................). SR (...) (...)% ([0-9D+-]+) (.+)$");
            var r6  = new Regex("^(........................). SR (...) (...)% ([0-9D+-]+)$");
            var r7  = new Regex("^(........................). SR (...) (...)% (.+)$");
            var r8  = new Regex("^Skills: "); 
            var r9  = new Regex("STR .... CON .... SIZ (....) INT .... POW .... DEX .... APP .... SIZSRM .*");
            var r10 = new Regex("Life.*:.* Movement: +([^ ]+) Mana:.* Damage:.*");

            var name = "";
            var number = "";
            var hitloc = "";
            int index = 0;

            while ((line = reader.ReadLine()) != null)
            {
                var match = r1.Match(line);
                if (match.Success)
                {
                    number = match.Groups[1].ToString();
                    hitloc = match.Groups[3].ToString();
                    var longname = match.Groups[2].ToString();

                    longname = longname
                        .Replace(",", " ")
                        .Replace("  ", " ")
                        .Replace("  ", " ")
                        .Replace("An ", "")
                        .Replace("A ", "")
                        .Replace("Veteran ", "")
                        .Replace("Experienced ", "")
                        .Replace("Tough ", "")
                        .Replace("Average ", "");

                    name = "#" + number + " " + longname;
                    continue;
                }

                match = r2.Match(line);
                if (match.Success)
                {
                    var wpn = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var dmg = match.Groups[4].ToString().Trim();

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                    readyRolls.AppendStripContents(name, attackStrip.Children);
                    continue;
                }

                match = r3.Match(line);
                if (match.Success)
                {
                    var wpn = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var dmg = match.Groups[4].ToString().Trim();

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                    readyRolls.AppendStripContents(name, attackStrip.Children);

                    var parryPct = match.Groups[5].ToString().Trim();
                    var parryAP = match.Groups[6].ToString().Trim();

                    var parryStrip = new ParryStrip();
                    parryStrip.Init(name, parryChoice: wpn, ap: parryAP, parryPct: parryPct);
                    readyRolls.AppendStripContents(name, parryStrip.Children);
                    continue;
                }

                match = r4.Match(line);
                if (match.Success)
                {
                    var wpn = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var dmg = match.Groups[4].ToString().Trim();
                    var note = match.Groups[5].ToString().Trim();

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: note);
                    readyRolls.AppendStripContents(name, attackStrip.Children);
                    continue;
                }

                match = r5.Match(line);
                if (match.Success)
                {
                    var wpn = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var dmg = match.Groups[4].ToString().Trim();
                    var note = match.Groups[5].ToString().Trim();

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: note);
                    readyRolls.AppendStripContents(name, attackStrip.Children);
                    continue;
                }

                match = r6.Match(line);
                if (match.Success)
                {
                    var wpn = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var dmg = match.Groups[4].ToString().Trim();

                    var attackStrip = new AttackStrip();
                    attackStrip.Init(name, weapon: wpn, sr: sr, pct: pct, damage: dmg, note: null);
                    readyRolls.AppendStripContents(name, attackStrip.Children);
                    continue;
                }

                match = r7.Match(line);
                if (match.Success)
                {
                    var skill = match.Groups[1].ToString().Trim();
                    var sr = match.Groups[2].ToString().Trim();
                    var pct = match.Groups[3].ToString().Trim();
                    var note = match.Groups[4].ToString().Trim();

                    var skillStrip = new SkillStrip();
                    skillStrip.Init(name, skill, sr: sr, skillPct: pct, note: note);
                    readyRolls.AppendStripContents(name, skillStrip.Children);
                    continue;
                }

                match = r8.Match(line);
                if (match.Success)
                {
                    var l = line.Substring("Skills: ".Length);
                    while (l.Length > 0)
                    {
                        string car, ctr;
                        Parse2Ex(l, out car, out ctr, ',');
                        l = ctr;

                        string skill, val;
                        RParse2Ex(car, out skill, out val, ' ');

                        skill = skill.Trim();
                        val = val.Trim();

                        bool special = skill.StartsWith("*");
                        
                        if (special)
                            skill = skill.Substring(1);

                        if (skill == "Dodge" || special)
                        {
                            var skillStrip = new SkillStrip();
                            skillStrip.Init(name, skill, sr: null, skillPct: val, note: null);
                            readyRolls.AppendStripContents(name, skillStrip.Children);
                        }
                    }
                    continue;
                }

                match = r9.Match(line);
                if (match.Success)
                {
                    var siz = match.Groups[1].ToString().Trim();
                    name = name + " (SIZ " + siz + ")";

                    if (dlg.ImageName != null)
                    {
                        map1.AcceptLibraryToken(hitloc, siz, number, dlg.ImageName, index++);
                    }
                    else if (dlg.fillBrush != null)
                    {
                        map1.AcceptGeneratedToken(hitloc, siz, number, dlg.fillBrush, index++);
                    }
                    continue;
                }

                match = r10.Match(line);
                if (match.Success)
                {
                    var move = match.Groups[1].ToString().Trim();
                    name = name + " (Moves: " + move + ")";
                    continue;
                }
            }
        }

        internal void SetTopMap(string k)
        {
            map1.SetMapPath(k);
        }

        internal void SetBottomMap(string k)
        {
            map2.SetMapPath(k);
        }

        internal void NewMap_Click(object sender, RoutedEventArgs e)
        {
            PromptNewMap();
        }

        internal void PromptNewMap()
        {
            var dlg = new ManyKey("New Map", "Name:", "");

            if (dlg.ShowDialog() == true)
            {
                string name = dlg.Results[0].Replace(" ", "_").Replace("/", "_");

                name = "_maps/" + name;

                MakeNewMap(name);
            }
        }

        internal void MakeNewMap(string path)
        {
            var v = "<Tile Rows=\"50\" Columns=\"50\" Thickness=\"2\" Tag=\"\" Width=\"2000\" Height=\"2000\" Margin=\"0,0,0,0\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" IsHitTestVisible=\"False\" xmlns=\"clr-namespace:GameAid;assembly=GameAid\" xmlns:av=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"></Tile>";
            SendHost(String.Format("n {0} {1} {2}", path, "000000", v));


            // strip off the _maps/ path again
            map1.AddNewMap(path.Substring(6));
        }

        internal void DelayAction(int delayMilliseconds, Action func)
        {
            // Create a timer with a two second interval.
            var aTimer = new System.Timers.Timer(delayMilliseconds);
            aTimer.Start();

            // Hook up the Elapsed event for the timer.
            aTimer.Elapsed += new System.Timers.ElapsedEventHandler(
                (obj, eventArgs) =>
                {
                    aTimer.Stop();
                    Dispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        func);
                });
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            var root = MainWindow.mainWindow.SquadRoot;

            root.Visibility = Visibility.Hidden;
            vs1.Visibility = Visibility.Visible;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            var squad = MainWindow.mainWindow.Squad;
            foreach (var sq in Squad.Children)
            {
                var sheet = sq as VirtualSheet;
                if (sheet != null)
                    sheet.Refresh();
            }
        }

        private void textChanged(object sender, TextChangedEventArgs e)
        {
            string str = searchBox.Text;

            foreach (var sq in Squad.Children)
            {
                var sheet = sq as VirtualSheet;
                if (sheet != null)
                    sheet.DoSearch(str);
            }
        }
    }

    public class DictBundle
    {
        public string path;
        public Dictionary<string, string> dict = new Dictionary<string, string>();
    }

    public class EvalBundle : DictBundle
    {
    }

    public class DownloadFile
    {
        public string name;
        public byte[] bytes;
    }

    public class HoursReport
    {
        public int hours;
    }

    public class AudioReport
    {
        public string desc;
        public int killcount;
        public string text;
    }
}
