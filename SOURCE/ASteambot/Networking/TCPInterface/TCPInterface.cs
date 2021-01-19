using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ASteambot.Networking
{
    public class TCPInterface
    {
        public int Port { get; private set; }
        public bool Running { get; private set; }
        public event EventHandler<EventArgGameServer> MessageReceived;

        private string password;
        private string data;

        protected virtual void OnMessageReceived(EventArgGameServer e)
        {
            if (MessageReceived != null)
                MessageReceived(this, e);
        }
        
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private void HandleMessage(Socket handler, string content, bool isWebSocket, bool isWebSocketHS = false)
        {
            if (isWebSocketHS)
            {
                Console.WriteLine("=====Web socket connection detected - handshaking from client=====\n{0}", content);

                // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                // 3. Compute SHA-1 and Base64 hash of the new value
                // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                string swk = Regex.Match(content, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                byte[] response = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                handler.Send(response);
                //handler.BeginSend(response, 0, response.Length, 0, new AsyncCallback(SendCallback), handler);
                //handler.Write(response, 0, response.Length);
            }
            else
            {
                if (Program.DEBUG)
                    Console.WriteLine("Received message from server :\n" + content + "\n\n");

                if (!content.StartsWith(password))
                    return;

                content = content.Replace(password, "");

                string[] codeargsdata = content.Split(new char[] { '&' }, 2);

                string[] idmsgtype = codeargsdata[0].Split(new char[] { '|' }, 2);

                codeargsdata[1] = codeargsdata[1].Replace("\0", string.Empty);

                GameServerRequest gsr = new GameServerRequest(handler, idmsgtype[0], idmsgtype[1], codeargsdata[1], isWebSocket);

                EventArgGameServer arg = new EventArgGameServer(gsr);
                OnMessageReceived(arg);
            }
        }

        public void Stop()
        {
            Running = false;
        }

        public TCPInterface(int port, string password)
        {
            Port = port;
            this.password = password;
            this.data = "";
        }

        public void StartListening()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), Port);

            try
            {
                Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(localEndPoint);
                listener.Listen(100);
                
                var host = Dns.GetHostEntry("localhost");

                for (int cwidth = Console.WindowWidth; cwidth-2 > 0; cwidth--)
                    Console.Write("*");
                Console.WriteLine("*");

                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Console.WriteLine("Use this ip/port if ASteambot **IS** running on the same machine as the server :");
                        Console.WriteLine("IP : " + ip + " PORT : " + Port);
                    }
                }

                Console.WriteLine("Use this ip/port if ASteambot **IS NOT** running on the same machine as the server :");
                Console.WriteLine("IP : " + new WebClient().DownloadString("https://api.ipify.org").Replace("\n", "") + " PORT : " + Port);

                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine("Example (in ASteambot_Core.cfg) : ");
                Console.WriteLine("   sm_asteambot_server_ip \"XXX.XXX.XX.XX\" ");
                Console.WriteLine("   sm_asteambot_server_port \"" + Port+"\" ");

                for (int cwidth = Console.WindowWidth; cwidth - 2 > 0; cwidth--)
                    Console.Write("*");
                Console.WriteLine("*");

                Running = true;
                while (Running)
                {
                    allDone.Reset();

                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    
                    while (!allDone.WaitOne(3000) && Running)
                    {
                        if (!Running)
                            break;
                    }
                }

            }
            catch (SocketException e)
            {
                Program.PrintErrorMessage("Error while creating socket ! It may be because of the port being already usued! Use another TCP port number.");
                Program.PrintErrorMessage(e.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();
            
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            { 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead).Replace("\0", String.Empty));

                    if (Regex.IsMatch(state.sb.ToString(), "^GET", RegexOptions.IgnoreCase))
                    {
                        HandleMessage(handler, state.sb.ToString(), true, true);

                        state.sb.Clear();
                    }
                    else
                    {
                        //bool fin = (state.buffer[0] & 0b10000000) != 0;
                        bool mask = (state.buffer[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
                        if (mask)
                        {
                            int opcode = state.buffer[0] & 0b00001111, // expecting 1 - text message
                                msglen = state.buffer[1] - 128, // & 0111 1111
                                offset = 2;

                            if (msglen == 126)
                            {
                                // was ToUInt16(bytes, offset) but the result is incorrect
                                msglen = BitConverter.ToUInt16(new byte[] { state.buffer[3], state.buffer[2] }, 0);
                                offset = 4;
                            }
                            else if (msglen == 127)
                            {
                                Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                                // i don't really know the byte order, please edit this
                                // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                                // offset = 10;
                            }

                            if (mask)
                            {
                                byte[] decoded = new byte[msglen];
                                byte[] masks = new byte[4] { state.buffer[offset], state.buffer[offset + 1], state.buffer[offset + 2], state.buffer[offset + 3] };
                                offset += 4;

                                for (int i = 0; i < msglen; ++i)
                                    decoded[i] = (byte)(state.buffer[offset + i] ^ masks[i % 4]);

                                Console.WriteLine(">>> " + Encoding.UTF8.GetString(decoded));
                                HandleMessage(handler, Encoding.UTF8.GetString(decoded), true);
                            }

                            state.sb.Clear();
                        }
                        else
                        { 
                            if (state.sb.ToString().Contains("<EOF>"))
                            {
                                bool lastMessageFinished = false;
                                if (state.sb.ToString().EndsWith("<EOF>"))
                                    lastMessageFinished = true;

                                string[] msg = state.sb.ToString().Split(new string[] { "<EOF>" }, StringSplitOptions.None);

                                for (int i = 0; i < msg.Length; i++)
                                {
                                    if (i < msg.Length - 1)
                                    {
                                        string finalMsg = data + msg[i];

                                        HandleMessage(handler, finalMsg, false);
                                    }

                                    if (i == msg.Length - 1 && lastMessageFinished && msg[i].Length > 0)
                                    {
                                        string finalMsg = data + msg[i];

                                        HandleMessage(handler, finalMsg, false);
                                    }
                                    else
                                    {
                                        data = msg[i];
                                    }

                                    if (i == 0)
                                        data = "";
                                }

                                state.sb.Clear();
                            }
                        }
                    }

                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }

            }
            catch (SocketException ex)
            {
                Program.PrintErrorMessage(ex.Message);
            }
        }
    }
}
