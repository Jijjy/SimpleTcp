﻿using System;
using System.Collections.Generic;
using System.Text;
using SimpleTcp;

namespace ClientTest
{
    class Program
    {
        static string _ServerIp;
        static int _ServerPort;
        static readonly bool _Ssl;
        static readonly string _PfxFilename = null;
        static readonly string _PfxPassword = null;

        static SimpleTcpClient _Client;
        static bool _RunForever = true;

        static void Main(string[] args)
        {
            _ServerIp =    InputString("Server IP   :", "127.0.0.1", false);
            _ServerPort = InputInteger("Server Port :", 9000, true, false);

            /*
            _Ssl =        InputBoolean("Use SSL     :", false);

            if (_Ssl)
            {
                _PfxFilename = InputString("PFX Certificate File:", "simpletcp.pfx", false);
                _PfxPassword = InputString("PFX File Password:", "simpletcp", false);
            }

            _Client = new SimpleTcpClient(_ServerIp, _ServerPort, _Ssl, _PfxFilename, _PfxPassword);
            */

            // _Client = new SimpleTcpClient((_ServerIp + ":" + _ServerPort));

            _Client = new SimpleTcpClient(_ServerIp, _ServerPort);
            _Client.Events.Connected += Connected;
            _Client.Events.Disconnected += Disconnected;
            _Client.Events.DataReceived += DataReceived;
            _Client.Keepalive.EnableTcpKeepAlives = true; 
            _Client.Settings.MutuallyAuthenticate = false;
            _Client.Settings.AcceptInvalidCertificates = true;
            _Client.Settings.ConnectTimeoutMs = 5000;
            _Client.Logger = Logger;

            // _Client.Connect();
            _Client.ConnectWithRetries(5000);

            while (_RunForever)
            {
                string userInput = InputString("Command [? for help]:", null, false);
                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                    case "Q":
                        _RunForever = false;
                        break;
                    case "c":
                    case "C":
                    case "cls":
                        Console.Clear();
                        break;
                    case "send":
                        Send();
                        break;
                    case "sendasync":
                        SendAsync();
                        break;
                    case "connected":
                        IsConnected();
                        break;
                    case "dispose":
                        _Client.Dispose();
                        break;
                    case "stats":
                        Console.WriteLine(_Client.Statistics.ToString());
                        break;
                    case "connect":
                        _Client.Connect();
                        break;
                    case "disconnect":
                        _Client.Disconnect();
                        break;
                    case "stats reset":
                        _Client.Statistics.Reset();
                        break;
                }
            }
        }

        static void IsConnected()
        {
            Console.WriteLine("Connected: " + _Client.IsConnected);
        }

        static void Connected(object sender, EventArgs e)
        {
            Console.WriteLine("*** Server connected");
        }

        static void Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("*** Server disconnected"); 
        }

        static void DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("[" + e.IpPort + "] " + Encoding.UTF8.GetString(e.Data));
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ?             Help, this menu");
            Console.WriteLine(" q             Quit");
            Console.WriteLine(" cls           Clear the screen");
            Console.WriteLine(" send          Send a message to the server");
            Console.WriteLine(" sendasync     Send a message to the server asynchronously");
            Console.WriteLine(" connected     Display if the client is connected to the server");
            Console.WriteLine(" dispose       Dispose of the client");
            Console.WriteLine(" connect       Connect to the server (connected: " + _Client.IsConnected + ")");
            Console.WriteLine(" disconnect    Disconnect from the server");
            Console.WriteLine(" stats         Display client statistics");
            Console.WriteLine(" stats reset   Reset client statistics");
            Console.WriteLine("");
        }

        static void Send()
        {
            string data = InputString("Data:", "Hello!", true);
            if (!string.IsNullOrEmpty(data)) _Client.Send(data);
        }

        static void SendAsync()
        {
            string data = InputString("Data:", "Hello!", true);
            if (!string.IsNullOrEmpty(data)) _Client.SendAsync(data).Wait();
        }

        static void Logger(string msg)
        {
            Console.WriteLine(msg);
        }

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput))
            {
                return yesDefault;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                return string.Compare(userInput, "n") != 0
                    && string.Compare(userInput, "no") != 0;
            }
            else
            {
                return (string.Compare(userInput, "y") == 0)
                    || (string.Compare(userInput, "yes") == 0);
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!string.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrEmpty(userInput))
                {
                    if (!string.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static List<string> InputStringList(string question, bool allowEmpty)
        {
            List<string> ret = new List<string>();

            while (true)
            {
                Console.Write(question);

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrEmpty(userInput))
                {
                    if (ret.Count < 1 && !allowEmpty) continue;
                    return ret;
                }

                ret.Add(userInput);
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                if (!int.TryParse(userInput, out int ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }
    }
}
