
using ASteambot.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambot
{
    public class GameServer
    {
        public string Name { get; set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }
        public int ServerID { get; private set; }
        public long SteamID { get; private set; }
        public bool Alive { get; private set; }
        public bool isWebSocket { get; private set; }

        private Socket socket;
        private string tcppasswd;
        private DataQueue dataQueue;

        private string endDelimiter = "<EOF>";

        private bool socketError = false;

        public GameServer(Socket socket, string tcppaswd, int serverid, string ipportname, bool isWebSocket)
        {
            Alive = true;
            string[] srvinfos = ipportname.Split(new[] { '|' }, 4);
            Name = srvinfos[3];
            tcppasswd = tcppaswd;
            SteamID = long.Parse(srvinfos[0]);
            IP = IPAddress.Parse(srvinfos[1]);
            Port = Int32.Parse(srvinfos[2]);
            this.socket = socket;
            ServerID = serverid;
            dataQueue = new DataQueue();
            this.isWebSocket = isWebSocket;
            dataQueue.OnAdd += DataQueue_OnAdd;
            
            FirstSend(ServerID);
        }

        private void DataQueue_OnAdd(object sender, EventArgData e)
        {
            byte[] bytes = e.GetData;
            string str = Encoding.Default.GetString(bytes);

            try
            {
                if (!socket.Connected)
                    socketError = true;
                else
                    socket.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendCallback), socket);
            }
            catch(Exception ex)
            {
                socketError = true;
            }

            dataQueue.Remove(bytes);
            dataQueue.Clear();
        }

        public void Update(GameServer gs)
        {
            this.socket = gs.socket;
            this.IP = gs.IP;
            this.Port = gs.Port;
            this.ServerID = gs.ServerID;
        }

        public bool SocketConnected()
        {
            string finaldata = tcppasswd + -1 + ")" + ((int)NetworkCode.ASteambotCode.Simple).ToString() + "|" + "PING" + endDelimiter;
           
            //socket.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendCallback), socket);
            int size = Encoding.ASCII.GetByteCount(finaldata);

            if (size > NetworkCode.MAX_CHUNK_SIZE)
            {
                List<string> chunks = ChunksUpto(finaldata, NetworkCode.MAX_CHUNK_SIZE).ToList();
                foreach (string chunk in chunks)
                {
                    if (socketError)
                        break;

                    byte[] data = Encoding.UTF8.GetBytes(chunk);
                    socketError = !QuickSend(data);
                }
            }
            else
            {
                byte[] data = Encoding.UTF8.GetBytes(finaldata);
                socketError = !QuickSend(data);
            }

            return !socketError;
        }

        private bool QuickSend(byte[] data)
        {
            try
            {
                socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), socket);
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        public void FirstSend(int serverID)
        {
            string finaldata = tcppasswd + "-1)SRVID| " + serverID + endDelimiter;

            //Console.WriteLine(finaldata);
            /*byte[] byteData = Encoding.UTF8.GetBytes(finaldata);
            socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);*/

            int size = Encoding.ASCII.GetByteCount(finaldata);

            if (size > NetworkCode.MAX_CHUNK_SIZE)
            {
                List<string> chunks = ChunksUpto(finaldata, NetworkCode.MAX_CHUNK_SIZE).ToList();
                foreach (string chunk in chunks)
                    dataQueue.Add(Encoding.UTF8.GetBytes(chunk));
            }
            else
            {
                if (isWebSocket)
                    SendMessageToClient(finaldata);
                else
                    dataQueue.Add(Encoding.UTF8.GetBytes(finaldata));
            }
        }

        private IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }

        public bool Send(int moduleID, NetworkCode.ASteambotCode netcode, string data)
        {
            string finaldata = tcppasswd + moduleID + ")" + ((int)netcode).ToString() + "|" + data;
            int size = Encoding.ASCII.GetByteCount(finaldata);

            if (isWebSocket && !socketError)
            {
                SendMessageToClient(finaldata);
            }
            else
            {
                if (size > NetworkCode.MAX_CHUNK_SIZE && !socketError)
                {
                    List<string> chunks = ChunksUpto(finaldata, NetworkCode.MAX_CHUNK_SIZE).ToList();
                    string lastChunk = chunks.Last();

                    foreach (string chunk in chunks)
                    {
                        if (socketError)
                            break;

                        if (chunk.Equals(lastChunk))
                        {
                            if (chunk.Length + endDelimiter.Length > NetworkCode.MAX_CHUNK_SIZE)
                            {
                                dataQueue.Add(Encoding.UTF8.GetBytes(chunk));
                                dataQueue.Add(Encoding.UTF8.GetBytes(endDelimiter));
                            }
                            else
                            {
                                dataQueue.Add(Encoding.UTF8.GetBytes(chunk + endDelimiter));
                            }
                        }
                        else
                        {
                            dataQueue.Add(Encoding.UTF8.GetBytes(chunk));
                        }

                        Thread.Sleep(100);
                    }
                }
                else
                {
                    if (finaldata.Length + endDelimiter.Length > NetworkCode.MAX_CHUNK_SIZE)
                    {
                        dataQueue.Add(Encoding.UTF8.GetBytes(finaldata));
                        Thread.Sleep(100);
                        dataQueue.Add(Encoding.UTF8.GetBytes(endDelimiter));
                    }
                    else
                    {
                        dataQueue.Add(Encoding.UTF8.GetBytes(finaldata + endDelimiter));
                    }
                }

                dataQueue.Clear();
            }

            //Console.WriteLine("Done, status -> Sucess ? " + !socketError);

            return !socketError;
        }

        private void SendCallback(IAsyncResult ar)
        {
            //try
            //{
            Socket handler = (Socket)ar.AsyncState;

            int bytesSent = handler.EndSend(ar);
            /*}
            catch(SocketException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Socket crash: ");
                Console.WriteLine(e);
                Console.ForegroundColor = ConsoleColor.White;

                socketError = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }*/
        }

        public void SendMessageToClient(string msg)
        {
            using (var stream = new NetworkStream(socket))
            {
                Queue<string> que = new Queue<string>(SplitInGroups(msg, 125));
                int len = que.Count;

                while (que.Count > 0)
                {
                    var header = GetHeader(
                        que.Count > 1 ? false : true,
                        que.Count == len ? false : true
                    );

                    byte[] list = Encoding.UTF8.GetBytes(que.Dequeue());
                    header = (header << 7) + list.Length;
                    stream.Write(IntToByteArray((ushort)header), 0, 2);
                    stream.Write(list, 0, list.Length);
                }
            }
        }

        protected byte[] IntToByteArray(ushort value)
        {
            var ary = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(ary);
            }

            return ary;
        }

        private IEnumerable<string> SplitInGroups(string original, int size)
        {
            var p = 0;
            var l = original.Length;
            while (l - p > size)
            {
                yield return original.Substring(p, size);
                p += size;
            }
            yield return original.Substring(p);
        }

        protected int GetHeader(bool finalFrame, bool contFrame)
        {
            int header = finalFrame ? 1 : 0;//fin: 0 = more frames, 1 = final frame
            header = (header << 1) + 0;//rsv1
            header = (header << 1) + 0;//rsv2
            header = (header << 1) + 0;//rsv3
            header = (header << 4) + (contFrame ? 0 : 1);//opcode : 0 = continuation frame, 1 = text
            header = (header << 1) + 0;//mask: server -> client = no mask

            return header;
        }

        private void PrintSocketError(string msg)
        {
            Program.PrintErrorMessage("Could not send data : ");
            Program.PrintErrorMessage(msg);
            Program.PrintErrorMessage("to " + Name + " (" + ServerID + ") because the socket is not connected (" + IP + ":" + Port + ") !");
        }

        class ArrayView<T> : IEnumerable<T>
        {
            private readonly T[] array;
            private readonly int offset, count;

            public ArrayView(T[] array, int offset, int count)
            {
                this.array = array;
                this.offset = offset;
                this.count = count;
            }

            public int Length
            {
                get { return count; }
            }

            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= this.count)
                        throw new IndexOutOfRangeException();
                    else
                        return this.array[offset + index];
                }
                set
                {
                    if (index < 0 || index >= this.count)
                        throw new IndexOutOfRangeException();
                    else
                        this.array[offset + index] = value;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (int i = offset; i < offset + count; i++)
                    yield return array[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                IEnumerator<T> enumerator = this.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
            }
        }

        private T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}
