using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace fsx_relay_console
{
    namespace Http
    {
        public enum HttpMethod
        {
            GET,
            POST,
            PUT,
            DELETE
        }
        public class HttpRequest
        {
            public System.Net.HttpListenerRequest Request { get; }

            public HttpRequest(System.Net.HttpListenerRequest r)
            {
                this.Request = r;
            }
        }

        public class HttpResponse
        {
            public System.Net.HttpListenerResponse Response { get; }

            public HttpResponse(System.Net.HttpListenerResponse r)
            {
                this.Response = r;
            }

            public void SendJson(string jsonText)
            {
                this.Response.ContentType = "text/json";
                this.Response.StatusCode = 200;
                this.Response.ContentEncoding = Encoding.UTF8;

                byte[] bytes = Encoding.UTF8.GetBytes(jsonText);
                this.Response.ContentLength64 = bytes.Length;
                this.Response.OutputStream.Write(bytes, 0, bytes.Length);
                this.Response.OutputStream.Close();
            }

            public void SendJson<T>(T o)
            {
                this.SendJson(JsonConvert.SerializeObject(o));
            }
        }

        public delegate void HttpHandler(HttpRequest req, HttpResponse res);

        public class HttpServer
        {

            protected struct Route
            {
                string route { get; set; }
                HttpMethod method { get; set; }
                public HttpHandler Handler { get; }

                public Route(string route, HttpMethod method, HttpHandler handler) {
                    this.route = route;
                    this.method = method;
                    this.Handler = handler;
                }

                public bool Matches(HttpMethod method, string path) {
                    return this.method == method && this.route == path;
                }

                public bool Call(System.Net.HttpListenerContext ctx)
                {
                    Handler(new HttpRequest(ctx.Request), new HttpResponse(ctx.Response));
                    return true;
                }
            }

            private System.Net.HttpListener httpListener;

            private System.Collections.Generic.IList<Route> routes;
            public HttpServer(string prefix)
            {
                this.httpListener = new System.Net.HttpListener();
                this.httpListener.Prefixes.Add(prefix);

                this.routes = new System.Collections.Generic.List<Route>();
            }

            public void Get(string route, HttpHandler handler)
            {
                this.routes.Add(new Route(route, HttpMethod.GET, handler));
            }

            public void Post(string route, HttpHandler handler)
            {
                this.routes.Add(new Route(route, HttpMethod.POST, handler));
            }

            protected void Dispatch(System.Net.HttpListenerContext ctx)
            {
                bool handled = false;
                int status = 200;
                try
                {
                    var method = (HttpMethod)Enum.Parse(typeof(HttpMethod), ctx.Request.HttpMethod, true);

                    foreach (var r in this.routes)
                    {
                        if (r.Matches(method, ctx.Request.Url.AbsolutePath) && r.Call(ctx))
                        {
                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        Logger.GetLogger().Log($"Unhandled HTTP request to {ctx.Request.RawUrl}");
                        status = 404;
                    }
                }
                catch (System.Exception e)
                {
                    Logger.GetLogger().Log($"Exception while handling HTTP request to {ctx.Request.RawUrl}{Environment.NewLine}{e}");
                    status = 500;
                }
                if (!handled)
                {
                    ctx.Response.StatusCode = status;
                    ctx.Response.OutputStream.Close();
                }
            }

            public void Listen()
            {
                var logger = Logger.GetLogger();
                this.httpListener.Start();
                

                ThreadPool.QueueUserWorkItem((_) => {
                    logger.Log("HttpServer listening...");
                    try
                    {
                        while (this.httpListener.IsListening)
                        {
                            ThreadPool.QueueUserWorkItem((o) => {
                                var ctx = o as System.Net.HttpListenerContext;
                                this.Dispatch(ctx);
                            }, this.httpListener.GetContext());
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log("Caught exception: " + e.ToString());
                    }
                });
            }

            public void Stop()
            {
                this.httpListener.Stop();
                this.httpListener.Close();
            }
            
        }
    }


}