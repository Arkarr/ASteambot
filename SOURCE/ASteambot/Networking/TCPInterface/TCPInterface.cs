using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;


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

        private void HandleMessage(Socket handler, string content)
        {
            if(Program.DEBUG)
                Console.WriteLine("Received message from server :\n"+content+"\n\n");

            if (!content.StartsWith(password))
                return;
            
            content = content.Replace(password, "");

            string[] codeargsdata = content.Split(new char[] { '&' }, 2);

            string[] idmsgtype = codeargsdata[0].Split(new char[] { '|' }, 2);

            codeargsdata[1] = codeargsdata[1].Replace("\0", string.Empty);

            GameServerRequest gsr = new GameServerRequest(handler, idmsgtype[0], idmsgtype[1], codeargsdata[1]);

            EventArgGameServer arg = new EventArgGameServer(gsr);
            OnMessageReceived(arg);
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
                
                var host = Dns.GetHostEntry(Dns.GetHostName());

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
                    
                    if (state.sb.ToString().Contains("<EOF>"))
                    {
                        bool lastMessageFinished = false;
                        if (state.sb.ToString().EndsWith("<EOF>"))
                            lastMessageFinished = true;

                        string[] msg = state.sb.ToString().Split(new string[] { "<EOF>" }, StringSplitOptions.None);

                        for(int i = 0; i < msg.Length; i++)
                        {
                            if (i < msg.Length - 1)
                            {
                                string finalMsg = data + msg[i];

                                HandleMessage(handler, finalMsg);
                            }

                            if (i == msg.Length - 1 && lastMessageFinished && msg[i].Length > 0)
                            {
                                string finalMsg = data + msg[i];

                                HandleMessage(handler, finalMsg);
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
