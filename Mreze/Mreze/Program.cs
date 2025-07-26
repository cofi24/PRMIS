using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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

            Console.WriteLine("Povezivanje sa serverom...");
            TcpClient client = new TcpClient(serverIp, 51000);
            NetworkStream stream = client.GetStream();

            Console.WriteLine("Povezani sa serverom. Igra počinje!");

            // Glavna petlja za igru
            while (true)
            {
                // Čekaj na poruku servera
                byte[] buffer = new byte[256];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine("Server: " + message);

                if (message.Contains("Vaš je red"))
                {
                    // Igrač baca kockicu
                    Console.Write("Bacite kockicu (unesite broj): ");
                    string diceRoll = Console.ReadLine();
                    byte[] data = Encoding.UTF8.GetBytes(diceRoll);
                    stream.Write(data, 0, data.Length);
                }
                else if (message.Contains("Bacili ste 6"))
                {
                    // Pitanje za izbor akcije
                    Console.WriteLine("Izaberite: 1 - Izbaciti novu figuru, 2 - Pomeriti postojeću figuru.");
                    string choice = Console.ReadLine();
                    byte[] data = Encoding.UTF8.GetBytes(choice);
                    stream.Write(data, 0, data.Length);
                }
                else if (message.Contains("Unesite broj figure"))
                {
                    // Igrač bira figuru
                    Console.Write("Izaberite figuru za pomeranje: ");
                    string figureChoice = Console.ReadLine();
                    byte[] data = Encoding.UTF8.GetBytes(figureChoice);
                    stream.Write(data, 0, data.Length);
                }
                else if (message.Contains("eliminisana"))
                {
                    // Obaveštenje o sudaru
                    Console.WriteLine("Server: " + message);
                }
                else if (message.Contains("Ažurirane pozicije"))
                {
                    // Prikaz ažuriranih pozicija figura
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



        static void SendMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            clientSocket.Send(data);
        }

    }
}
