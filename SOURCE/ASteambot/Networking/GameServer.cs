
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
        private string tcppasswd;

        public GameServer(Socket socket, string tcppaswd, int serverid, string ipportname)
        {
            string[] srvinfos = ipportname.Split('|');
            Name = srvinfos[2];
            tcppasswd = tcppaswd;
            IP = IPAddress.Parse(srvinfos[0]);
            Port = Int32.Parse(srvinfos[1]);
            this.socket = socket;
            ServerID = serverid;

            FirstSend(ServerID);
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
        public void FirstSend(int serverID)
        {
            string finaldata = tcppasswd + "-1)SRVID| " + serverID + "<EOF>";

            //Console.WriteLine(finaldata);
            byte[] byteData = Encoding.ASCII.GetBytes(finaldata);
            socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);
        }

        public bool Send(int moduleID, NetworkCode.ASteambotCode netcode, string data)
        {
            string finaldata = tcppasswd + moduleID + ")" + ((int)netcode).ToString() + "|" + data + "<EOF>";

            if (SocketConnected())
            {
                try
                {
                    byte[] byteData = Encoding.ASCII.GetBytes(finaldata);
                    socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ForegroundColor = ConsoleColor.White;
                    PrintSocketError(data);
                    return false;
                }

                return true;
            }
            else
            {
                PrintSocketError(data);
                return false;
            }
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

        private void PrintSocketError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not send data : ");
            Console.WriteLine(msg);
            Console.WriteLine("to " + Name + " ("+ServerID+") because the socket is not connected ("+IP + ":" + Port +") !");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
