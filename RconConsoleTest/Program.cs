using System;
using System.Net;
using System.Threading;

namespace RconConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Rcon rcon = new Rcon(IPAddress.Parse("10.0.0.3"), 27015, "apple");

                while (true)
                {
                    Console.WriteLine(rcon.SendCommand("status"));
                    Thread.Sleep(5000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
