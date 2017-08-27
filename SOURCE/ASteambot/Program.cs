using ASteambot.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambot
{
    class Program
    {
        //Private var
        private static LoginInfo logininfo;
        private static Manager steambotManager;
        private static AsynchronousSocketListener socketServer;

        private static Thread threadManager;
        private static Thread threadSocket;

        static void Main(string[] args)
        {
            Console.Title = "Akarr's steambot";

            PrintWelcomeMessage();

            StartSocketServer(4567);

            steambotManager = new Manager();
            threadManager = new Thread(new ThreadStart(steambotManager.Start));
            threadManager.Start();

            if (args.Length >= 3)
                AttemptLoginBot(args[0], args[1], args[2]);
            else
                AttemptLoginBot();

            while (steambotManager.OnlineBots.Count < 1)
                Thread.Sleep(TimeSpan.FromSeconds(3));

            string command = "";
            while(command != "quit")
            {
                Console.Write("> ");
                command = Console.ReadLine();
                steambotManager.Command(command);
            }

            int seconds = 5;
            while (seconds != 0)
            {
                Console.WriteLine("{0}...", seconds);
                Thread.Sleep(TimeSpan.FromSeconds(1));
                seconds--;
            }
        }

        private static void AttemptLoginBot(string username="", string password="", string api="")
        {
            if (username.Length == 0 && password.Length == 0)
            {
                Console.WriteLine("Bot's steam username :");
                username = Console.ReadLine();
                Console.WriteLine("Bot's steam password :");
                password = Console.ReadLine();
                Console.WriteLine("Bot's steam API :");
                api = Console.ReadLine();
            }

            logininfo = new LoginInfo(username, password, api);

            steambotManager.Auth(logininfo);
        }

        private static void PrintWelcomeMessage()
        {
            Console.WriteLine("Welcome message");
            Console.WriteLine("Lauching bot...");
        }

        private static void StartSocketServer(int port)
        {
            Console.WriteLine("Starting TCP server on port {0}", port);
            socketServer = new AsynchronousSocketListener(4567);
            threadSocket = new Thread(new ThreadStart(socketServer.StartListening));
            threadSocket.Start();
        }
    }
}
