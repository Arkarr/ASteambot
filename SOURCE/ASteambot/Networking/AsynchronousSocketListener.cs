using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;


namespace ASteambot.Networking
{
    public class AsynchronousSocketListener
    {
        public int Port { get; private set; }
        public bool Running { get; private set; }
        public event EventHandler<EventArgGameServer> MessageReceived;

        private string password;

        protected virtual void OnMessageReceived(EventArgGameServer e)
        {
            if (MessageReceived != null)
                MessageReceived(this, e);
        }
        
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private void HandleMessage(Socket handler, string content)
        {
            Console.WriteLine(content);

            if (!content.StartsWith(password))
                return;

            //Must be always true :
            if (content.EndsWith("<EOF>"))
            {
                content = content.Replace(password, "");
                content = content.Substring(0, content.Length - 5);

                string[] codeargsdata = content.Split(new char[] { '&' }, 2);

                string[] idmsgtype = codeargsdata[0].Split(new char[] { '|' }, 2);

                codeargsdata[1] = codeargsdata[1].Replace("\0", string.Empty);

                Console.WriteLine("Data :\n" + codeargsdata[1]);

                GameServerRequest gsr = new GameServerRequest(handler, idmsgtype[0], idmsgtype[1], codeargsdata[1]);

                EventArgGameServer arg = new EventArgGameServer(gsr);
                OnMessageReceived(arg);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Received message, but it's incomplete/not correctly formated !");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public void Stop()
        {
            Running = false;
        }

        public AsynchronousSocketListener(int port, string password)
        {
            Port = port;
            this.password = password;
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
                Console.WriteLine("IP : " + new WebClient().DownloadString("http://icanhazip.com").Replace("\n", "") + " PORT : " + Port);
                Console.ForegroundColor = ConsoleColor.White;


                Console.WriteLine("  Example (in ASteambot_Core.cfg) : ");
                Console.WriteLine("    sm_asteambot_server_ip \"XXX.XXX.XX.XX\" ");
                Console.WriteLine("    sm_asteambot_server_port \""+Port+"\" ");

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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Error while creating socket ! It may be because of the port being already usued! Use another TCP port number.");
                Console.WriteLine(e);
                Console.ForegroundColor = ConsoleColor.White;
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
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
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

                    if (state.sb.ToString().EndsWith("<EOF>"))
                    {
                        HandleMessage(handler, state.sb.ToString());
                        state.sb.Clear();
                    }

                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
            catch (SocketException ex) { Console.WriteLine(ex.Message); }
            /*catch (Exception e)
            {
                Console.WriteLine("Error while processing a message sent by the game server!");
                Console.WriteLine(e.Message);
                Console.WriteLine("Command : " + content);
                Console.WriteLine(e.StackTrace);

                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }*/
        }
    }
}
