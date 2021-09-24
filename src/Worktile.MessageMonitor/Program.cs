using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Worktile.MessageMonitor
{
    class Program
    {
        static JObject Configuration { get; set; }
        static HttpClient HttpClient { get; set; }
        static string Domain { get; set; }

        static async Task Main(string[] args)
        {
            GetOSInfo();
            Initialize();
            await LoginAsync();
            var connectionInfo = await GetConnectionInfoAsync();
            var client = new SocketIO(connectionInfo.Uri, new SocketIOOptions
            {
                EIO = 3,
                Query = new Dictionary<string, string>
                {
                    { "token", connectionInfo.Token },
                    { "uid", connectionInfo.Uid },
                    { "client", "web" }
                }
            });

            client.ClientWebSocketProvider = () =>
            {
                var clientWebSocket = new DefaultClientWebSocket
                {
                    ConfigOptions = o =>
                    {
                        var options = o as System.Net.WebSockets.ClientWebSocketOptions;
                        options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            Console.WriteLine("SslPolicyErrors: " + sslPolicyErrors);
                            return true;
                        };
                    }
                };
                return clientWebSocket;
            };

            client.OnConnected += Client_OnConnected;
            client.OnPing += Client_OnPing;
            client.OnPong += Client_OnPong;
            client.OnDisconnected += Client_OnDisconnected;
            client.OnReconnectAttempt += Client_OnReconnectAttempt;

            Console.WriteLine("Connecting...");
            await client.ConnectAsync();

            Console.ReadLine();
        }

        private static void Client_OnReconnectAttempt(object sender, int e)
        {
            Console.WriteLine("Attemp: " + e);
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


        public static string GetOSInfo()
        {
            //Get Operating system information.
            OperatingSystem os = Environment.OSVersion;
            //Get version information about the os.
            Version vs = os.Version;

            //Variable to hold our return value
            string operatingSystem = "";

            if (os.Platform == PlatformID.Win32Windows)
            {
                //This is a pre-NT version of Windows
                switch (vs.Minor)
                {
                    case 0:
                        operatingSystem = "95";
                        break;
                    case 10:
                        if (vs.Revision.ToString() == "2222A")
                            operatingSystem = "98SE";
                        else
                            operatingSystem = "98";
                        break;
                    case 90:
                        operatingSystem = "Me";
                        break;
                    default:
                        break;
                }
            }
            else if (os.Platform == PlatformID.Win32NT)
            {
                switch (vs.Major)
                {
                    case 3:
                        operatingSystem = "NT 3.51";
                        break;
                    case 4:
                        operatingSystem = "NT 4.0";
                        break;
                    case 5:
                        if (vs.Minor == 0)
                            operatingSystem = "2000";
                        else
                            operatingSystem = "XP";
                        break;
                    case 6:
                        if (vs.Minor == 0)
                            operatingSystem = "Vista";
                        else if (vs.Minor == 1)
                            operatingSystem = "7";
                        else if (vs.Minor == 2)
                            operatingSystem = "8";
                        else
                            operatingSystem = "8.1";
                        break;
                    case 10:
                        operatingSystem = "10";
                        break;
                    default:
                        break;
                }
            }
            //Make sure we actually got something in our OS check
            //We don't want to just return " Service Pack 2" or " 32-bit"
            //That information is useless without the OS version.
            if (operatingSystem != "")
            {
                //Got something.  Let's prepend "Windows" and get more info.
                operatingSystem = "Windows " + operatingSystem;
                //See if there's a service pack installed.
                if (os.ServicePack != "")
                {
                    //Append it to the OS name.  i.e. "Windows XP Service Pack 3"
                    operatingSystem += " " + os.ServicePack;
                }
                //Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
                //operatingSystem += " " + getOSArchitecture().ToString() + "-bit";
            }
            //Return the information we've gathered.
            Console.WriteLine(operatingSystem);
            return operatingSystem;
        }
    }
}
