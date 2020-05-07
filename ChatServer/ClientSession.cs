using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatServer
{
    class ClientSession
    {
        Socket socket;    // otwarte gniazdo polaczenia
        NetworkStream ns; // strumien sieciowy "na gniezdzie"
        StreamReader sr;  // strumien do odbierania danych "na s.sieciowym"
        StreamWriter sw;  // strumien do wysylania danych "na s.sieciowym"

        Thread thread;

        string username;
        public string Username { get => username; }

        public delegate void OnDisconnectHandler(ClientSession src);
        public delegate void PrintUsersHandler(ClientSession src);
        public delegate bool CheckUsernameHandler(ClientSession src, string username);
        public delegate void BroadcastMessageHandler(ClientSession src, string message);
        public delegate bool WhisperMessageHandler(ClientSession src, string message, string target);

        public OnDisconnectHandler onDisconnect;
        public CheckUsernameHandler CheckUsername;
        public BroadcastMessageHandler BroadcastMessage;
        public WhisperMessageHandler WhisperMessage;
        public PrintUsersHandler PrintUsers;

        enum ReturnValue
        {
            Quit = -1,
            Normal = 0
        };

        public ClientSession(Socket socket)
        {
            this.socket = socket;
            ns = new NetworkStream(this.socket);
            sr = new StreamReader(ns);
            sw = new StreamWriter(ns);
            username = null;
            sw.AutoFlush = true;
        }

        public void Start()
        {
            thread = new Thread(ProcessCommunication);
            thread.Start();
        }

        public void ProcessCommunication()
        {
            string message;
            ReturnValue code = ReturnValue.Normal;

            try
            {
                sw.WriteLine("Dołączanie do servera chatu.");
                int attempts = 3;


                //Ustawienie nazwy uzytkownika            
                do
                {
                    sw.Write("Wprowadz nazwe użytkownika: ");
                    var us = sr.ReadLine();

                    if (CheckUsername(this, us))
                    {
                        username = us;
                        Console.WriteLine("{0} joined the server", us);
                        BroadcastMessage(null, String.Format("{0} dołączył do servera!, witaj!", username));
                        sw.WriteLine("Wpisz /help, dla listy dostępnych poleceń!");
                    }
                    else if (attempts <= 0)
                    {
                        sw.WriteLine("Nie udało się dołączyć do servera!");
                        Disconnect();
                    }
                    else
                    {
                        sw.WriteLine("Nazwa zajęta! Spróbuj ponownie");
                        attempts--;
                    }
                }
                while (username == null);
            } catch (System.IO.IOException e)
            {
                Disconnect();
                return;
            }

            //Komunikacja
            do
            {
                
                // czekaj na komunikat
                if (socket.Connected)
                {
                    try
                    {
                        message = sr.ReadLine();
                        code = ProcessMessage(message);
                    } catch (System.IO.IOException e)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            while (code != ReturnValue.Quit);

            Disconnect();
        }


        //wysłanie sformatowanej wiadomości
        public void Send(string sender, string message)
        {
            Send(String.Format($"[{sender}]: {message}"));
        }

        //wysłanie wiadomości
        public void Send(string message)
        {
            try
            {
                sw.WriteLine(message);
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        //Wyodrębnienie komendy i argumentu
        private string SplitMessage(string message, out string argument)
        {
            if (message == null)
            {
                argument = null;
                return "/quit";
            }

            if (message[0]!='/')
            {
                argument = message;
                return "/say";
            }

            var buffer = message.Split(' ');
            if (buffer.Length<1)
            {
                argument = "";
                return "";
            }

            var cmd = buffer[0];
            argument = String.Join(" ", buffer.Skip(1));

            return cmd;
        }

        //Przetworzenie wysłanej wiadomości
        private ReturnValue ProcessMessage(string message)
        {
            string argument;
            string command = SplitMessage(message, out argument);

            switch(command)
            {
                //Pomoc
                case "/help":
                    Send("Dostępne komendy:");
                    Send("/help - wyświetla dostepne komendy");
                    Send("/whois - wyświetla podłączonych użytkowników");
                    Send("/say <tresc> - wysyla wiadomosc do wszystkich");
                    Send("/pm <adresat> <tresc> - wysyla prywatną wiadomość do [adresat]");
                    Send("/roll [min] [max] - losuje liczbę pomiędzy min-max");
                    Send("/quit - opuszcza serwer");
                    break;

                //Wyjście
                case "/quit":
                    return ReturnValue.Quit;

                //Normalnie mówienie
                case "/say":
                    BroadcastMessage(this, argument);
                    break;

                //prywatna wiadomość
                case "/pm":
                case "/pw":
                case "/whisper":
                    var buffer = argument.Split(' ');
                    if (buffer.Length < 2) {
                        Send("Zła ilość argumentów");
                        break;
                    }
                    var target = buffer[0];
                    var msg = String.Join(" ", buffer.Skip(1));

                    if (WhisperMessage(this, msg, target))
                        Send("Wysłano prywatną wiadomość do " + target);
                    else
                        Send(target + " nie istnieje!");
                    break;

                //wyświetlenie aktualnych użytkowników
                case "/whois":
                    Send("Aktualnie podłączeni użytkownicy:");
                    PrintUsers(this);
                    break;

                //Losowanie liczby (dodatkowo)
                case "/roll":
                case "/rand":
                    var buffer2 = argument.Split(' ');
                    Random rnd = new Random();
                    var argnumb = buffer2.Length;

                    try
                    {
                        int rmin = 1;
                        int rmax = 100;

                        if (argnumb==1 && buffer2[0] != "")
                        {
                            rmax = int.Parse(buffer2[0]);
                        } else if (argnumb==2)
                        {
                            rmin = int.Parse(buffer2[0]);
                            rmax = int.Parse(buffer2[1]);
                        } else if (argnumb>2)
                        {
                            throw new FormatException();
                        }

                        BroadcastMessage(null, String.Format($"{Username} wylosował {rnd.Next(rmin, rmax)} ({rmin}-{rmax})"));
                    }
                    catch ( FormatException e )
                    {
                        sw.WriteLine("Nieprawidłowy format argumentów");
                    }
                    break;

                //W przypadku nieznanej komendy
                default:
                    sw.WriteLine("Niepoprawna komenda");
                    break;
            }
            return ReturnValue.Normal;
        }
        public void Disconnect()
        {
            if (sw != null) sw.Close();
            sw = null;
            if (sr != null) sr.Close();
            sr = null;
            if (ns != null) ns.Close();
            ns = null;
            if (socket != null) socket.Close();
            socket = null;

            onDisconnect(this);
            BroadcastMessage(null, String.Format($"Użytkownik {Username} opuścił rozmowę"));
        }
    }
}
