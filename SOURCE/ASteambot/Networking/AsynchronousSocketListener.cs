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
        public event EventHandler<EventArgGameServer> MessageReceived;

        private string password;

        protected virtual void OnMessageReceived(EventArgGameServer e)
        {
            MessageReceived?.Invoke(this, e);
        }
        
        private ManualResetEvent allDone = new ManualResetEvent(false);
        private bool running = false;

        public AsynchronousSocketListener(int port, string password)
        {
            Port = port;
            this.password = password;
        }
        
        public void StartListening()
        {
            byte[] bytes = new byte[1024];
            
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), Port);
            
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                running = true;
                while (running)
                {
                    allDone.Reset();
 
                    //Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    
                    allDone.WaitOne();
                }

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
            String content = String.Empty;
            
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    content = state.sb.ToString();
                    HandleMessage(handler, content);
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        Send(handler, content);
                    }
                    else
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }
            catch(Exception e)
            {

            }
        }

        private void HandleMessage(Socket handler, string content)
        {
            if (!content.StartsWith(password))
                return;

            content = content.Replace(password, "");
            string[] codeargs = content.Split('|');

            codeargs[1] = codeargs[1].Replace("\0", string.Empty);

            EventArgGameServer arg = new EventArgGameServer(handler, codeargs[0], codeargs[1]);
            OnMessageReceived(arg);
        }

        private void Send(Socket handler, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                
                int bytesSent = handler.EndSend(ar);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Stop()
        {
            running = false;
            allDone.Set();
        }
    }
}
