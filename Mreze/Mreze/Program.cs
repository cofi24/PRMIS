using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mreze
{
    public class Program
    {
        static Socket clientSocket;

        static void Main(string[] args)
        {
            Console.WriteLine("Unesite IP adresu servera:");
            string serverIp = Console.ReadLine();

            try
            {
                // Kreiraj socket (IPv4, TCP)
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Konektuj se na server
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), 51000));
                Console.WriteLine("Povezani sa serverom. Igra počinje!");

                // Pokreni nit za primanje podataka sa servera
                Thread recvThread = new Thread(StartPolling);
                recvThread.IsBackground = true;
                recvThread.Start();

                // Glavna nit šalje ili samo drži program aktivnim
                while (true)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška: " + ex.Message);
            }

        }

        static void StartPolling()
        {
            new Thread(() =>
            {
                byte[] buffer = new byte[1024];

                while (true)
                {
                    try
                    {
                        // Proverava koliko bajtova je spremno za čitanje
                        if (clientSocket.Available > 0)
                        {
                            int bytesRead = clientSocket.Receive(buffer);
                            if (bytesRead > 0)
                            {
                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                                if (string.IsNullOrEmpty(message)) continue;

                                Console.WriteLine("Server: " + message);

                                // Obrada poruka kao i ranije
                                if (message.Contains("Vaš je red"))
                                {
                                    int diceRoll;
                                    while (true)
                                    {
                                        Console.Write("Bacite kockicu (1-6): ");
                                        string input = Console.ReadLine();

                                        // pokuša parsiranje i provera granica
                                        if (int.TryParse(input, out diceRoll) && diceRoll >= 1 && diceRoll <= 6)
                                        {
                                            break; // validan unos → izlaz iz petlje
                                        }

                                        Console.WriteLine("❌ Nevažeći unos. Morate uneti broj od 1 do 6.");
                                    }

                                    SendMessage(diceRoll.ToString());
                                }
                                else if (message.Contains("Bacili ste 6"))
                                {
                                    Console.WriteLine("Izaberite: 1 - Izbaciti novu figuru, 2 - Pomeriti postojeću figuru.");
                                    string choice = Console.ReadLine();
                                    SendMessage(choice);
                                }
                                else if (message.Contains("Unesite broj figure"))
                                {
                                    Console.Write("Izaberite figuru za pomeranje: ");
                                    string figureChoice = Console.ReadLine();
                                    SendMessage(figureChoice);
                                }
                                else if (message.Contains("eliminisana"))
                                {
                                    Console.WriteLine("Obaveštenje: " + message);
                                }
                                else if (message.Contains("Ažurirane pozicije"))
                                {
                                    Console.WriteLine("Server: " + message);
                                }
                                else if (message.Contains("(da/ne)"))
                                {
                                    Console.Write("Unesite odgovor (da/ne): ");
                                    string input = Console.ReadLine();
                                    SendMessage(input);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(10); // Ne zamaraj CPU
                        }
                    }
                    catch (SocketException se)
                    {

                        Console.WriteLine("Greška u prijemu: " + se.Message);
                        break;
                    }
                }
            }).Start();
        }


        static void SendMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            clientSocket.Send(data);
        }

    }
}
