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
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace GameHost
{
    class Program
    {
        static public bool fConsoleMode = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Startup");

            HttpResponder.StartHttpListener();
            IrcServer.Start();

            if (args.Length == 1 && args[0] == "console")
            {
                fConsoleMode = true;

                new Worker().loadparty();     

                GameBot.Bot b = new GameBot.Bot("roller", "roller");
                b.ConsoleLoop();
                return;
            }

            new Worker().loadparty();            

            Thread botThread = new Thread(GameBot.Bot.Start);
            botThread.Start();

            System.Net.Sockets.TcpListener listener;

            listener = new TcpListener(IPAddress.Any, 6668);

            listener.Start();

            for (; ; )
            {
                TcpClient client = listener.AcceptTcpClient();

                Console.WriteLine("Accepting a connection");

                Thread t = new Thread(Program.DoWork);
                t.Start(client);

            }
        }

        public static Dictionary<string, Dict> master = new Dictionary<string, Dict>();
        public static Dictionary<string, DateTime> serverState = new Dictionary<string, DateTime>();
        public static List<Worker> workerList = new List<Worker>();
        public static Dictionary<string, int> killcounts = new Dictionary<string, int>();

        public static void DoWork(object data)
        {
            Console.WriteLine("Static thread procedure. Data='{0}'",data);

            TcpClient client = (TcpClient)data;

            NetworkStream stream = client.GetStream();

            Worker w = new Worker(client, stream);

            lock (workerList)
            {
                workerList.Add(w);
            }

            w.DoWork();

            lock (workerList)
            {
                workerList.Remove(w);
            }

            Console.WriteLine("closing");

            client.Close();
        }

        public static void Broadcast(string origin, string data)
        {
            lock (workerList)
            {
                Console.WriteLine("Broadcast {0} {1}", origin, data);
                foreach (var w in workerList)
                {
                    try
                    {
                        if (w.myroom == origin)
                        {
                            w.SendToGameaid(data);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}
