using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.Odbc;

namespace RemedyServer
{
    public class RemedyServer
    {
        public static void Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 9090);
            server.Start();
            OdbcConnection dbConn;
            string conString;
            while (true)
            {
                Console.WriteLine("Waiting for Client...");

                Console.WriteLine("Waiting for clients on port 9090");
                while (true)
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        conString = "Driver={Microsoft Access Driver (*.mdb)};DBQ=Remedy.mdb";
                        //check http://www.connectionstrings.com/ for accessing other databases/adapters
                        dbConn = new OdbcConnection(conString);
                        dbConn.Open();
                        ConnectionHandler handler = new ConnectionHandler(client.Client, dbConn);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(handler.HandleConnection));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Connection failed on port 9090");
                    }
                }
            }
        }
    }
    class ConnectionHandler
    {
        private Socket client;
        private NetworkStream ns;
        private StreamReader reader;
        private StreamWriter writer;
        private OdbcConnection dbConn;
        private OdbcCommand sqlCommand;
        private OdbcDataReader dbReader;
        private static int connections = 0;
        private string line, userName, password;
        public ConnectionHandler(Socket client, OdbcConnection dbConn)
        {
            this.dbConn = dbConn;
            this.client = client;
        }
        public void HandleConnection(Object state)
        {
            try
            {
                ns = new NetworkStream(client);
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                connections++;
                Console.WriteLine("New client accepted: {0} active connections", connections);
                writer.WriteLine("Welcome to my server");
                writer.Flush();
                line = null;
                try
                {
                    line = reader.ReadLine();
                    if (line.Trim().Equals("Auth"))
                    {
                        userName = reader.ReadLine();
                        password = reader.ReadLine();

                        string auth = "SELECT * FROM User_Information WHERE Name='" + userName +
                            "' AND Password='" + password + "'";

                        sqlCommand = new OdbcCommand(auth, dbConn);
                        sqlCommand.ExecuteNonQuery();
                        dbReader = sqlCommand.ExecuteReader();
                        if (dbReader.Read())
                        {
                            writer.WriteLine("Auth OK");
                            writer.Flush();
                            writer.WriteLine("Welcome " + dbReader.GetString(3));
                            writer.Flush();
                            if (dbReader.GetString(3).Equals("Leader"))
                                new HandleLeader(dbConn, ns, reader, writer, userName);
                            else if (dbReader.GetString(3).Equals("Member"))
                                new HandleMember(dbConn, ns, reader, writer, userName);
                            else if (dbReader.GetString(3).Equals("System"))
                                new HandleSystem(dbConn, reader, writer);
                        }
                        else
                            writer.WriteLine("Auth not OK");
                    }
                    else if (line.Trim().Equals("Quit"))
                        goto Skip;
                }
                catch (SocketException)
                {
                    writer.WriteLine("Error"); writer.Flush();
                }
                catch
                {
                }
                Skip:
                client.Close();
                ns.Close();
                dbConn.Close();
                connections--;
                Console.WriteLine("Client disconnected: {0}active connections", connections);
            }
            catch (Exception)
            {
                connections--;
                Console.WriteLine("Client disconnected: {0} active connections", connections);
            }
        }
    }
}

