using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        static void Main(string[] args)
        {
            // Unos broja ćelija od administratora
            Console.Write("Unesite broj ćelija na tabli: ");
            int boardSize = int.Parse(Console.ReadLine());

            // Provera da li je broj ćelija deljiv sa 4 i veći od 16
            if (boardSize % 4 != 0 || boardSize <= 16)
            {
                Console.WriteLine("Broj ćelija mora biti deljiv sa 4 i veći od 16.");
                return;
            }

            // Pokretanje servera
            GameServer server = new GameServer(boardSize);
            server.Start();
        }
    }
}


public class GameServer
{
    private const int MAX_PLAYERS = 4;
    private List<Player> players = new List<Player>();
    private List<Socket> clientSockets = new List<Socket>();
    private int boardSize;
    private bool gameRunning = false;
    private Socket serverSocket;
    private int nextPlayerId = 1;
    private int gameRound = 0;

    public GameServer(int boardSize)
    {
        this.boardSize = boardSize;
    }

    public void Start()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, 51000));
        serverSocket.Listen(MAX_PLAYERS);
        Console.WriteLine("Server pokrenut, čeka na igrače...");

        while (players.Count < MAX_PLAYERS)
        {
            Socket clientSocket = serverSocket.Accept();
            clientSockets.Add(clientSocket);
            int playerBase = (boardSize / MAX_PLAYERS) * players.Count; // Početna baza za svakog igrača
            int targetBase = (boardSize / MAX_PLAYERS) * players.Count; // Početna baza za svakog igrača

            Player newPlayer = new Player(clientSocket, nextPlayerId, playerBase,targetBase);
            nextPlayerId++;
            players.Add(newPlayer);
            Console.WriteLine($"Igrač {newPlayer.Id} povezan. Početna baza: {newPlayer.BasePosition}");
            SendMessageToPlayer(newPlayer, $"Dobrodošli, Igrač {newPlayer.Id}! Vaša baza je na poziciji {newPlayer.BasePosition}. Čeka se ostali igrači.");
        }

        while (true)
        {
            StartGameLoop(gameRound);  // Pokreće jednu igru

            AnnounceEndAndAskReplay();  // Pita sve da li žele novu

            List<Player> readyPlayers = WaitForReplayResponses();

            if (readyPlayers.Count == 0)
            {
                Console.WriteLine("Svi igrači su otišli. Server se gasi.");
                break;
            }
            else if (readyPlayers.Count < MAX_PLAYERS)
            {
                Console.WriteLine("Čekamo nove igrače...");

                while (readyPlayers.Count < MAX_PLAYERS)
                {
                    Socket clientSocket = serverSocket.Accept();
                    int playerBase = (boardSize / MAX_PLAYERS) * readyPlayers.Count;
                    int targetBase = (playerBase + boardSize / 2) % boardSize;

                    Player newPlayer = new Player(clientSocket, nextPlayerId, playerBase, targetBase);
                    nextPlayerId++;
                    readyPlayers.Add(newPlayer);

                    SendMessageToPlayer(newPlayer, "Dobrodošli u novu igru!");
                }
            }
            gameRound++;
            // Postavi novu listu igrača
            players = readyPlayers;
        }
    }

    private void HandlePlayerTurn(Player player)
    {
        bool hasExtraTurn = true;

        while (hasExtraTurn)
        {
            // Čekanje na rezultat bacanja kockice od igrača
            SendMessageToPlayer(player, "Vaš je red. Bacite kockicu!");
            string result = ReceiveMessage(player.ClientSocket);
            int diceRoll = int.Parse(result);
            Console.WriteLine($"Igrač {player.Id} je bacio {diceRoll}.");

            if (diceRoll == 6)
            {
                // Ako je bacio 6, dobija dodatni potez
                SendMessageToPlayer(player, "Bacili ste 6! Dobijate dodatni potez.");

                // Pitajte igrača da li želi izbaciti novu figuru ili pomeriti postojeću
                SendMessageToPlayer(player, "Da li želite da izbacite novu figuru (1) ili pomerite postojeću figuru (2)?");
                string choice = ReceiveMessage(player.ClientSocket);

                if (choice == "1")
                {
                    if (player.FiguresInPlay < 4)
                    {
                        player.StartFigure();
                    }
                    else
                    {
                        SendMessageToPlayer(player, "Sve figure su već na tabli. Pomeramo postojeću figuru.");
                        AskAndMoveFigure(player, diceRoll);
                    }
                }
                else if (choice == "2")
                {
                    AskAndMoveFigure(player, diceRoll);
                }
                else
                {
                    SendMessageToPlayer(player, "Neispravan izbor. Figura se pomera.");
                    AskAndMoveFigure(player, diceRoll);
                }

                // Igrač nastavlja jer je dobio dodatni potez
                hasExtraTurn = true;
            }
            else
            {
                // Ako nije bacio 6, obavlja se standardni potez
                AskAndMoveFigure(player, diceRoll);
                hasExtraTurn = false;
            }

            // Provera stanja na tabli
            SendMessageToPlayer(player, $"Vaše figure su sada na: {string.Join(", ", player.GetFigurePositions())}");
            CheckForCollisions(player);
        }

        // Obavestiti sve igrače o statusu igre
        foreach (var otherPlayer in players)
        {
            if (otherPlayer != player)
            {
                SendMessageToPlayer(otherPlayer, $"Igrač {player.Id} je završio potez.");
            }
        }
    }
    public void MoveSpecificFigure(Player player, int figureIndex, int steps, int boardSize)
    {
        if (figureIndex >= 0 && figureIndex < player.FigurePositions.Count)
        {
            int newPosition = player.FigurePositions[figureIndex] + steps;
            if (newPosition <= boardSize) // Provera granice cilja
            {
                if (!player.FigurePositions.Contains(newPosition)) // Provera preklapanja
                {
                    player.FigurePositions[figureIndex] = newPosition;
                    Console.WriteLine($"Figura {figureIndex + 1} igrača {player.Id} pomerena na poziciju {newPosition}.");
                }
                else
                {
                    Console.WriteLine("Pozicija je već zauzeta. Pomeranje nije moguće.");
                }
            }
            else
            {
                Console.WriteLine($"Figura {figureIndex + 1} igrača {player.Id} ne može biti pomerena izvan cilja.");
            }
        }
        else
        {
            Console.WriteLine($"Neispravan izbor figure. Igrač {player.Id} ima {player.FigurePositions.Count} figura na tabli.");
        }
    }

    private void AskAndMoveFigure(Player player, int steps)
    {
        // Prikažite igraču pozicije njegovih figura
        SendMessageToPlayer(player, $"Vaše figure su na pozicijama: {string.Join(", ", player.GetFigurePositions())}");
        SendMessageToPlayer(player, "Unesite broj figure koju želite da pomerite (1 - prva figura, 2 - druga figura, ...):");

        // Primite izbor od igrača
        string choice = ReceiveMessage(player.ClientSocket);
        int figureIndex;
        if (int.TryParse(choice, out figureIndex))
        {
            figureIndex -= 1; // Indeksiranje od nule
            player.MoveFigure(figureIndex, steps, boardSize);
        }
        else
        {
            SendMessageToPlayer(player, "Neispravan unos. Figura se ne pomera.");
        }
    }



    private void CheckForCollisions(Player player)
    {
        foreach (var otherPlayer in players)
        {
            if (otherPlayer != player)
            {
                foreach (var playerPos in player.GetFigurePositions())
                {
                    if (otherPlayer.GetFigurePositions().Contains(playerPos))
                    {
                        Console.WriteLine($"Figura igrača {player.Id} sudarila se sa figurom igrača {otherPlayer.Id} na poziciji {playerPos}.");

                        // Vraćanje figure drugog igrača u bazu
                        otherPlayer.ReturnFigureToBase(playerPos);

                        // Obaveštavanje igrača o sudaru
                        SendMessageToPlayer(player, $"Vaša figura je eliminisala figuru igrača {otherPlayer.Id} na poziciji {playerPos}.");
                        SendMessageToPlayer(otherPlayer, $"Vaša figura je eliminisana od strane igrača {player.Id} i vraćena u bazu.");

                        // Ažuriranje svih igrača
                        BroadcastGameState();
                    }
                }
            }
        }
    }

    private void BroadcastGameState()
    {
        foreach (var player in players)
        {
            string positions = string.Join(", ", player.GetFigurePositions());
            SendMessageToPlayer(player, $"Ažurirane pozicije vaših figura: {positions}");
        }
    }

    private bool CheckGameOver()
    {
        foreach (var player in players)
        {
            if (player.HasWon())
            {
                return true;
            }
        }
        return false;
    }

    private void AnnounceWinner()
    {
        foreach (var player in players)
        {
            if (player.HasWon())
            {
                SendMessageToPlayer(player, "Čestitamo! Pobedili ste!");
            }
            else
            {
                SendMessageToPlayer(player, "Nažalost, niste pobedili.");
            }
        }
    }

    private void AnnounceEndAndAskReplay()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Kraj igre! Rang lista:");
        int rank = 1;
        foreach (var player in players.OrderByDescending(p => p.FiguresFinished))
        {
            sb.AppendLine($"{rank}. Igrač {player.Id} - završenih figura: {player.FiguresFinished}");
            rank++;
        }

        foreach (var p in players)
        {
            try
            {
                SendMessageToPlayer(p, sb.ToString());
                SendMessageToPlayer(p, "Da li želite da igrate ponovo? (da/ne)");
            }
            catch
            {
                Console.WriteLine($"Ne mogu da pošaljem igraču {p.Id} - diskonektovan.");
            }
        }
    }

    private List<Player> WaitForReplayResponses()
    {
        List<Player> readyPlayers = new List<Player>();
        // players = readyPlayers; // Zamenjujemo staru listu sa samo spremnim igračima


        foreach (var p in players)
        {
            try
            {
                string response = ReceiveMessage(p.ClientSocket);
                if (response != null && response.Trim().ToLower() == "da")
                {
                    readyPlayers.Add(p);
                }
                else
                {
                    Console.WriteLine($"Igrač {p.Id} nije spreman za novu igru.");
                }
            }
            catch
            {
                Console.WriteLine($"Igrač {p.Id} diskonektovan.");
            }
        }

        return readyPlayers;
    }

    private void StartGameLoop(int gameRound)
    {
        gameRunning = true;

        Console.WriteLine("Nova igra počinje!");

        //foreach (var player in players)
        //{
        //    player.ResetForNewGame();
        //    SendMessageToPlayer(player, "Nova igra počinje!");
        //}
        for (int i = 0; i < players.Count; i++)
        {
            int rotatedBase = (boardSize / MAX_PLAYERS) * ((i + gameRound) % MAX_PLAYERS);
            players[i].ResetForNewGame(rotatedBase, rotatedBase); // baza = cilj
            SendMessageToPlayer(players[i], $"Nova igra počinje! Vaša nova baza: {rotatedBase}");
        }

        while (gameRunning)
        {
            foreach (var player in players)
            {
                HandlePlayerTurn(player);
                if (CheckGameOver())
                {
                    gameRunning = false;
                    break;
                }
            }
        }

        AnnounceWinner();
    }





    private string ReceiveMessage(Socket socket)
    {
        byte[] buffer = new byte[256];
        int bytesRead = socket.Receive(buffer);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    private void SendMessageToPlayer(Player player, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        player.ClientSocket.Send(data);
    }
}


