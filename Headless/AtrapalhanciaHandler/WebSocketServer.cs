using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AtrapalhanciaHandler
{
    //? I dont enjoy overhead specifically on this part because i want maximum performance from my side. However, couldnt this websocket be an external service? Maybe really using js hidden on the backend and comunicating locally to the client (this app) that only interacts with it? (the emulator side will also connect to this external socket but with some sort of low permissions to only interact with receiving the atrapalhancias and etc)
    public class WebSocketServer
    {
        private static async Task HandleHandshake(TcpClient client, NetworkStream stream)
        {
            var buffer = new byte[1024];

            // Read initial handshake request
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (!Regex.IsMatch(request, "^GET", RegexOptions.IgnoreCase))
            {
                Console.WriteLine("Invalid handshake request");
                client.Close();
                return;
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
        }

        private static async Task HandleClientFrames(TcpClient client, NetworkStream stream)
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


        private static async Task HandleClientAsync(TcpClient client)
        {
            Console.WriteLine("A client connected.");
            
            using var stream = client.GetStream();

            try
            {
                // Step 1: Perform the handshake
                await HandleHandshake(client, stream);

                // Step 2: If handshake successful, start reading frames (your existing logic)
                await HandleClientFrames(client, stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }

        }

        public static async Task Listen()
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
    }
}
