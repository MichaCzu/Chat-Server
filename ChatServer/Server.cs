using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatServer
{
    class Server
    {
        IPEndPoint ipEndPoint;
        Socket listeningSocket;
        List<ClientSession> connectedClients;

        public Server(IPAddress ipAddress, int ipPort)
        {
            connectedClients = new List<ClientSession>();
            ipEndPoint = new IPEndPoint(ipAddress, ipPort);
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            Console.WriteLine("Server created.");
        }

        public void Start()
        {
            listeningSocket.Bind(ipEndPoint);
            listeningSocket.Listen(1);

            Console.WriteLine("Server @ {0} started.", ipEndPoint);
            do
            {
                Console.WriteLine("Server @ {0} waits for a client...", ipEndPoint);
                Socket clientSocket = listeningSocket.Accept();

                Console.WriteLine("Server @ {0} client connected @ {1}.", ipEndPoint, clientSocket.RemoteEndPoint);
                Console.WriteLine("Server @ {0} starting client thread.", ipEndPoint, clientSocket.RemoteEndPoint);

                //Stworzenie sesji chatu i podpięcie odpowiednich delegatów
                ClientSession ch = new ClientSession(clientSocket);
                ch.onDisconnect = OnDisconnect;
                ch.CheckUsername = CheckUsername;
                ch.BroadcastMessage = BroadcastMessage;
                ch.WhisperMessage = WhisperMessage;
                ch.PrintUsers = PrintUsers;
                connectedClients.Add(ch);
                ch.Start();
            }
            while (true);

            Stop();
        }

        public void Stop()
        {
            BroadcastMessage(null, "Zamykanie serwera..");

            //Odpięcie każdego klienta (foreach się niesprawdziło przez usuwanie na bieżąco)
            for(int i=connectedClients.Count-1; i>=0; i--)
            {
                connectedClients[i].Send("Żegnaj..");
                connectedClients[i].Disconnect();
            }
        }

        private void BroadcastMessage(ClientSession src, string message)
        {
            foreach (var client in connectedClients)
            {
                //Pomin jeżeli jeszcze nie ma przypisanego nicku
                if (client.Username == null)
                    continue;

                //Jezeli uzytkownik "anonimowy" - wyslij jako wiadomosc servera
                if (src == null)
                {
                    client.Send("Server", message);
                }
                //Niewysylanie samemu sobie
                else if (client != src)
                {
                    client.Send(src.Username, message);
                }
            }
        }

        private void PrintUsers(ClientSession src)
        {
            int i = 1; ;
            foreach (var user in connectedClients)
            {
                //Pomin jeżeli jeszcze nie ma przypisanego nicku
                if (user.Username == null)
                    continue;

                src.Send($"{i}.{user.Username} {(user.Username == user.Username ? "(You)" : "")}");
                i++;
            }
        }

        public bool WhisperMessage(ClientSession src, string message, string target)
        {
            foreach (var user in connectedClients)
            {
                //Jezeli znajdzie cel prywatnej wiadomosci = wyslij
                if (user.Username == target)
                {
                    user.Send(String.Format("{0} whispers \"{1}\"",src.Username, message));
                    return true;
                }
            }
            return false;
        }
        private bool CheckUsername(ClientSession src, string username)
        {
            if (username == "")
                return false;

            foreach(ClientSession ch in connectedClients)
            {
                if (username == ch.Username)
                    return false;
            }
            return true;
        }

        private void OnDisconnect(ClientSession src)
        {
            if (src != null)
            {
                Console.WriteLine(String.Format($"{src.Username} disconnected."));
                connectedClients.Remove(src);
            }
        }
}
}