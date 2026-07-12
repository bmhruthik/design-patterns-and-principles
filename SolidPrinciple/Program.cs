using SRP = SolidCookbook.Principles.SRP;
using OCP = SolidCookbook.Principles.OCP;
using LSP = SolidCookbook.Principles.LSP;
using ISP = SolidCookbook.Principles.ISP;
using DIP = SolidCookbook.Principles.DIP;

while (true)
{
    Console.Clear();

    Console.WriteLine("===== SOLID Cookbook =====");
    Console.WriteLine("1. SRP");
    Console.WriteLine("2. OCP");
    Console.WriteLine("3. LSP");
    Console.WriteLine("4. ISP");
    Console.WriteLine("5. DIP");
    Console.WriteLine("0. Exit");

    Console.Write("\nChoice: ");

    var input = Console.ReadLine();

    switch (input)
    {
        case "1":
            SRP.Demo.Run();
            break;

        case "2":
            OCP.Demo.Run();
            break;

        case "3":
            LSP.Demo.Run();
            break;

        case "4":
            ISP.Demo.Run();
            break;

        case "5":
            DIP.Demo.Run();
            break;

        case "0":
            return;
    }

    Console.WriteLine("\nPress any key...");
    Console.ReadKey();
}