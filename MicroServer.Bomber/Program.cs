using MicroServer.Bomber;

Console.WriteLine("<<< Start bomb it! >>>");

var client = new Bomber();
Bomber.Run();

Console.WriteLine("<<< Bombing finished! Press any key to exit... >>>");
Console.ReadKey();