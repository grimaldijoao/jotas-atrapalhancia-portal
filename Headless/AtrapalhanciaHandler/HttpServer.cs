using AtrapalhanciaDatabase;
using AtrapalhanciaDatabase.Tables;
using Headless.Shared;
using Headless.Shared.Interfaces;
using Headless.Shared.Utils;
using Newtonsoft.Json;
using System.Text.Json;
using System.Net;
using System.Text;
using AtrapalhanciaHandler;
using AtrapalhanciaWebSocket;
using Shared.Utils;

namespace Headless.AtrapalhanciaHandler
{
    public class GameConnectedEventArgs : EventArgs
    {
        private readonly string _broadcasterId;
        private readonly string _channelName;
        private readonly string _accessToken;
        private readonly string _gameName;
        private readonly string _route;

        public string BroadcasterId => _broadcasterId;
        public string ChannelName => _channelName;
        public string AccessToken => _accessToken;
        public string Game => _gameName;
        public string Route => _route;

        public GameConnectedEventArgs(string broadcasterId, string channel, string accessToken, string game, string route)
        {
            _broadcasterId = broadcasterId;
            _channelName = channel;
            _accessToken = accessToken;
            _gameName += game;
            _route += route;
        }
    }

    public class AtrapalhanciaFileUpdateBroadcaster
    {
        FileSystemWatcher watcher = new FileSystemWatcher();
        CancellationTokenSource? cancelTokenSource = null;

        public AtrapalhanciaFileUpdateBroadcaster()
        {

        }

