using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using static System.Net.Mime.MediaTypeNames;


namespace Week1Server
{
    internal class Program
    {
        static Dictionary<int, byte[]> gameState = new Dictionary<int, byte[]>(); //initialise this at the start of the program
        static Socket newsock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); //make a socket using UDP. The parameters passed are enums used by the constructor of Socket to configure the socket.
        static IPEndPoint[] sender = new IPEndPoint[30];
        static EndPoint[] Remote = new EndPoint[30];
        static List<IPEndPoint> connectedClients = new List<IPEndPoint>();

        static string playerInfo = "Ping";
        static string ip = "25.53.60.215";
        static int lastAssignedGlobalID = 12;
        static void Main(string[] args)
        {
            
            initializeServer();


            Thread thr1 = new Thread(SendData);
            Thread thr2 = new Thread(KeyCheck);
            Thread thr3 = new Thread(ReceiveData);

            thr1.Start();
            thr2.Start();
            thr3.Start();
        }

        
        //open socket for the ip
        static void initializeServer()
        {

            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), 9050); //our server IP. This is set to local (127.0.0.1) on socket 9050. If 9050 is firewalled, you might want to try another!


            newsock.Bind(ipep); //bind the socket to our given IP
            Console.WriteLine("Socket open..."); //if we made it this far without any networking errors, it’s a good start!
        }

        //Send data to Client
        static private void SendData()
        {
            byte[] data = new byte[1024];


            while (true)
            {
                for (int i = 1; i < Remote.Length; i++)
                {
                    if (Remote[i] != null)
                    {


                        // send dictionary contents back to clients
                        foreach (IPEndPoint ep in connectedClients)
                        {
                           
                            if (ep.Port != 0)
                            {
                                //update data to clients
                                foreach (KeyValuePair<int, byte[]> kvp in gameState)
                                {
                                    newsock.SendTo(kvp.Value, kvp.Value.Length, SocketFlags.None, ep);
                                }
                            }
                        }                        
                    }

                }
                //Thread.Sleep(5);
            }
            
        }

        //Receive data from Client
        static private void ReceiveData()
        {


            byte[] data = new byte[1024]; // packet size. 
            int recv;

            int pos = 0;

            while (true)
            {
                //receive the current endpoint and the data from it
                sender[pos] = new IPEndPoint(IPAddress.Any, 0);
                if (Remote[pos] == null)
                {
                    Remote[pos] = (EndPoint)(sender[pos]);
                }               

                EndPoint newRemote = Remote[pos];
                data = new byte[2048];
                recv = newsock.ReceiveFrom(data, ref newRemote); //recv is now a byte array containing what just arrived from the client
               
                string text = Encoding.ASCII.GetString(data, 0, recv); //pass data to a string               


                //if string relates to id
                if (text.Contains("I need a UID for local object:"))
                {
                    Console.WriteLine(text.Substring(text.IndexOf(':')));
                    //parse the string into an int to get the local ID
                    int localObjectNumber = Int32.Parse(text.Substring(text.IndexOf(':') + 1));
                    //assign the ID
                    string returnVal = ("Assigned UID:" + localObjectNumber + ";" + lastAssignedGlobalID++);
                    newsock.SendTo(Encoding.ASCII.GetBytes(returnVal), Encoding.ASCII.GetBytes(returnVal).Length, 
                        SocketFlags.None, newRemote);

                }
                //if string relates to data from client
                else if (text.Contains("Object data;"))
                {
                    //Console.WriteLine(text);
                    string globalId = text.Split(";")[1];
                    int intId = Int32.Parse(globalId);
                    

                    CheckCheating(text);
                    //anti cheat for HP
                    
                    if (gameState.ContainsKey(intId))
                    {
                            //if the object already exists
                            gameState[intId] = data; //data being the original bytes of the packet
                    }
                    else //the object is new to the game
                    {
                            gameState.Add(intId, data);
                    }
                    
                    
                    
                }
                //if string relates to losing hp
                else if (text.Contains("lose hp;"))
                {
                    //get ID of enemy that was hit and the damage
                    Console.WriteLine(text);
                    string globalId = text.Split(";")[1];
                    int intId = Int32.Parse(globalId);
                    string weaponDmg = text.Split(";")[2];
                    int dmg = Int32.Parse(weaponDmg);

                    //loop trough all clients
                    foreach (IPEndPoint ep in connectedClients)
                    {
                       
                        Console.WriteLine("Sending event to " + ep.ToString());
                        if (ep.Port != 0)
                        {
                            //send the id of the enemy it and the dmg to all clients
                            string returnVal = ("Id:;" + intId + ";" + dmg + ";");
                            newsock.SendTo(Encoding.ASCII.GetBytes(returnVal), Encoding.ASCII.GetBytes(returnVal).Length,
                                    SocketFlags.None, ep);
                            Console.WriteLine("got event to " + ep.ToString());
                        }
                    }
                }

                    // connect the client 
                    bool IPisInList = false;
                IPEndPoint senderIPEndPoint = (IPEndPoint)newRemote;

                //loop trough all clients
                foreach (IPEndPoint ep in connectedClients)
                {
                    if (senderIPEndPoint.ToString().Equals(ep.ToString())) IPisInList = true;
                  
                    if (ep.Port != 0)
                    {
                        //Update data of the clients
                        foreach (KeyValuePair<int, byte[]> kvp in gameState)
                        {
                            newsock.SendTo(kvp.Value, kvp.Value.Length, SocketFlags.None, ep);
                        }
                    }
                }
                if (!IPisInList) //if client doesn't exist connect it
                {
                    connectedClients.Add(senderIPEndPoint);
                    Console.WriteLine("A new client just connected. There are now " + connectedClients.Count + " clients.");
                }               


            }
        }
        //check if key was pressed
        static private void KeyCheck()
        {

            while (true)
            {
                //if pressed esc close program
                if (Console.ReadKey().Key == ConsoleKey.Escape)
                {
                    Environment.Exit(0);
                    return;
                }
            }
        }

        static public void CheckCheating(string data)
        {
            //get ID of enemy that was hit and the current hp
            string globalId = data.Split(";")[1];
            int intId = Int32.Parse(globalId);
            string hpValue = data.Split(";")[9];
            int currentHP = Int32.Parse(hpValue);

            if (currentHP > 100)
            {
                Console.WriteLine("Player: " + intId + " was disconnected due to cheating");
            }           
            
        }

    }
}





