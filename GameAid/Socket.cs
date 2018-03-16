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
using System.Windows.Data;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace GameAid
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpClient hostClient;
        TcpClient ircClient;

        public void SendHost(String data)
        {
            string d = data + "\n";

            if (hostClient == null)
                return;

            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(d);

            RetryAction(() =>
                {
                    hostClient.GetStream().Write(byteData, 0, byteData.Length);
                }
            );
        }

        void RetryAction(Action action)
        {
            bool succeeded = false;

            while (!succeeded)
            {
                var savedClient = hostClient;
                try
                {
                    action();                   
                    succeeded = true;
                }
                catch (Exception)
                {
                    do
                    {
                        System.Threading.Thread.Sleep(3000);
                    }
                    while (savedClient == hostClient);
                }
            }
        }

        public void SendUpload(string password, byte[] bytes, string file)
        {
            if (bytes == null)
                return;

            var cmd = String.Format("upload {0} {1} {2}\n", file, password, bytes.Length);
            byte[] byteData = Encoding.ASCII.GetBytes(cmd);

            RetryAction(() =>
                {
                    hostClient.GetStream().Write(byteData, 0, byteData.Length);
                    hostClient.GetStream().Write(bytes, 0, bytes.Length);
                });
        }

        public void SendIrc(String data)
        {
            string d = data + "\n";

            if (ircClient == null)
                return;

            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(d);

            ircClient.GetStream().Write(byteData, 0, byteData.Length);
        }

        public void SendIrcChat(String msg)
        {
            SendIrc(String.Format(":{0} PRIVMSG {1} :{2}", nick, mychannel, msg));
        }

        public void SendIrcEmote(String msg)
        {
            SendIrc(String.Format(":{0} PRIVMSG {1} :\x0001ACTION {2}\x0001", nick, mychannel, msg));
        }

        public void HandleIrc(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            for (;;)
            {
                try
                {
                    ReportText(worker, "*", "Attempting to (re)connect");

                    ircClient = new System.Net.Sockets.TcpClient(server, 6667);
                    NetworkStream stream = ircClient.GetStream();
                    StreamReader sr = new StreamReader(stream, Encoding.ASCII);

                    SendIrc(String.Format("user {0} {0} {0} {0}", nick));
                    SendIrc(String.Format("nick {0}", nick));
                    SendIrc(String.Format("join {0}", mychannel));

                    string line;

                    for (;;)
                    {
                        // no need to hold on to line while we block on input
                        line = null;

                        // read a line from the stream
                        line = sr.ReadLine();

                        // if there's no line, we are exiting...
                        if (line == null)
                        {
                            ReportText(worker, "*", "disconnected");
                            break;
                        }

                        string a1, a2, a3;

                        Parse3(line, out a1, out a2, out a3);
                        string user = ParseUser(a1);

                        if (a1 == "PING")
                        {
                            SendIrc(String.Format("PONG :{0}", nick));

                            // refresh name list from time to time in case we're busted
                            SendIrc(String.Format("NAMES {0}", mychannel));
                            continue;
                        }

                        switch (a2)
                        {
                            case "PRIVMSG":
                                {
                                    if (a3 == null)
                                        continue;

                                    string room, msg;

                                    Parse2(a3, out room, out msg);

                                    if (!room.StartsWith("#"))
                                        room = user;

                                    if (msg.StartsWith(":\x0001ACTION "))
                                    {
                                        string s1, s2;
                                        Parse2(msg, out s1, out s2);
                                        s2 = s2.Substring(0, s2.Length - 1);
                                        ReportText(worker, room, String.Format("{0} {1}", user, s2));
                                    }
                                    else
                                    {
                                        ReportText(worker, room, String.Format("<{0}> {1}", user, msg.Substring(1)));
                                    }
                                    break;
                                }

                            case "372":
                            case "375":
                            case "001":
                            case "002":
                            case "265":
                                {
                                    string room, msg;
                                    Parse2(a3, out room, out msg);
                                    if (msg != null && msg.Length > 1)
                                        ReportText(worker, "System", msg.Substring(1));
                                }
                                break;

                            case "433":
                                {
                                    // nick is in use, pick a different nickname
                                    string junk, conflict, junk2;
                                    Parse3(a3, out junk, out conflict, out junk2);
                                    nick = conflict + "_";
                                    SendIrc(String.Format("nick {0}", nick));
                                    SendHost(String.Format("nick {0}", nick));
                                    SendIrc(String.Format("join {0}", mychannel));
                                }
                                break;
                         
                            case "331": // no topic is set
                            case "332":
                                {
                                    // entering a room with an topic or no topic, either way suitable text is after the colon
                                    // a3 = "yournick #gameroom :No topic is set."  OR
                                    // a3 = "yournick #gameroom :The topic."
                                    string n, room, topic;
                                    Parse3(a3, out n, out room, out topic);
                                    if (topic.Length > 0) topic = topic.Substring(1);

                                    ReportTopic(worker, room, room + ": " + topic);
                                }
                                break;

                            case "321":
                            case "323":
                                // introduce and end LIST output, ignored.
                                break;

                            case "322":  // LIST output
                                {
                                    // :MyServer. 322 fd #gameroom 4 :Test
                                    // a3 = "fd #gameroom 4 :Test";
                                    string prefix, topic;
                                    Parse2Ex(a3, out prefix, out topic, ':');
                                    if (topic == "")
                                        break;

                                    string who, room, junk;
                                    Parse3(prefix, out who, out room, out junk);
                                    if (room == "")
                                        break;

                                    ReportTopic(worker, room, room + ": " + topic);
                                    break;
                                }

                            case "353":
                                {
                                    // 353 is people in the existing room
                                    // a3 = "lk = #gameroom :lk Arc @__ Kinzzz"

                                    string prefix, people;
                                    Parse2Ex(a3, out prefix, out people, ':');
                                    if (people == "")
                                        break;

                                    string junk, room;
                                    Parse2Ex(prefix, out junk, out room, '=');
                                    if (room == "")
                                        break;

                                    room = room.Trim();

                                    ReportClearNames(worker, room);
                                    for (;;)
                                    {
                                        string car, ctr;
                                        Parse2(people, out car, out ctr);

                                        if (car == "")
                                            break;

                                        ReportAddName(worker, room, car);

                                        if (ctr == "")
                                            break;

                                        people = ctr;
                                    }
                                }
                                break;

                            //  I get the names in the name list above
                            // :MyServer. 366 Rico #gameroom :End of /NAMES list.
                            // drop the end of names notification on the floor
                            case "366":
                                break;

                            case "NICK":
                                // change of nickname
                                if (a3.StartsWith(":") && a3.Length > 1)
                                {
                                    string newName = a3.Substring(1);
                                    ReportText(worker, "*", String.Format("{0} is now known as {1}.", user, newName));
                                    ReportNewNick(worker, user, newName);

                                    if (user == nick)
                                    {
                                        SendHost("nick " + newName);
                                        nick = newName;
                                    }
                                }
                                break;

                            case "JOIN":
                                // someone has entered the channel (maybe me)
                                if (a3.StartsWith(":") && a3.Length > 1)
                                {
                                    string room = a3.Substring(1);
                                    ReportAddName(worker, room, user);
                                    ReportText(worker, room, String.Format("{0} has joined the channel.", user));
                                }
                                break;

                            case "PART":
                                // someone has left the channel (maybe me)
                                if (a3.StartsWith(":") && a3.Length > 1)
                                {
                                    string room = a3.Substring(1);
                                    ReportRemoveName(worker, room, user);
                                    ReportText(worker, room, String.Format("{0} has left the channel.", user));
                                }
                                break;

                            case "QUIT":
                                // someone left the server
                                ReportText(worker, "*", String.Format("{0} has left the server.", user));
                                break;

                            case "TOPIC":
                                // new topic, example:
                                // :Patience!saraiah@50.46.118.*** TOPIC #gameroom :loot restored, audited, added spaces to description
                                // entering a room with an existing topic
                                {
                                    string room, topic;
                                    Parse2Ex(a3, out room, out topic, ':');
                                    room = room.Trim();
                                    ReportTopic(worker, room, room + ": " + topic);
                                    break;
                                }

                            case "004":
                                // dunno what this is
                                // :MyServer. 004 Rico MyServer. CR1.7.6 oiwsabjgrchytxkmnpeAEGFSLX abcdeijklmnoprstuvAMNL

                            case "005":
                                // whatever, some protocols
                                // :MyServer. 005 Rico NOQUIT WATCH=128 SAFELIST TUNL :are available on this server

                            case "376":
                                // message of the day... whatever not supported
                                // :MyServer. 376 Rico :End of /MOTD command.

                            case "333":
                                // this is the person that set the topic and when
                                // :MyServer. 333 Rico #gameroom Patience 1342900125
                                break;

                            default:
                                // system messages
                                // :MyServer. 251 Rico :There are 1 users and 8 invisible on 1 servers

                                // operators
                                // :MyServer. 252 Rico 2 :operator(s) online

                                // channels
                                // :MyServer. 254 Rico 4 :channels formed

                                // clients and servers
                                // :MyServer. 255 Rico :I have 9 clients and 0 servers

                                // global users
                                // :MyServer. 266 Rico :Current global users: 9  Max: 14
                                ReportText(worker, "System", line);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportException(worker, ex);
                }

                ReportClearNames(worker, "*");
                ReportText(worker, "*", "Sleeping for 3s");
                System.Threading.Thread.Sleep(3000);
            }
        }


        void ReportException(BackgroundWorker worker, Exception ex)
        {
            var s = ex.ToString() + "\r\n" + ex.StackTrace.ToString();
            ReportPopup(worker, s);
        }

        void ReportPopup(BackgroundWorker worker, string msg)
        {
            var pair = new KeyValuePair<string, string>(msg, "");
            worker.ReportProgress(MiniIRC.DoPopup, pair);
        }

        void ReportText(BackgroundWorker worker, string target, string message)
        {
            var pair = new KeyValuePair<string, string>(target, message);
            worker.ReportProgress(MiniIRC.DoText, pair);
        }

        void ReportTopic(BackgroundWorker worker, string room, string message)
        {
            var pair = new KeyValuePair<string, string>(room, message);
            worker.ReportProgress(MiniIRC.DoTopic, pair);
        }

        void ReportClearNames(BackgroundWorker worker, string room)
        {
            var pair = new KeyValuePair<string, string>(room, "");
            worker.ReportProgress(MiniIRC.DoClearNames, pair);
        }

        void ReportAddName(BackgroundWorker worker, string room, string name)
        {
            var pair = new KeyValuePair<string, string>(room, name);
            worker.ReportProgress(MiniIRC.DoAddName, pair);
        }

        void ReportRemoveName(BackgroundWorker worker, string room, string name)
        {
            var pair = new KeyValuePair<string, string>(room, name);
            worker.ReportProgress(MiniIRC.DoRemoveName, pair);
        }

        void ReportNewNick(BackgroundWorker worker, string oldName, string newName)
        {
            var pair = new KeyValuePair<string, string>(oldName, newName);
            worker.ReportProgress(MiniIRC.DoNewNick, pair);
        }

        string ParseUser(string a1)
        {
            string s1, s2;
            Parse2Ex(a1.Substring(1), out s1, out s2, '!');
            return s1;
        }

        public void HandleHost(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker b = (BackgroundWorker)sender;
            DictBundle pending = null;
            string a1, a2, a3, a4;

            for (;;)
            {
                try
                {
                    hostClient = new System.Net.Sockets.TcpClient(server, 6668);

                    NetworkStream stream = hostClient.GetStream();
                    HybridStreamReader r = new HybridStreamReader(stream);
                    HybridStreamReader savedStream = null;

                    SendHost("nick " + nick);
                    SendHost("dir /");
                    SendHost("dir _maps/default");

                    string line;

                    for (; ; )
                    {
                        if ((line = r.ReadLine()) == null)
                        {
                            if (savedStream != null)
                            {
                                r = savedStream;
                                savedStream = null;
                                continue;
                            }

                            break;
                        }

                        if (line.StartsWith("hours "))
                        {
                            int hours;
                            Parse2(line, out a1, out a2);
                            Int32.TryParse(a2, out hours);

                            var h = new HoursReport();
                            h.hours = hours;
                            b.ReportProgress(1, h);
                        }
                        else if (line.StartsWith("audio "))
                        {
                            int count;
                            Parse3(line, out a1, out a2, out a3);

                            var audio = new AudioReport();
                            audio.desc = a2;
                            if (a2 == "ownage")
                            {
                                Int32.TryParse(a3, out count);
                                audio.killcount = count;
                            }
                            else
                            {
                                audio.text = a3;
                            }

                            b.ReportProgress(1, audio);
                        }
                        else if (line.StartsWith("download "))
                        {
                            int len;
                            Parse3(line, out a1, out a2, out a3);
                            Int32.TryParse(a3, out len);
                            var bytes = new byte[len];
                            r.Read(bytes);

                            var download = new DownloadFile();
                            download.name = a2;
                            download.bytes = bytes;
                            b.ReportProgress(1, download);
                        }
                        else if (line.StartsWith("compressed "))
                        {
                            int clen, len;
                            Parse3(line, out a1, out a2, out a3);
                            Int32.TryParse(a2, out clen);
                            Int32.TryParse(a3, out len);

                            // switch to the saved stream
                            savedStream = r;
                            r = UncompressBytes(r, clen);

                            if (r == null)
                                r = savedStream;
                            continue;
                        }
                        else if (line.StartsWith("begin eval "))
                        {
                            pending = new EvalBundle();
                            Parse3(line, out a1, out a2, out a3);
                            pending.path = a3;
                        }
                        else if (line.StartsWith("begin dir "))
                        {
                            pending = new DictBundle();
                            Parse3(line, out a1, out a2, out a3);
                            pending.path = a3;
                        }
                        else if (pending != null && line.StartsWith("v "))
                        {
                            Parse4(line, out a1, out a2, out a3, out a4);
                            if (a1 != "v" || a2 != pending.path)
                                continue;

                            if (!pending.dict.ContainsKey(a3))
                            {
                                pending.dict.Add(a3, a4);
                            }
                        }
                        else if (line.StartsWith("end dir "))
                        {
                            b.ReportProgress(1, pending);
                            pending = null;
                        }
                        else if (line.StartsWith("end eval "))
                        {
                            b.ReportProgress(1, pending);
                            pending = null;
                        }
                        else if (pending == null)
                        {
                            b.ReportProgress(1, line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    b.ReportProgress(0, ex);
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        HybridStreamReader UncompressBytes(HybridStreamReader stream, int clen)
        {
            byte[] bytes = new byte[clen];

            stream.Read(bytes);

            var ms = new MemoryStream(bytes);

            var df = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);

            HybridStreamReader sr = new HybridStreamReader(df);
            return sr;
        }

        public static void Parse2(string s, out string a1, out string a2)
        {
            Parse2Ex(s, out a1,  out a2, ' ');
        }

        public static void RParse2Ex(string s, out string a1, out string a2, char c)
        {
            int iCmd = s.LastIndexOf(c);

            if (iCmd < 0)
            {
                a1 = s;
                a2 = "";
            }
            else
            {
                a1 = s.Substring(0, iCmd);
                a2 = s.Substring(iCmd + 1);
            }
        }

        public static void Parse2Ex(string s, out string a1, out string a2, char c)
        {
            int iCmd = s.IndexOf(c);

            if (iCmd < 0)
            {
                a1 = s;
                a2 = "";
            }
            else
            {
                a1 = s.Substring(0, iCmd);
                a2 = s.Substring(iCmd + 1);
            }
        }

        public static void Parse3(string s, out string a1, out string a2, out string a3)
        {
            string t;

            Parse2(s, out a1, out t);
            Parse2(t, out a2, out a3);
        }

        public static void Parse4(string s, out string a1, out string a2, out string a3, out string a4)
        {
            string t;

            Parse2(s, out a1, out t);
            Parse3(t, out a2, out a3, out a4);
        }

        public void HandleTimer(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker b = (BackgroundWorker)sender;

            for (; ; )
            {
                System.Threading.Thread.Sleep(3000);
                b.ReportProgress(1, null);
            }
        }
    }

    class HybridStreamReader
    {
        Stream s;
        byte[] buffer = new byte[8192];
        int cb;
        int ib;

        public HybridStreamReader(Stream s)
        {
            this.s = s;
            ib = 0;
            cb = 0;
        }

        public string ReadLine()
        {
            string line = "";

            for (; ; )
            {
                int ibStart = ib;
                while (ib < cb)
                {
                    if (buffer[ib] == '\n')
                        break;

                    ib++;
                }

                if (ib > ibStart && buffer[ib-1] == (byte)'\r')
                    line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart - 1);            
                else
                    line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart);

                if (ib < cb)
                {
                    ib++;
                    return line;
                }

                cb = s.Read(buffer, 0, buffer.Length);
                ib = 0;

                if (cb == 0)
                    return null;
            }
        }

        public void Read(byte[] target)
        {
            int offset = 0;
            int len = target.Length;

            for (; ; )
            {
                if (cb - ib >= len)
                {
                    Array.Copy(buffer, ib, target, offset, len);
                    ib += len;
                    return;
                }

                if (cb - ib > 0)
                {
                    Array.Copy(buffer, ib, target, offset, cb - ib);
                    len -= cb - ib;
                    offset += cb - ib;
                }

                cb = s.Read(buffer, 0, buffer.Length);
                ib = 0;

                if (cb == 0)
                    throw new FileFormatException("compressed segment ended too soon");
            }
        }
    }
}
