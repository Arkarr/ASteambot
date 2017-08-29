using ASteambot.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot
{
    public class GameServer
    {
        public string Name { get; set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }
        public int ServerID { get; private set; }

        private Socket socket;

        public GameServer(Socket socket, int serverid, string ipportname)
        {
            string[] srvinfos = ipportname.Split('|');
            Name = srvinfos[2];
            IP = IPAddress.Parse(srvinfos[0]);
            Port = Int32.Parse(srvinfos[1]);
            this.socket = socket;
            ServerID = serverid;

            Send("SRVID|" + ServerID);
        }
        
        public bool SocketConnected()
        {
            bool check1 = socket.Poll(1000, SelectMode.SelectRead);
            bool check2 = (socket.Available == 0);
            if (check1 && check2)
                return false;
            else
                return true;
        }

        public void Send(string data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            
            socket.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), socket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
