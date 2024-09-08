using System;
using LogicalServerUdp;

var manager = new NetManager();
manager.Start(8000);

Console.WriteLine("Server started... Press enter to stop");

Console.ReadLine();

manager.Stop();
