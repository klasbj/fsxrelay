using System;
using System.Text;
using BeatlesBlog.SimConnect;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace fsx_relay_console
{
    
    class Program
    {
        public static AutoResetEvent exitEvent = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            Logger logger = Logger.GetLogger();

            Console.CancelKeyPress += (a,b) => {
                b.Cancel = true;
                Program.exitEvent.Set();
            };

            logger.Log("Helo");

            var datas = new ConcurrentDictionary<string,object>();

            var httpserver = new Http.HttpServer("http://*:8080/");
            httpserver.Get("/helo", (req,res) => {
                res.SendJson(@"{""answer"":""helo""}");
            });
            httpserver.Get("/instrumentdata", (req,res) => {
                res.SendJson(datas["instrumentdata"]);
            });
            httpserver.Listen();

            //ISimClient sim = new DummySimClient();
            ISimClient sim = new SimClient();
            sim.Disconnected += () => { Program.exitEvent.Set(); };
            sim.InstrumentDataReceived += (client,data) => {
                datas["instrumentdata"] = data;
            };
            Logger.GetLogger().Log("Set up. Starting...");
            sim.Open("Helo");

            Program.exitEvent.WaitOne();
            httpserver.Stop();


            Logger.th.Abort();
        }
    }
}