public class Player
{
    public int Id { get; private set; }
    public int FiguresInPlay { get; private set; }
    public List<int> FigurePositions { get; private set; }
    public Socket ClientSocket { get; private set; }
    public int BasePosition { get; private set; }
    public int FiguresFinished { get; private set; }
    public int TargetPosition { get; private set; }

    public Player(Socket clientSocket, int id, int basePosition, int targetPosition)
    {
        Id = id;
        ClientSocket = clientSocket;
        BasePosition = basePosition;
        FigurePositions = new List<int>();
        FiguresInPlay = 0;
        TargetPosition = targetPosition;
    }

    public void StartFigure()
    {
        if (FiguresInPlay < 4)
        {
            FigurePositions.Add(BasePosition);
            FiguresInPlay++;
            Console.WriteLine($"Igrač {Id} je pokrenuo novu figuru iz baze ({BasePosition}).");
        }
    }
    public bool MoveFigure(int figureIndex, int steps, int boardSize)
    {
        if (figureIndex < 0 || figureIndex >= FigurePositions.Count)
        {
            Console.WriteLine("Neispravan izbor figure.");
            return false;
        }

        int currentPosition = FigurePositions[figureIndex];

        // Koliko polja ostaje do cilja?
        int distanceToTarget = (TargetPosition - currentPosition + boardSize) % boardSize;

        if (distanceToTarget == 0)
            distanceToTarget = boardSize;

        if (distanceToTarget == steps)
        {
            // Tačno za ulazak u cilj
            FigurePositions.RemoveAt(figureIndex);
            FiguresInPlay--;
            FiguresFinished++;
            string message = $"🎉 Figura igrača {Id} je stigla u ciljnu bazu na poziciji {TargetPosition}!\n" +
                 $"🏁 Ukupno završenih figura: {FiguresFinished}";

            Console.WriteLine(message);

            // Pošalji klijentu
            try
            {
                NetworkStream stream = new NetworkStream(ClientSocket);
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Greška pri slanju poruke klijentu {Id}: {ex.Message}");
            }
            return true;
        }
        else if (steps < distanceToTarget)
        {
            // Normalno pomeranje
            int newPosition = (currentPosition + steps) % boardSize;
            FigurePositions[figureIndex] = newPosition;
            Console.WriteLine($"Figura igrača {Id} pomerena na poziciju {newPosition}.");
            return true;
        }
        else
        {
            // Prevelik broj – ne može da uđe u cilj
            //Console.WriteLine($"Figura igrača {Id} ne može da se pomeri jer je potrebno {distanceToTarget} polja za ulazak u cilj.");
            string poruka = $"❌ Figura igrača {Id} ne može da se pomeri jer joj nedostaje {distanceToTarget} polja do cilja.";

            Console.WriteLine(poruka); // Ispis na serveru

            // Pošalji klijentu
            byte[] data = Encoding.UTF8.GetBytes(poruka);
            try
            {
                NetworkStream stream = new NetworkStream(ClientSocket);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Greška pri slanju poruke klijentu {Id}: {ex.Message}");
            }

            return false;
        }
    }

    //public void MoveFigure(int steps, int boardSize)
    //{
    //    for (int i = 0; i < FigurePositions.Count; i++)
    //    {
    //        int newPosition = (FigurePositions[i] + steps) % boardSize;
    //        FigurePositions[i] = newPosition;
    //    }
    //}

    //public void ReturnFigureToBase(int position)
    //{
    //    FigurePositions.Remove(position);
    //    FiguresInPlay--;
    //}

    public bool HasWon()
    {
        return FigurePositions.Count == 4 && FigurePositions.TrueForAll(p => p == BasePosition);
    }

    public List<int> GetFigurePositions()
    {
        return FigurePositions;
    }

    public void ReturnFigureToBase(int position)
    {
        if (FigurePositions.Remove(position))
        {
            FiguresInPlay--;
            Console.WriteLine($"Figura na poziciji {position} vraćena u bazu igrača {Id}.");
        }
    }
    public void ResetForNewGame(int newBase, int newTarget)
    {
        BasePosition = newBase;
        TargetPosition = newTarget;
        FigurePositions.Clear();
        FiguresInPlay = 0;
        FiguresFinished = 0;
    }
}

