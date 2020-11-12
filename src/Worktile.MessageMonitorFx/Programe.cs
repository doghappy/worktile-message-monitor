using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.WebSocketClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Worktile.MessageMonitorFx
{
    class Program
    {
        static JObject Configuration { get; set; }
        static HttpClient HttpClient { get; set; }
        static string Domain { get; set; }

        static async Task Main(string[] args)
        {
            Initialize();
            await LoginAsync();
            var connectionInfo = await GetConnectionInfoAsync();
            var client = new SocketIO(connectionInfo.Uri, new SocketIOOptions
            {
                Query = new Dictionary<string, string>
                {
                    { "token", connectionInfo.Token },
                    { "uid", connectionInfo.Uid },
                    { "client", "web" }
                },
            });

            if (client.Socket is WebSocketSharpClient)
            {
                var socket = client.Socket as WebSocketSharpClient;
                socket.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls;
                socket.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }
                    Console.WriteLine(sslPolicyErrors);
                    return false;
                };
            }
            else
            {
                var socket = client.Socket as ClientWebSocket;
                // .NET Framework：
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }
                    Console.WriteLine(sslPolicyErrors);
                    return false;
                };
            }


            client.OnConnected += Client_OnConnected;
            client.OnPing += Client_OnPing;
            client.OnPong += Client_OnPong;
            client.OnDisconnected += Client_OnDisconnected;

            Console.WriteLine("Connecting...");
            await client.ConnectAsync();

            Console.ReadLine();
        }

        private static void Client_OnDisconnected(object sender, string e)
        {
            Console.WriteLine("Disconnected: " + e);
        }

        private static void Client_OnPong(object sender, TimeSpan e)
        {
            Console.WriteLine("Pong: " + e.TotalMilliseconds);
        }

        private static void Client_OnPing(object sender, EventArgs e)
        {
            Console.WriteLine("Ping");
        }

        private static void Client_OnConnected(object sender, EventArgs e)
        {
            var client = sender as SocketIO;

            //client.On("ready", response =>
            //{
            //    Console.WriteLine(response.GetValue().ToString());
            //});
            Console.WriteLine("Connected, Id: " + client.Id);
            client.On("message", response =>
            {
                Console.WriteLine("** message:");
                Console.WriteLine(response.GetValue().ToString());
            });
            //client.On("feed", response =>
            //{
            //    Console.WriteLine("** feed:");
            //    Console.WriteLine(response.GetValue().ToString());
            //});
        }

        static void Initialize()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            string json = File.ReadAllText("config.json");
            Configuration = JObject.Parse(json);
            Domain = Configuration.Value<string>("domain");
            HttpClient = new HttpClient
            {
                BaseAddress = new Uri($"https://{Domain}.worktile.com")
            };
        }

        static async Task LoginAsync()
        {
            await PostAsync("api/account/team/signin", new
            {
                signin_name = Configuration.Value<string>("user"),
                password = Configuration.Value<string>("password"),
                team_id = Configuration.Value<string>("teamId")
            });
        }

        static async Task<(Uri Uri, string Token, string Uid)> GetConnectionInfoAsync()
        {
            var response = await GetAsync("api/user/me");
            string uri = response["data"]["config"]["feed"].Value<string>("newHost");
            string token = response["data"]["me"].Value<string>("imToken");
            string uid = response["data"]["me"].Value<string>("uid");
            return (new Uri(uri), token, uid);
        }

        static async Task<JObject> GetAsync(string uri)
        {
            string json = await HttpClient.GetStringAsync(uri);
            return JObject.Parse(json);
        }

        static async Task<JObject> PostAsync(string uri, object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            var resMsg = await HttpClient.PostAsync(uri, stringContent);
            string resJson = await resMsg.Content.ReadAsStringAsync();
            return JObject.Parse(resJson);
        }
    }
}
