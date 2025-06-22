using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace AtrapalhanciaWebSocket
{
    public class WebSocketServer
    {
        private WebSocketServer()
        {

        }

        private ConcurrentDictionary<string, ServiceConnection<BaseServiceBehavior>> ServiceConnections = new ConcurrentDictionary<string, ServiceConnection<BaseServiceBehavior>>();
        private ConcurrentDictionary<string, BaseServiceBehavior> Services = new ConcurrentDictionary<string, BaseServiceBehavior>(); //TODO eventually DateTime will be replaced by something useful?

        public void AddService<T>(T behavior) where T : BaseServiceBehavior
        {
            Services[behavior.Route] = behavior;
        }

        public bool ServiceExists(string route)
        {
            return ServiceConnections.ContainsKey(route);
        }

        public bool ConnectionExists(string ip)
        {
            return ServiceConnections.ContainsKey(ip);
        }

        private void AddConnection(string ip, TcpClient client, BaseServiceBehavior behavior)
        {
            ServiceConnections[ip] = ServiceConnection.Create(client, behavior);
            ServiceConnections[ip].Behavior.OnOpenEvent.Invoke(null, EventArgs.Empty);
        }

        public ServiceConnection<BaseServiceBehavior> GetConnection(string ip)
        {
            if (ServiceConnections.TryGetValue(ip, out var connection))
            {
                return connection;
            }

            throw new KeyNotFoundException("No connections under this ip");
        }

        private void RemoveConnection(string ip)
        {
            if (ServiceConnections.Remove(ip, out var connection))
            {
                connection.Behavior.OnCloseEvent.Invoke(null, EventArgs.Empty);
            }
        }

        private void Close(string ip, TcpClient client)
        {
            RemoveConnection(ip);
            client.Close();
        }

        private byte[] CreateWebSocketTextFrame(byte[] payload)
        {
            List<byte> frame = new();
            frame.Add(0x81); // FIN + opcode 0x1 (text)

            if (payload.Length <= 125)
            {
                frame.Add((byte)payload.Length);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                frame.Add(126);
                frame.AddRange(BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)payload.Length)));
            }
            else
            {
                frame.Add(127);
                frame.AddRange(BitConverter.GetBytes((ulong)IPAddress.HostToNetworkOrder((long)payload.Length)));
            }

            frame.AddRange(payload);
            return frame.ToArray();
        }


        public async Task SendMessageAsync(string ip, string message)
        {
            if (ServiceConnections.TryGetValue(ip, out var connection))
            {
                try
                {
                    var client = connection.TcpClient;
                    await connection.WriteLock.WaitAsync();
                    var stream = client.GetStream();
                    byte[] payload = Encoding.UTF8.GetBytes(message);
                    byte[] frame = CreateWebSocketTextFrame(payload);
                    await stream.WriteAsync(frame, 0, frame.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send message to {ip}: {ex.Message}");
                }
                finally
                {
                    connection.WriteLock.Release();
                }
            }
            else
            {
                throw new KeyNotFoundException("No connections under this ip");
            }
        }

        public async Task BroadcastServiceAsync(string serviceRoute, string message)
        {
            var connections = ServiceConnections.Where((kvp) => kvp.Value.Behavior.Route == serviceRoute).ToArray();
            foreach (var connection in connections)
            {
                await SendMessageAsync(connection.Key, message);
            }
        }

        private async Task<BaseServiceBehavior> HandleHandshake(TcpClient client, NetworkStream stream, CancellationToken handshakeTimer)
        {
            var buffer = new byte[1024];

            // Read initial handshake request
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (!Regex.IsMatch(request, "^GET", RegexOptions.IgnoreCase))
            {
                client.Close();

                throw new InvalidDataException("Invalid handshake request");
            }

            string route = "/";
            try
            {
                var lines = request.Split("\r\n");
                var firstLine = lines[0];
                var parts = firstLine.Split(" ");

                if (parts.Length >= 2)
                    route = parts[1]; // The path
            }
            catch
            {
                throw new InvalidDataException("Malformed handshake request");
            }

            if (!Services.ContainsKey(route))
            {
                throw new InvalidDataException("Invalid service route (maybe you need to run AddService first)");
            }

            Console.WriteLine("===== Handshaking from client =====\n" + request);

            // Extract Sec-WebSocket-Key
            string swk = Regex.Match(request, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
            string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] swkHash = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
            string acceptKey = Convert.ToBase64String(swkHash);

            // Send handshake response
            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: " + acceptKey + "\r\n\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

            Console.WriteLine("Handshake response sent.");

            return Services[route];
        }

        private async Task HandleClientFrames(TcpClient client, NetworkStream stream, BaseServiceBehavior behavior)
        {
            var buffer = new byte[1024];
            DateTime lastPong = DateTime.UtcNow;

            var pingTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));

            // Task to send pings periodically and check pong response
            _ = Task.Run(async () =>
            {
                while (await pingTimer.WaitForNextTickAsync())
                {
                    try
                    {
                        // Send a ping frame (opcode 0x9)
                        byte[] ping = new byte[] { 0x89, 0x00 };
                        await stream.WriteAsync(ping, 0, ping.Length);
                        Console.WriteLine("Ping sent");

                        if ((DateTime.UtcNow - lastPong).TotalSeconds > 60)
                        {
                            Console.WriteLine("Client unresponsive. Closing.");
                            client.Close();
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            });

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client disconnected.");
                    break;
                }

                var bytes = buffer.Take(bytesRead).ToArray();

                int opcode = bytes[0] & 0b00001111;
                bool mask = (bytes[1] & 0b10000000) != 0;
                ulong offset = 2;
                ulong msgLen = (ulong)(bytes[1] & 0b01111111);

                // Handle Pong (opcode 0xA)
                if (opcode == 0xA)
                {
                    Console.WriteLine("Pong received");
                    lastPong = DateTime.UtcNow;
                    continue;
                }

                // Handle Ping (opcode 0x9) — reply with Pong automatically
                if (opcode == 0x9)
                {
                    Console.WriteLine("Ping received — sending Pong");
                    byte[] pong = new byte[] { 0x8A, 0x00 }; // FIN=1, opcode=0xA pong, no payload
                    await stream.WriteAsync(pong, 0, pong.Length);
                    continue;
                }

                // Handle Close (opcode 0x8) — reply with Close frame and break
                if (opcode == 0x8)
                {
                    Console.WriteLine("Client sent close frame. Sending close frame in response.");

                    // Proper close frame: FIN=1, opcode=0x8, no payload
                    byte[] closeFrame = new byte[] { 0x88, 0x00 };
                    await stream.WriteAsync(closeFrame, 0, closeFrame.Length);

                    break;
                }

                // Read extended payload length if present
                if (msgLen == 126)
                {
                    msgLen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                    offset = 4;
                }
                else if (msgLen == 127)
                {
                    msgLen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                    offset = 10;
                }

                if (mask)
                {
                    byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                    offset += 4;

                    byte[] decoded = new byte[msgLen];
                    for (ulong i = 0; i < msgLen; ++i)
                        decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                    string text = Encoding.UTF8.GetString(decoded);
                    behavior.OnMessageEvent.Invoke(null, text);

                    Console.WriteLine("Message: " + text);

                    // TODO: Process your message here
                }
                else
                {
                    Console.WriteLine("Mask bit not set — invalid frame from client");
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            Console.WriteLine("A client connected.");

            using var stream = client.GetStream();
            var ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            try
            {
                // Step 1: Perform the handshake
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var handshakeTask = HandleHandshake(client, stream, cts.Token);
                var completed = await Task.WhenAny(handshakeTask, Task.Delay(Timeout.Infinite, cts.Token));

                if (completed != handshakeTask)
                {
                    throw new TimeoutException("Handshake timed out");
                }

                var behavior = await handshakeTask;

                AddConnection(ip, client, behavior);

                behavior.close = () => Close(ip, client);
                behavior.send = (message) => SendMessageAsync(ip, message);

                // Step 2: If handshake successful, start reading frames (your existing logic)
                await HandleClientFrames(client, stream, behavior);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                Close(ip, client);
            }

        }

        private async Task StartListening()
        {
            string ip = "0.0.0.0";
            int port = 5000;
            var server = new TcpListener(IPAddress.Parse(ip), port);

            server.Start();
            Console.WriteLine("Server has started on {0}:{1}, Waiting for a connection…", ip, port);

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine("A client connected.");

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        public static WebSocketServer Listen()
        {
            var server = new WebSocketServer();

            server.StartListening();

            return server;
        }
    }
}
