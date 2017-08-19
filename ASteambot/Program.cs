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

            steambotManager.SelectFirstBot();

            string command = "";
            while(command != "quit")
            {
                Console.Write("> ");
                command = Console.ReadLine();
                steambotManager.Command(command);
            }

            socketServer.Stop();
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
            for(int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.ForegroundColor = ConsoleColor.Green;
            string centerText = "ASteambot - Arkarr's steambot";
            Console.SetCursorPosition((Console.WindowWidth - centerText.Length) / 2, Console.CursorTop);
            Console.WriteLine(centerText);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
            Console.WriteLine("\tAll informations related to this software can be found here :");
            Console.WriteLine("\thttps://forums.alliedmods.net/showthread.php?t=273091");
            Console.WriteLine("");
            Console.WriteLine("\tVersion 1.1.0 - PUBLIC");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.Write("\tI would like you not to remove this text ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("<3 ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("!");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("\tArkarr's Steam profile : http://steamcommunity.com/id/arkarr");

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");
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
