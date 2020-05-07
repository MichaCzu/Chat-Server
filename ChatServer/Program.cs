using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        static Server server = null;
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            string address = "127.0.0.1";
            int port = 11000;

            server = new Server(IPAddress.Parse(address), port);
            server.Start();

            Console.WriteLine("The server has been stopped. Hit [Enter]...");
            Console.ReadLine();
        }
        
        //Wyłaczenie servera: ctrl+c;
        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Stopping the server...");
            server.Stop();
            
            Console.ReadLine();
            e.Cancel = true; // nie chcemy wymusic zamkniecia programu!
        }
    }
}
