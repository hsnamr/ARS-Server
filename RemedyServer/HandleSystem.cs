using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.Odbc;

namespace RemedyServer
{
    class HandleSystem
    {
        private StreamReader reader;
        private StreamWriter writer;
        private OdbcConnection dbConn;
        private OdbcCommand sqlCommand;
        private string line= "", command, userName, password, email;
        public HandleSystem(OdbcConnection dbConn, StreamReader reader, StreamWriter writer)
        {
            this.dbConn = dbConn;
            this.reader = reader;
            this.writer = writer;
            BeginHandling();
        }
        public void BeginHandling()
        {
            do
            {
                line = reader.ReadLine();
                switch (line)
                {
                    case "Create Leader":
                        try
                        {
                            userName = reader.ReadLine();
                            email = reader.ReadLine();
                            password = reader.ReadLine();
                            command = "INSERT INTO User_Information Values('" + userName + "','" + email + "','" + password + "','Leader')";//name,email,pass
                            sqlCommand = new OdbcCommand(command, dbConn);
                            sqlCommand.ExecuteNonQuery();
                            writer.WriteLine("OK");//I have inserted the new user
                            writer.Flush();
                        }
                        catch
                        {
                            writer.WriteLine("Not OK");
                            writer.Flush();
                        }
                        break;

                        case "Delete Leader":
                            try
                            {
                                leaderName = reader.ReadLine();
                                command = "DELETE FROM User_Information WHERE Name ='" + leaderName + "' AND Role = 'Leader'";
                                sqlCommand = new OdbcCommand(command, dbConn);
                                sqlCommand.ExecuteNonQuery();
                                AcceptedCommand();
                            }
                            catch
                            {
                                writer.WriteLine("Not OK");
                                writer.Flush();
                            }
                            break;

                    case "Quit":
                        line = "Break";
                        break;

                    default:
                        writer.WriteLine("not known Command");
                        writer.Flush();
                        break;
                }
            } while (line != "Break");
        }
    }
}