        public void Watch()
        {
            if(!Directory.Exists(Environment.CurrentDirectory + "/Atrapalhancias"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "/Atrapalhancias");
            }

            watcher.Path = Environment.CurrentDirectory + "/Atrapalhancias";

            watcher.NotifyFilter = NotifyFilters.LastWrite;

            watcher.Filter = "*.dll";

            watcher.Changed += new FileSystemEventHandler(OnChanged);

            watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                Action reload = () =>
                {
                    //TODO mais de um service?
                    //Server.WebSocketServices["/channel/umjotas"].Sessions.Broadcast("reload"); //TODO mais de um user conectado no umjotas?
                };
                reload.Debounce(ref cancelTokenSource, 375);
            }
        }
    }

    public class HttpServer : IDisposable
    {
        private const string url = "http://+:8000/";
        private CancellationTokenSource CancellationToken = new CancellationTokenSource();

        private ITokenDecoder TokenDecoder;
        private HttpListener? listener;
        private WebSocketServer SocketServer;

        private long requestCount = 0;

        public static event EventHandler<GameConnectedEventArgs>? OnGameConnected;
        public static event EventHandler<string>? OnSocketCreationRequested;

        private string twitch_client_secret = File.ReadAllText("client_secret.txt");

        public HttpServer(ITokenDecoder tokenDecoder, WebSocketServer socketServer)
        {
            TokenDecoder = tokenDecoder;
            SocketServer = socketServer;
        }

        private void AddCORSHeaders(HttpListenerResponse resp)
        {
            resp.AddHeader("Access-Control-Allow-Origin", "*");
            resp.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");
        }

        private Dictionary<string,string> GetRequestBodyJSON(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                var maybe = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<Dictionary<string,string>>(maybe)!;
            }
        }

        private byte[] GetJSON(object json)
        {
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json);
        }

        private byte[] Ok()
        {
            return GetJSON(new { success = true });
        }

        private byte[] NotFound()
        {
            return GetJSON(new { error = "Rota não encontrada." });
        }

        private byte[] AtrapalhanciaNotFound()
        {
            return GetJSON(new { error = "Atrapalhancia não encontrada." });
        }

        private void RespondJSON(ref HttpListenerResponse resp, byte[] json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            AddCORSHeaders(resp);
            resp.StatusCode = (int)statusCode;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = json.Length;
            resp.OutputStream.WriteAsync(json, 0, json.Length).GetAwaiter().GetResult();
            resp.Close();
        }

        private Task HandleIncomingConnections()
        {
            bool runServer = true;

            while (runServer)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(CancellationToken.Token);
                }

                HttpListenerContext ctx = listener.GetContextAsync().GetAwaiter().GetResult();

                var bannedHosts = new string[] { "agenciakom.com" };

                new Task(() =>
                {
                    HttpListenerRequest req = ctx.Request;
                    HttpListenerResponse resp = ctx.Response;

                    Console.WriteLine("Request #: {0}", ++requestCount);
                    Console.WriteLine(req.Url.ToString());
                    Console.WriteLine(req.HttpMethod);
                    Console.WriteLine(req.UserHostName);
                    Console.WriteLine(req.UserAgent);
                    Console.WriteLine();

                    foreach(var bannedHost in bannedHosts)
                    {
                        if(req.Url.ToString().Contains(bannedHost, StringComparison.OrdinalIgnoreCase))
                        {
                            resp.StatusCode = 403;
                            byte[] buffer = Encoding.UTF8.GetBytes("Forbidden - you will be held accountable in the court of law if you keep going.");
                            resp.OutputStream.Write(buffer, 0, buffer.Length);
                            resp.Close();
                            return;
                        }
                    }

                    //TODO frameworkify
                    if (req.HttpMethod == "OPTIONS")
                    {
                        AddCORSHeaders(resp);
                        resp.StatusCode = (int)HttpStatusCode.NoContent; // 204 No Content
                        resp.Close();
                        return;
                    }

                    if (req.HttpMethod == "GET" && req.Url.AbsolutePath.StartsWith("/hash"))
                    {
                        var atrapalhanciaUrlSplit = string.Join("", req.Url.AbsolutePath.Split("/hash").Skip(1)).Split('/').Skip(1);
                        if (atrapalhanciaUrlSplit.Count() != 1)
                        {
                            RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                            return;
                        }

                        if(Hashes.AtrapalhanciaFilenames.TryGetValue(atrapalhanciaUrlSplit.ElementAt(0), out var fileName))
                        {
                            RespondJSON(ref resp, Encoding.UTF8.GetBytes(fileName));
                            return;
                        }

                        RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                        return;
                    }

                    if(req.HttpMethod == "POST" && req.Url.AbsolutePath.StartsWith("/tiktok-test"))
                    {
                        var requestBody = GetRequestBodyJSON(req);
                        foreach(var kvp in requestBody)
                        {
                            TimestampedConsole.Log($"{kvp.Key} - {kvp.Value}");
                        }

                        RespondJSON(ref resp, Ok(), HttpStatusCode.OK);
                        return;
                    }

                    if (req.HttpMethod == "GET" && req.Url.AbsolutePath.StartsWith("/atrapalhancia"))
                    {
                        var atrapalhanciaUrlSplit = req.Url.AbsolutePath.Split("/atrapalhancia");
                        var atrapalhanciaName = atrapalhanciaUrlSplit[1];

                        if (!atrapalhanciaName.Contains('/'))
                        {
                            RespondJSON(ref resp, AtrapalhanciaNotFound(), HttpStatusCode.NotFound);
                            return;
                        }

                        atrapalhanciaName = atrapalhanciaName.Split('/')[1];
                        var filePath = Path.Combine(Environment.CurrentDirectory, "Atrapalhancias", $"{atrapalhanciaName}.dll");

                        if (atrapalhanciaName == "" || !File.Exists(filePath))
                        {
                            RespondJSON(ref resp, AtrapalhanciaNotFound(), HttpStatusCode.NotFound);
                            return;
                        }

                        resp.Headers.Add("Content-Disposition", $"inline; filename=\"{atrapalhanciaName}.dll\"");
                        resp.ContentType = "application/x-msdownload";
                        resp.ContentEncoding = Encoding.UTF8;

                        using (Stream source = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            resp.ContentLength64 = source.Length;
                            byte[] buffer = new byte[2048];
                            int bytesRead;
                            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                try
                                {
                                    resp.OutputStream.WriteAsync(buffer, 0, bytesRead).GetAwaiter().GetResult();
                                }
                                catch (HttpListenerException)
                                {
                                    break;
                                }
                            }
                        }

                        resp.Close();
                        return;

                    }

                    //if (req.HttpMethod == "GET" && req.Url.AbsolutePath.StartsWith("/currentscreenshot.png") && External.CurrentScreenshotPath != "")
                    //{
                    //    resp.ContentType = "image/png";
                    //    resp.ContentEncoding = Encoding.UTF8;
                    //    using (Stream source = new FileStream(External.CurrentScreenshotPath + "/current.png", FileMode.Open, FileAccess.Read, FileShare.Read))
                    //    {
                    //        resp.ContentLength64 = source.Length;
                    //        byte[] buffer = new byte[2048];
                    //        int bytesRead;
                    //        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    //        {
                    //            try
                    //            {
                    //                resp.OutputStream.WriteAsync(buffer, 0, bytesRead).GetAwaiter().GetResult();
                    //            }
                    //            catch (HttpListenerException)
                    //            {
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    resp.Close();
                    //    return;
                    //}

                    if(req.HttpMethod == "GET" && req.Url.AbsolutePath.StartsWith("/request-socket"))
                    {
                        if(OnSocketCreationRequested == null)
                        {
                            RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                            return;
                        }

                        var connectUrlSplit = string.Join("", req.Url.AbsolutePath.Split("/request-socket").Skip(1)).Split('/').Skip(1);
                        if (connectUrlSplit.Count() != 1)
                        {
                            RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                            return;
                        }

                        var handler = OnSocketCreationRequested;
                        handler.Invoke(this, connectUrlSplit.ElementAt(0));
                        RespondJSON(ref resp, Ok());
                        return;
                    }

                    if (req.HttpMethod == "POST" && req.Url.AbsolutePath.StartsWith("/twitch/register"))
                    {
                        var requestBody = GetRequestBodyJSON(req);
                        var claims = TokenDecoder.DecodeToken(requestBody["token"]);

                        if (claims["user_id"] != null)
                        {
                            var twitchCode = requestBody["twitchToken"]?.ToString();

                            if (string.IsNullOrWhiteSpace(twitchCode))
                            {
                                RespondJSON(ref resp, GetJSON(new { error = "Missing Twitch code." }), HttpStatusCode.BadRequest);
                                return;
                            }

                            var clientId = "fzrpx1kxpqk3cyklu4uhw9q0mpux2y";
                            var redirectUri = "https://atrapalhancias.com.br/login/emulator/";

                            using var client = new HttpClient();
                            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                            {
                                { "client_id", clientId },
                                { "client_secret", twitch_client_secret },
                                { "code", twitchCode },
                                { "grant_type", "authorization_code" },
                                { "redirect_uri", redirectUri }
                            });

                            HttpResponseMessage twitchResp;
                            try
                            {
                                twitchResp = client.PostAsync("https://id.twitch.tv/oauth2/token", tokenRequest).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                RespondJSON(ref resp, GetJSON(new { error = "Failed to reach Twitch.", detail = ex.Message }), HttpStatusCode.InternalServerError);
                                return;
                            }

                            if (!twitchResp.IsSuccessStatusCode)
                            {
                                var errBody = twitchResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                                RespondJSON(ref resp, GetJSON(new { error = "Twitch token exchange failed.", twitch = errBody }), HttpStatusCode.Unauthorized);
                                return;
                            }

                            var tokenJson = twitchResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tokenJson);

                            try
                            {
                                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
                                request.Headers.Add("Client-Id", "fzrpx1kxpqk3cyklu4uhw9q0mpux2y");
                                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData["access_token"].ToString());
                                
                                twitchResp = client.SendAsync(request).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                RespondJSON(ref resp, GetJSON(new { error = "Failed to reach Twitch.", detail = ex.Message }), HttpStatusCode.InternalServerError);
                                return;
                            }

                            var userJSON = twitchResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            
                            using var doc = JsonDocument.Parse(userJSON);
                            var root = doc.RootElement;

                            string broadcaster_id = root.GetProperty("data")[0].GetProperty("id").GetString();
                            string login = root.GetProperty("data")[0].GetProperty("login").GetString();

                            var twitchRelation = TwitchRelation.Create(broadcaster_id, login, tokenData["access_token"].ToString(), tokenData["refresh_token"].ToString());
                            Broadcaster.Create(claims["user_id"], twitchRelation.Id, claims["email"].ToString());

                            RespondJSON(ref resp, Ok());
                            return;
                        }
                    }

                    if (req.HttpMethod == "POST" && req.Url.AbsolutePath.StartsWith("/connect"))
                    {
                        var connectUrlSplit = string.Join("", req.Url.AbsolutePath.Split("/connect").Skip(1)).Split('/').Skip(1);
                        if (connectUrlSplit.Count() != 3)
                        {
                            RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                            return;
                        }
                        var socketId = connectUrlSplit.ElementAt(0);
                        var game = connectUrlSplit.ElementAt(1);
                        var guid = connectUrlSplit.ElementAt(2);

                        var requestBody = GetRequestBodyJSON(req);
                        var claims = TokenDecoder.DecodeToken(requestBody["token"]);

                        var broadcaster = Broadcaster.GetInstance(claims["user_id"]);

                        if (broadcaster == null || broadcaster.TwitchRelation == null || broadcaster.TwitchRelation.AccessToken == null)
                        {
                            RespondJSON(ref resp, GetJSON(new { code = "twitch-login" }), HttpStatusCode.Unauthorized);
                            return;
                        }

                        var channel = broadcaster.TwitchRelation.ChannelName;
                        var broadcasterId = broadcaster.TwitchRelation.BroadcasterId;
                        var accessToken = broadcaster.TwitchRelation.AccessToken;

                        var localIp = ((IPEndPoint)req.RemoteEndPoint!).Address;
                        if (IPAddress.IsLoopback(localIp))
                        {
                            localIp = IPAddress.Loopback;
                        }

                        var ip = req.Headers["X-Forwarded-For"] ?? localIp.ToString();

                        if (SocketServer.ConnectionExists(ip))
                        {
                            if(OnGameConnected != null)
                            {
                                try
                                {
                                    var handler = OnGameConnected;
                                    handler.Invoke(this, new GameConnectedEventArgs(broadcasterId, channel, accessToken, game, SocketServer.GetConnection(ip).Behavior.Route));
                            
                                }
                                catch (Exception e)
                                {
                                    RespondJSON(ref resp, GetJSON(new { code = "twitch-login" }), HttpStatusCode.Unauthorized);
                                    return;
                                }
                            }

                            var BroadcasterSocket = ((GameBehavior)(SocketServer.GetConnection(ip).Behavior));
                            BroadcasterSocket.ChannelName = channel;
                            SocketServer.AddServiceAlias(BroadcasterSocket.Route, channel);

                            SocketServer.SendMessageAsync(ip, "authenticated").GetAwaiter().GetResult();
                            RespondJSON(ref resp, Encoding.UTF8.GetBytes("Connected!"));
                            return;
                        }
                        else
                        {
                            RespondJSON(ref resp, Encoding.UTF8.GetBytes("Session not found!"), HttpStatusCode.InternalServerError);
                            return;
                        }
                    }

                    RespondJSON(ref resp, NotFound(), HttpStatusCode.NotFound);
                    return;

                }).Start();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if(listener != null)
            {
                listener.Close();
            }

            CancellationToken.Cancel();
            listener = null;
        }

        public static void Run(ITokenDecoder tokenDecoder, WebSocketServer socketServer)
        {
            using (HttpServer server = new HttpServer(tokenDecoder, socketServer))
            {
                server.listener = new HttpListener();
                server.listener.Prefixes.Add(url);
                server.listener.Start();

                var fileUpdateBroadcaster = new AtrapalhanciaFileUpdateBroadcaster();
                fileUpdateBroadcaster.Watch();

                SQLite.Initialize();

                Console.WriteLine("Listening for http connections on {0}", url);

                Task listenTask = server.HandleIncomingConnections();

                Console.WriteLine("🧙 Portal aberto! 🧙");

                listenTask.GetAwaiter().GetResult();

                server.listener.Close();
            }
        }
    }
}
