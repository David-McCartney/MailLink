using System;
using System.Collections.Generic;
using System.Text;

namespace Daemon
{
    public class Program
    {

        public static void Main(string[] args)
        {
            Console.Write("MailLink Connector - Version 1734 - Copyright (C) David McCartney.\r\n\r\nInitializing...");

            // Create an instance of the Connector calss, which manages all mail sessions.
            MailLink.Connector connector = new MailLink.Connector();

            connector.LogMessage += onLogMessage;

            Console.Write("\rInitialization Complete. Type 'HELP' for a list of commands.\n\n");

            // Start the connector.
            connector.Start();

            while (true)
            {
                Console.Write("Daemon:\\> ");

                switch (Console.ReadLine().ToUpper())
                {
                    case "START":
                        connector.Start();
                        break;

                    case "PAUSE":
                        connector.Pause();
                        break;

                    case "STOP":
                        connector.Stop();
                        break;

                    case "EXIT":
                        connector.Stop();
                        Environment.Exit(0);
                        break;

                    case "HELP":
                        Console.WriteLine("\nAvailable Commands:\n");
                        Console.WriteLine("START  - Start the Connector.");
                        Console.WriteLine("PAUSE  - Existing connections will remain open, but no new connections will be created.");
                        Console.WriteLine("STOP   - Existing connections will be closed, and the Connector will be shut down.");
                        Console.WriteLine("EXIT   - Close all connections, and Exit the Daemon\n");
                        break;

                    default:
                        Console.WriteLine("*** ERROR *** Command not recognized. Type 'HELP' for a list of commands.\n");
                        break;
                }
            }
        }

        private static void onLogMessage(Object sender, EventArgs e)
        {
            MailLink.Connector connector = (MailLink.Connector)sender;
            if (connector.Message.Level <= connector.LogLevel)
            {
                Console.Write("\r{0}\nDaemon:\\>", connector.Message.Text);
            }
        }
    }
}
