using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BeatlesBlog.SimConnect;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Globalization;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace fsx_relay_console
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger logger = Logger.GetLogger();

            SC sc = new SC();

            Console.CancelKeyPress += (a,b) => {
                if (sc != null)
                {
                    b.Cancel = true;
                    sc.exitEvent.Set();
                }
            };

            logger.Log("Helo");

            var httpserver = new Http.HttpServer("http://*:8080/");
            httpserver.Get("/helo", (req,res) => {
                res.SendJson(@"{""answer"":""helo""}");
            });
            httpserver.Listen();

            sc.Run();
            sc = null;
            httpserver.Stop();


            Logger.th.Abort();
        }
    }

    enum Requests
    {
        InstrumentData,
        AirspeedOnce
    }


    class SC
    {
        UdpClient socket = null;
        SimConnect sc = null;
        public AutoResetEvent exitEvent = new AutoResetEvent(false);
        ConcurrentDictionary<IPEndPoint, DateTime> clients = null;

        public SC()
        {
            sc = new SimConnect();
            socket = new UdpClient(new IPEndPoint(IPAddress.Any, 34234));
            clients = new ConcurrentDictionary<IPEndPoint, DateTime>();
        }

        ~SC()
        {
            sc = null;
            socket = null;
        }

        public void SendToAll(string msg)
        {
            var time = DateTime.Now;
            byte[] bytes = Encoding.UTF8.GetBytes(msg);
            foreach (var client in clients)
            {
                if (time - client.Value < TimeSpan.FromMinutes(10.0))
                {
                    socket.Send(bytes, bytes.Length, client.Key);
                }
            }
        }

        public void Send(string msg, IPEndPoint ep)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(msg);
            socket.Send(bytes, bytes.Length, ep);
        }

        public void Register(IPEndPoint ep)
        {
            Logger.GetLogger().Log("Register: " + ep);
            clients.AddOrUpdate(ep, DateTime.Now, (e, d1) => DateTime.Now);
        }

        enum CommandType
        {
            KeepAlive,
            Set
        }
        struct Command
        {
            public CommandType command;
            public String[] args;
        }

        public void Receive(IAsyncResult iar)
        {
            UdpClient s = (UdpClient)iar.AsyncState;
            IPEndPoint ep = new IPEndPoint(0, 1);
            var bytes = socket.EndReceive(iar, ref ep);
            Logger.GetLogger().Log("Receive: " + bytes.Length + " from " + ep);
            string msg = Encoding.UTF8.GetString(bytes);
            Logger.GetLogger().Log("Receive: " + msg);
            Command cmd = JsonConvert.DeserializeObject<Command>(msg);
            switch (cmd.command)
            {
                case CommandType.KeepAlive:
                    Register(ep);
                    Send("{\"keepalive\":true}", ep);
                    break;
                case CommandType.Set:
                    break;
                default:
                    break;
            }
            s.BeginReceive(new AsyncCallback(Receive), s);

        }

        public void Run()
        {
            //socket.BeginReceive(Receive, socket);
            ISimClient sim = new DummySimClient();
            sim.Disconnected += () => { exitEvent.Set(); };
            sim.InstrumentDataReceived += (client,data) => {
                var js = JsonConvert.SerializeObject(data);
                //Logger.GetLogger().Log(js);
                this.SendToAll(js);
            };
            Logger.GetLogger().Log("Set up. Starting...");
            sim.Open("Helo");

            exitEvent.WaitOne();
            //socket.Close();
        }
        // public void Run()
        // {
        //     // setup some hooks
        //     sc.OnRecvOpen += new SimConnect.RecvOpenEventHandler(sc_OnRecvOpen);
        //     sc.OnRecvException += new SimConnect.RecvExceptionEventHandler(sc_OnRecvException);
        //     sc.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(sc_OnRecvSimobjectData);
        //     sc.OnRecvQuit += new SimConnect.RecvQuitEventHandler(sc_OnRecvQuit);

        //     socket.BeginReceive(new AsyncCallback(Receive), socket);

        //     if (SimConnect.IsLocalRunning())
        //     {
        //         Logger.GetLogger().Log("Starting SimConnect");
        //         sc.Open("FSX_Relay");
        //     }
        //     else
        //     {
        //         sc.Open("FSX_Relay", "fractran", 4711);
        //         //Logger.GetLogger().Log("No local instance of FSX is running.");
        //         //send.Close();
        //         //return;
        //     }

        //     exitEvent.WaitOne();
        //     socket.Close();
        // }

        // void sc_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        // {
        //     Logger.GetLogger().Log("Received quit from FSX.");
        //     exitEvent.Set();

        // }

        // void sc_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        // {
        //     switch ((Requests)data.dwRequestID)
        //     {
        //         case Requests.InstrumentData:
        //             InstrumentData ind = (InstrumentData)data.dwData;
        //             string js = JsonConvert.SerializeObject(ind);
        //             //Logger.GetLogger().Log("Got and sending data: " + js);
        //             //Console.ReadLine();
        //             //exitEvent.Set();
        //             //return;
        //             SendToAll(js);
        //             break;
        //         default:
        //             break;
        //     }
        // }

        // void sc_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        // {
        //     Logger.GetLogger().Log("Received exception: " + (SIMCONNECT_EXCEPTION)data.dwException + ", " + data.dwIndex);
        // }

        // void sc_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        // {
        //     sc.RequestDataOnUserSimObject(Requests.InstrumentData, SIMCONNECT_PERIOD.SIM_FRAME, typeof(InstrumentData));
        // }
    }
}
