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
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace GameHost
{
    class IrcServer
    {
        public static IrcServer ircserver;
        public static string servername = "YourServer.";

        class User
        {
            public Socket socket;
            public string nick;
            public NetworkStream stream;
            public AsyncLineReader reader;

            // "USER" command args
            public string username;
            public string hostname;
            public string servername;
            public string realname;
        }

        public class Room
        {
            public string name;
            public Dictionary<string, int> members;
            public string topic;
        }

        Dictionary<string, User> nickDict = new Dictionary<string, User>();
        Dictionary<Stream, User> streamDict = new Dictionary<Stream, User>();
        Dictionary<string, Room> roomDict = new Dictionary<string, Room>();

        public static void Start()
        {
            ircserver = new IrcServer();
        }

        void Parse2(string s, out string a1, out string a2)
        {
            GameHost.Worker.Parse2(s, out a1, out a2);
        }

        void Parse2Ex(string s, out string a1, out string a2, string ch)
        {
            GameHost.Worker.Parse2Ex(s, out a1, out a2, ch);
        }

        void Parse3(string s, out string a1, out string a2, out string a3)
        {
            GameHost.Worker.Parse3(s, out a1, out a2, out a3);
        }

        const int MSG_WELCOME = 001;            // ":Welcome to the Internet Relay Chat network, <you>"
        const int MSG_HOSTINFO = 002;           // ":Your host is " + server + ", running version 1.0.0"
        const int MSG_HOSTCONFIG = 004;         // "<server> GB1.0.0 none none"
        const int MSG_HOSTSPEC = 005;           // "NOQUIT WATCH=128 SAFELIST TUNL :are available on this server"

        const int RPL_LUSERCLIENT = 251;        // ":There are <integer> users and <integer> invisible on <integer> servers"
        const int RPL_LUSEROP = 252;            // "<integer> :operator(s) online"   
        const int RPL_LUSERUNKNOWN = 253;       // "<integer> :unknown connection(s)"
        const int RPL_LUSERCHANNELS = 254;      // "<integer> :channels formed"
        const int RPL_LUSERME = 255;            // ":I have <integer> clients and <integer> servers"

        const int RPL_NOTOPIC = 331;            // "<channel> :No topic is set"
        const int RPL_TOPIC = 332;              // "<channel> :<topic>"

        const int RPL_WHOREPLY = 352;           // "<channel> <user> <host> <server> <nick> <H|G>[*][@|+] :<hopcount> <real name>"
        const int RPL_ENDOFWHO = 315;           // "<name> :End of /WHO list"

        const int RPL_NAMREPLY = 353;           // "<channel> :[[@|+]<nick> [[@|+]<nick> [...]]]"
        const int RPL_ENDOFNAMES = 366;         // "<channel> :End of /NAMES list"

        const int RPL_MOTDSTART = 375;          // ":- <server> Message of the day - "
        const int RPL_MOTD = 372;               // ":- <text>"
        const int RPL_ENDOFMOTD = 376;          // ":End of /MOTD command"

        const int ERR_NONICKNAMEGIVEN = 431;    // ":No nickname given"
        const int ERR_ERRONEUSNICKNAME = 432;
        const int ERR_NICKNAMEINUSE = 433;      // ":Nickname is already in use"

        const int ERR_NOSUCHNICK = 401;         // "<nickname> :No such nick/channel"
        const int ERR_NOSUCHCHANNEL = 403;      // "<channel name> :No such channel"  
        const int ERR_CANNOTSENDTOCHAN = 404;   // "<channel name> :Cannot send to channel"
        const int ERR_WASNOSUCHNICK = 406;      // "<nickname> :There was no such nickname"     
        const int ERR_TOOMANYTARGETS = 407;     // "<target> :Duplicate recipients. No message delivered"

        const int ERR_NOORIGIN = 409;           // ":No origin specified"
        const int ERR_NORECIPIENT = 411;        // ":No recipient given (<command>)"
        const int ERR_NOTEXTTOSEND = 412;       // ":No text to send"
        const int ERR_NOTOPLEVEL = 413;         // "<mask> :No toplevel domain specified"
        const int ERR_WILDTOPLEVEL = 414;       // "<mask> :Wildcard in toplevel domain"
        
        const int ERR_UNKNOWNCOMMAND= 421;      // "<command> :Unknown command"    

        const int ERR_KEYSET = 467;             // "<channel> :Channel key already set"
        const int ERR_CHANNELISFULL = 471;      // "<channel> :Cannot join channel (+l)"
        const int ERR_UNKNOWNMODE = 472;        // "<char> :is unknown mode char to me"
        const int ERR_INVITEONLYCHAN = 473;     // "<channel> :Cannot join channel (+i)"
        const int ERR_BANNEDFROMCHAN = 474;     // "<channel> :Cannot join channel (+b)"
        const int ERR_BADCHANNELKEY = 475;      // "<channel> :Cannot join channel (+k)"

        const int ERR_NOPRIVILEGES = 481;       // ":Permission Denied- You're not an IRC operator"

        IrcServer()
        {
            TcpListener listener = null;

            AsyncCallback callback = null;

            callback = (ar) =>
            {
                Socket sock = listener.EndAcceptSocket(ar);
                NetworkStream stream = new NetworkStream(sock, true);

                var u = new User();
                u.socket = sock;

                u.stream = stream;
                u.nick = "unknown";
                u.reader = new AsyncLineReader(stream, (Stream stm, string line) => ProcessLine(stm, line));

                Console.WriteLine("New incoming IRC connection");

                streamDict.Add(stream, u);

                listener.BeginAcceptSocket(callback, null);
            }; 
            
            try
            {
                listener = new TcpListener(IPAddress.Any, 6667);
                listener.Start();
                listener.BeginAcceptSocket(callback, null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }

        void ProcessLine(Stream stream, string line)
        {
            if (line == null)
            {
                // drop the connection
                return;
            }

            Console.WriteLine(line);

            string cmd, args;
            Parse2(line, out cmd, out args);

            if (cmd.StartsWith(":"))
            {
                line = args;
                Parse2(line, out cmd, out args);
            }

            switch (cmd.ToLower())
            {

                case "ping":
                    HandlePing(stream, args);
                    break;

                case "nick":
                    HandleNick(stream, args);
                    break;

                case "user":
                    HandleUser(stream, args);
                    break;

                case "join":
                    HandleJoin(stream, args);
                    break;

                case "part":
                    HandlePart(stream, args);
                    break;

                case "quit":
                    HandleQuit(stream, args);
                    break;

                case "names":
                    HandleNames(stream, args);
                    break;

                case "topic":
                    HandleTopic(stream, args);
                    break;

                case "who":
                    HandleWho(stream, args);
                    break;

                case "privmsg":
                    HandlePrivmsg(stream, args);
                    break;
            }
        }

        void HandlePing(Stream stream, string args)
        {
            SendOne(stream, "PONG " + servername);
        }

        void HandlePart(Stream stream, string args)
        {
            var room = GetRoom(args);

            if (room == null)
                return;

            User user = GetUser(stream);

            if (user == null)
                return;

            // :Rue!saraiah@RicoMariani.home PART #gameroom
            SendRoom(room.name, GetLongname(user) + " PART " + args, user);
        }

        void HandleNick(Stream stream, string args)
        {
            if (args.StartsWith(":"))
                args = args.Substring(1);

            if (args == "")
            {
                SendReplyToStream(stream, ERR_NONICKNAMEGIVEN, ":No nickname given");
                return;
            }

            if (nickDict.ContainsKey(args))
            {
                SendReplyToStream(stream, ERR_NICKNAMEINUSE, args + " :Nickname is already in use");
                return;
            }

            var user = GetUser(stream);

            if (user != null)
            {
                if (user.nick != "")
                    SendOne(stream, ":" + user.nick + " NICK " + args);

                nickDict.Remove(user.nick);
                nickDict.Add(args, user);

                ChangeNick(user.nick, args);
                user.nick = args;
            }
        }

        void ChangeNick(string prev, string curr)
        {
            foreach (var r in roomDict.Values)
            {
                if (r.members.ContainsKey(prev))
                {
                    r.members.Remove(prev);
                    r.members.Add(curr, 1);
                }
            }
        }

        public void HandleQuit(Stream stream, string msg)
        {
            // explicit lock because this method can be called from the catch blocks and we won't have the lock at that time
            lock (this)
            {
                var u = GetUser(stream);

                if (u == null)
                    return;

                nickDict.Remove(u.nick);

                streamDict.Remove(stream);

                foreach (var room in roomDict.Values)
                {
                    if (room.members.ContainsKey(u.nick))
                    {
                        room.members.Remove(u.nick);
                        SendRoom(room.name, GetLongname(u) + " PART " + room.name, u);
                    }
                }

                SendAll(GetLongname(u) + " QUIT " + msg, u);

                stream.Close();
            }
        }

        void HandleUser(Stream stream, string args)
        {
            if (stream == null)
                return;

            string tail;
            var user = GetUser(stream);

            if (user == null)
                return;

            GameHost.Worker.Parse2(args, out user.username, out tail);
            args = tail;
            GameHost.Worker.Parse2(args, out user.hostname, out tail);
            args = tail;
            GameHost.Worker.Parse2(args, out user.servername, out user.realname);
            args = tail;

            if (user.username != "" && user.hostname != "" && user.servername != "" && user.realname != "")
            {
                AnnounceIntro(stream);
            }
        }

        public void AnnounceIntro(Stream stream)
        {
            // :YourServer. 001 Sorsa :Welcome to the Internet Relay Chat network, Sorsa!saraiah@RicoMariani.home
            // :YourServer. 002 Sorsa :Your host is YourServer., running version 1.7.6
            // :YourServer. 004 Sorsa YourServer. CR1.7.6 oiwsabjgrchytxkmnpeAEGFSLX abcdeijklmnoprstuvAMNL
            // :YourServer. 005 Sorsa NOQUIT WATCH=128 SAFELIST TUNL :are available on this server
            // :YourServer. 251 Sorsa :There are 1 users and 3 invisible on 1 servers
            // :YourServer. 252 Sorsa 1 :operator(s) online
            // :YourServer. 254 Sorsa 4 :channels formed
            // :YourServer. 255 Sorsa :I have 4 clients and 0 servers
            // :YourServer. 265 Sorsa :Current local users: 4  Max: 8
            // :YourServer. 266 Sorsa :Current global users: 4  Max: 8
            // :YourServer. 375 Sorsa :- YourServer. Message of the Day -
            // :YourServer. 372 Sorsa :- Welcome. Please respect the rules of this
            // :YourServer. 372 Sorsa :- network No Nuking allowed, No child pornography,
            // :YourServer. 372 Sorsa :- No advertising, No war scripts will be accepted here,
            // :YourServer. 372 Sorsa :- And NO Trojans. DCC sending of those viruses is NOT allow
            // :YourServer. 372 Sorsa :- Network Staff :)
            // :YourServer. 376 Sorsa :End of /MOTD command.
            // :Sorsa!saraiah@RicoMariani-PC.home MODE Sorsa :+ixnE  

            var u = GetUser(stream);

            var longname = GetLongname(u);

            SendReplyToStream(stream, MSG_WELCOME, ":Welcome to the Internet Relay Chat network, " + longname);
            SendReplyToStream(stream, MSG_HOSTINFO, ":Your host is " + servername + ", running version 1.0.0");
            SendReplyToStream(stream, MSG_HOSTCONFIG, servername + " GB1.0.0 none none");
            SendReplyToStream(stream, MSG_HOSTSPEC, "NOQUIT WATCH=128 SAFELIST TUNL :are available on this server");
            SendReplyToStream(stream, RPL_LUSERCLIENT, String.Format(":There are {0} users and 0 invisible on 1 servers", nickDict.Count));
            SendReplyToStream(stream, RPL_LUSEROP, "0 :operator(s) online");
            SendReplyToStream(stream, RPL_LUSERCHANNELS, "0 :channels formed");
            SendReplyToStream(stream, RPL_LUSERME, String.Format(":I have {0} clients and 0 servers", nickDict.Count));

            // message of the day
            SendReplyToStream(stream, RPL_MOTDSTART, ":- " + servername + " Message of the Day -");
            SendReplyToStream(stream, RPL_MOTD, ":- Welcome. Please respect the rules of this");
            SendReplyToStream(stream, RPL_MOTD, ":- network No Nuking allowed, No child pornography,");
            SendReplyToStream(stream, RPL_MOTD, ":- No advertising, No war scripts will be accepted here,");
            SendReplyToStream(stream, RPL_MOTD, ":- And NO Trojans. DCC sending of those viruses is NOT allow");
            SendReplyToStream(stream, RPL_MOTD, ":- Network Staff :)");
            SendReplyToStream(stream, RPL_ENDOFMOTD, ":End of /MOTD command.");

            SendOne(stream, longname + " MODE " + u.nick + " :+ixnE"); // whatever that means
        }

        static string GetLongname(User u)
        {
            return String.Format("{0}!{1}@{2}.{3}", u.nick, u.username, u.hostname, u.servername);
        }


        void SendRoom(string roomname, string data, User exception = null)
        {
            Room room = GetRoom(roomname);

            if (room == null)
                return;

            var members = room.members;

            foreach (var member in members.Keys)
            {
                User user = GetUser(member);

                if (user == null)
                    continue;

                if (user != exception)
                    SendOne(user.stream, data);
            }
        }

        void SendAll(string data, User exception = null)
        {
            var values = streamDict.Values.ToArray();

            foreach (var user in values)
            {
                if (user != exception)
                    SendOne(user.stream, data);
            }
        }

        // join #gameroom
        // :RM!u1@RicoMariani-PC.home JOIN :#gameroom

        // :YourServer. 332 RM #gameroom :Jan 24 2015 session posted
        // :YourServer. 333 RM #gameroom Bigglesworth 1422202266
        // :Rue!saraiah@RicoMariani-PC.home JOIN :#gameroom


        Room GetRoom(string name)
        {
            name = name.ToLower();

            Room room = null;

            if (!roomDict.TryGetValue(name, out room))
                return null;

            return room;
        }

        void HandleJoin(Stream stream, string args)
        {
            string nick = GetNick(stream);

            if (nick == "")
            {
                SendOne(stream, ":You have no nick.");
                return;
            }

            while (args != "")
            {
                string roomname, tail;
                Parse2Ex(args, out roomname, out tail, ",");
                args = tail;

                if (!roomname.StartsWith("#"))
                {
                    SendReplyToStream(stream, ERR_BADCHANNELKEY, String.Format("{0} :Cannot join channel", roomname));
                    return;
                }

                Room room = GetRoom(roomname);

                if (room == null)
                {
                    room = CreateRoom(roomname);
                }

                if (room.members.ContainsKey(nick))
                {
                    return;
                }

                room.members.Add(nick, 1);

                var user = GetUser(stream);
                var longname = GetLongname(user);

                // :RM!u1@RicoMariani-PC.home JOIN :#gameroom
                SendRoom(room.name, ":" + longname + " JOIN :" + room.name);
                HandleNames(stream, room.name);

                if (room.topic != "")
                    SendReplyToStream(stream, RPL_TOPIC, room.name + " :" + room.topic);
                else
                    SendReplyToStream(stream, RPL_NOTOPIC, room.name + " :No topic is set");
            }
        }

        Room CreateRoom(string roomname)
        {
            roomname = roomname.ToLower();

            var room = new Room();
            room.name = roomname;
            room.members = new Dictionary<string, int>();
            roomDict.Add(roomname, room);
            return room;
        }

        void HandleTopic(Stream stream, string args)
        {
            string roomname, topic;
            Parse2(args, out roomname, out topic);

            if (topic.StartsWith(":"))
                topic = topic.Substring(1);

            Room room = GetRoom(roomname);

            if (room == null)
                return;

            room.topic = topic;

            if (room.topic != "")
                SendReplyToRoom(room, RPL_TOPIC, room.name + " :" + room.topic);
            else
                SendReplyToRoom(room, RPL_NOTOPIC, room.name + " :No topic is set");
        }

        void HandleNames(Stream stream, string args)
        {
            // :YourServer. 353 RM = #gameroom :RM GM_Athenor @__ Lluvia
            // :YourServer. 366 RM #gameroom :End of /NAMES list.
            string nick = GetNick(stream);

            while (args != "")
            {
                string roomname, tail;
                Parse2Ex(args, out roomname, out tail, ",");
                args = tail;

                Room room = GetRoom(roomname);
                if (room == null)
                    continue;

                string names = "";
                foreach (var k in room.members.Keys)
                {
                    var key = k;
                    if (key == "__")
                        key = "@__";

                    if (names.Length == 0)
                        names = key;
                    else
                        names = names + " " + key;
                }

                if (room != null)
                {
                    SendReplyToStream(stream, RPL_NAMREPLY, nick + " = " + roomname + " :" + names);
                    SendReplyToStream(stream, RPL_ENDOFNAMES, nick+" "+roomname+ " :End of /NAMES list.");
                }
            }   
        }

        void HandlePrivmsg(Stream stream, string args)
        {
            string target;
            string msg;

            Parse2(args, out target, out msg);

            if (msg == "")
                return;


            // :Rue!saraiah@RicoMariani-PC.home PRIVMSG #gameroom :foo
            // :Rue!saraiah@RicoMariani-PC.home PRIVMSG RM :foo

            string longname = GetLongname(GetUser(stream));

            string data = ":" + longname + " PRIVMSG " + target + " " + msg;

            var origin = GetUser(stream);

            if (target.StartsWith("#"))
            {
                SendRoom(target, data, origin);
            }
            else
            {
                var user = GetUser(target);
                if (user != null)
                    SendOne(user.stream, data);
            }

        }


        public void SendReplyToRoom(Room room, int code, string msg)
        {
            foreach (var nick in room.members.Keys)
            {
                User u = GetUser(nick);
                if (u == null)
                    continue;

                SendReplyToStream(u.stream, code, msg);
            }
        }


        public void SendReplyToStream(Stream stream, int code, string msg)
        {
            string c = code.ToString();

            if (c.Length == 1)
                c = "00" + c;

            if (c.Length == 2)
                c = "0" + c;

            var nick = GetNick(stream);

            if (nick == "")
                return;
            
            var resp = String.Format(":{0} {1} {2} {3}\n", servername, c, nick, msg);

            SendOne(stream, resp);
        }

        string GetNick(Stream stream)
        {
            User u;

            if (!streamDict.TryGetValue(stream, out u))
                return "";

            return u.nick;
        }

        User GetUser(Stream stream)
        {
            User u;

            if (!streamDict.TryGetValue(stream, out u))
                return null;

            return u;
        }

        User GetUser(string nick)
        {
            User u;

            if (!nickDict.TryGetValue(nick, out u))
                return null;

            return u;             
        }

        void HandleWho(Stream stream, string args)
        {
            // :YourServer. 352 yournick * u1 RicoMariani-PC.home YourServer. whonick H% :1 u4
            // :YourServer. 315 yournick whonick :End of /WHO list.
            

            string nick = GetNick(stream);

            while (args != "")
            {
                string who, tail;
                Parse2Ex(args, out who, out tail, ",");
                args = tail;

                User u = GetUser(who);
                if (u == null)
                    continue;

                SendReplyToStream(stream, RPL_WHOREPLY, "* " + u.username + " " + u.hostname + " " + servername + " " + who + " :1 " + u.realname);
                SendReplyToStream(stream, RPL_ENDOFWHO, who + " :End of /WHO list");
            }
        }


        public void SendOne(Stream stream, String data)
        {
            if (!data.EndsWith("\n"))
                data = data + "\n";

            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // broadcast could come from another thread
            try
            {
                // send the data to the remote device.
                stream.Write(byteData, 0, byteData.Length);
            }
            catch
            {
                IrcServer.ircserver.HandleQuit(stream, ":DISCONNECTED");
            }
        }
    }


    class AsyncLineReader
    {
        Stream stream;
        byte[] buffer = new byte[8192];
        string line;
        Action<Stream, string> action;

        public AsyncLineReader(Stream _stream, Action<Stream, string> _action)
        {
            stream = _stream;
            action = _action;
            BeginReadLine();
        }

        void BeginReadLine()
        {
            AsyncCallback callback = null;

            callback = (result) =>
                {
                    int cb = 0;

                    try
                    {
                        cb = stream.EndRead(result);
                    }
                    catch (Exception)
                    {
                        IrcServer.ircserver.HandleQuit(stream, ":DISCONNECTED");
                        cb = 0;
                    }

                    if (cb > 0)
                    {
                        ProcessBuffer(cb);

                        try
                        {
                            stream.BeginRead(buffer, 0, buffer.Length, callback, null);
                        }
                        catch (Exception)
                        {
                            IrcServer.ircserver.HandleQuit(stream, ":DISCONNECTED");
                        }
                    }
                };

            stream.BeginRead(buffer, 0, buffer.Length, callback, null);
        }

        void ProcessBuffer(int cb)
        {
            if (cb == 0)
            {
                action(stream, null);
                return;
            }

            int ibStart = 0;
            int ib = 0;

            for (; ; )
            {
                while (ib < cb)
                {
                    if (buffer[ib] == '\n')
                        break;

                    ib++;
                }

                if (ib >= 1 && buffer[ib - 1] == (byte)'\r')
                    line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart - 1);
                else
                    line += Encoding.ASCII.GetString(buffer, ibStart, ib - ibStart);

                if (ib < cb)
                {
                    ib++;
                    lock (this)
                    {
                        action(stream, line);
                    }
                    line = "";
                    ibStart = ib;
                }
                else
                {
                    break;
                }
            }

        }
    }
}
