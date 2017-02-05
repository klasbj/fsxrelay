using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace fsx_relay_console
{
    class Logger
    {
        private static Logger logger = null;
        public static Thread th = null;
        public static Logger GetLogger()
        {
            if (logger == null)
            {
                logger = new Logger();
                th = new Thread(new ParameterizedThreadStart(run));
                th.Start(logger);
            }
            return logger;
        }

        private static void run(object o)
        {
            string msg;
            Logger l = (Logger)o;
            while (true)
            {
                while (l.queue.TryDequeue(out msg))
                {
                    Console.WriteLine(msg);
                }
                Thread.Sleep(1000);
            }
        }

        private ConcurrentQueue<string> queue;

        private Logger()
        {
            queue = new ConcurrentQueue<string>();
        }

        public void Log(string message)
        {
            var id = Thread.CurrentThread.ManagedThreadId;
            queue.Enqueue($"{DateTime.UtcNow:HH:mm:ss.fffZ} {id}: {message}");
        }
    }
}
