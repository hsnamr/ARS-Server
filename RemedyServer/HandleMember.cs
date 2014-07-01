using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.Odbc;

namespace RemedyServer
{
    class HandleMember
    {
        private NetworkStream ns;
        private StreamReader reader;
        private StreamWriter writer;
        private OdbcConnection dbConn;
        private OdbcCommand sqlCommand;
        private OdbcDataReader dbReader;
        private Handle_File fileHandler;
        private string line, userName, command, fileName, jobDone, successor, allFiles = "";
        private int ticketNum, fileNumber, fileLength;
        private string[] fileNames;
        public HandleMember(OdbcConnection dbConn, NetworkStream ns, StreamReader reader, StreamWriter writer, string userName)
        {
            this.dbConn = dbConn;
            this.reader = reader;
            this.writer = writer;
            this.ns = ns;
            this.userName = userName;
            fileHandler = new Handle_File(ns, reader, writer);
            BeginHandling();
        }
        public void BeginHandling()//initializing the leader handler
        {
            try
            {
                //writer.WriteLine("I am not done");
                command = " SELECT T.Number, T.Assigner, T.Issue_Date, I.Assigned, I.Attachment, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements" +
                        " FROM Ticket AS T, Ticket_Information AS I" +
                        " WHERE T.Number=I.Number AND I.Assigned='" + userName + 
                        "' AND (I.Status = 'Assigned' OR I.Status='Waiting' OR I.Status='Work_In_Progress') AND I.Due_Date > #" + System.DateTime.Now + "#";
                sqlCommand = new OdbcCommand(command, dbConn);
                dbReader = sqlCommand.ExecuteReader();
                //Console.WriteLine("I am done");
                while (dbReader.Read())
                {
                    line = "";//empty the line so you can read the next row
                    for (int i = 0; i < dbReader.FieldCount - 1; i++)
                    {
                        line += dbReader.GetString(i) + "##";// but the whole row in one column and send it, seperated by commas
                    }
                    writer.WriteLine(line);
                    writer.Flush();
                }
                writer.WriteLine(".");// end of reading from the database
                writer.Flush();
                DB_Handler.DisposeAll(sqlCommand, dbReader);
                WaitForQueries();
                //do not forget to close the reader
            }
            catch
            {
                Console.WriteLine(" The Database is down please try again later");//for debugging server
            }
        }
        public void WaitForQueries()
        {
            do
            {
                try
                {
                    line = reader.ReadLine();//reader the command the user wants to issue
                    switch (line)
                    {
                        case "Ticket Info"://has been tested
                            GetTicketInfo();
                            AcceptedCommand();
                            break;
                        case "Get Attachment"://has been tested
                            GetAttachment();
                            AcceptedCommand();
                            break;
                        case "Update Status1":
                            UpdateStatus1();
                            AcceptedCommand();
                            break;
                        case "Update Status2":
                            UpdateStatus2();
                            AcceptedCommand();
                            break;
                        case "Quit":
                            line = "break";
                            AcceptedCommand();
                            break;
                        default:
                            writer.WriteLine("not known Command");
                            writer.Flush();
                            break;
                    }
                    
                }
                catch
                {
                    Console.WriteLine(" The Database is down please try again later");
                    DB_Handler.DisposeAll(sqlCommand, dbReader);
                }
            } while (line!="break");
        }
        public void GetTicketInfo()
        {
            ticketNum = int.Parse(reader.ReadLine());//get the ticket number you want to handle
            command = "SELECT T.Number, T.Issue_Date,I.Assigned, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements, I.Attachment " +
                "FROM Ticket_Information AS I, Ticket AS T WHERE T.Number=" + ticketNum + " AND T.Number=I.Number AND I.Assigned='"+userName+"'";//name,email,pass
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            while (dbReader.Read())
            {
                line = "";//empty the line so you can read the next row
                for (int i = 0; i < dbReader.FieldCount - 1; i++)
                {
                    line += dbReader.GetString(i) + "##";// but the whole row in one column and send it, seperated by ##
                }
                writer.WriteLine(line);
                writer.Flush();
            }
            writer.WriteLine(".");//sending the end of tickets info
            writer.Flush();
        }
        public void GetAttachment()//has been tested
        {
            ticketNum = int.Parse(reader.ReadLine());
            fileName = reader.ReadLine().Trim();
            fileHandler.GetAttachment(ticketNum, userName, fileName);
        }
        public void UpdateStatus2()
        {
            ticketNum = int.Parse(reader.ReadLine());
            fileNumber = int.Parse(reader.ReadLine());
            fileNames = new string[fileNumber];
            for (int i = fileNumber; i > 0; i--)
            {
                Console.WriteLine("Rading the file #: " + i);
                fileName = reader.ReadLine();
                fileNames[i - 1] = fileName;
                fileLength = int.Parse(reader.ReadLine());
                fileHandler.ReadAttachments(ticketNum, userName, fileName, fileLength);//uploading attachment to the server
            }
            jobDone = reader.ReadLine();// reading the job done by the client
            allFiles = "##";
            for (int i = 0; i < fileNumber; i++)// getting the new files
                allFiles += fileNames[i] + "##"; //seperate files by ## and insert their names to the database
            command = " Update Ticket_Information SET Status='Done', Attachment= Attachment + '"+ allFiles +"', JobDone='"+ jobDone +"' WHERE Assigned = '" + userName + "' AND Number=" + ticketNum;
            sqlCommand = new OdbcCommand(command, dbConn);
            sqlCommand.ExecuteNonQuery();
            UpdateSuccessor();
            
        }//has been tested
        public void UpdateStatus1()
        {
            ticketNum = int.Parse(reader.ReadLine());
            command = " Update Ticket_Information SET Status='Work_In_Progress' WHERE Assigned = '" + userName + "' AND Number=" + ticketNum;
            sqlCommand = new OdbcCommand(command, dbConn);
            sqlCommand.ExecuteNonQuery();
        }//has been tested
        public void AcceptedCommand()
        {
            writer.WriteLine("OK");//I am accepting the command and I have executed it
            writer.Flush();
            DB_Handler.DisposeAll(sqlCommand, dbReader);
        }
        public void UpdateSuccessor()
        {
            //Updating the Status of the succeding sequence
            command = "SELECT Sequence FROM Ticket_Information WHERE Number=" + ticketNum + " AND Assigned='" + userName + "'";
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            dbReader.Read();
            int seq = int.Parse(dbReader.GetString(0));
            command = "SELECT MAX(Sequence) FROM Ticket_Information WHERE Number=" + ticketNum;
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            dbReader.Read();
            int seq2 = int.Parse(dbReader.GetString(0));
            if (seq < seq2)// means there are successors
            {
                command = "SELECT Assigned FROM Ticket_Information WHERE Number=" + ticketNum+" AND Sequence="+(++seq);
                sqlCommand = new OdbcCommand(command, dbConn);
                dbReader = sqlCommand.ExecuteReader();
                dbReader.Read();
                successor = dbReader.GetString(0);
                string[] tokens = allFiles.Split('#');
                Directory.CreateDirectory(successor + "/" + ticketNum);
                for (int k = 0; k < tokens.Length; k++)
                {
                    try
                    {
                        File.Copy((userName + "/" + ticketNum + "/" + tokens[k]), (successor + "/" + ticketNum + "/" + tokens[k]),true);
                    }
                    catch(Exception ww)
                    {
                        Console.WriteLine(ww);//gives an exception but still works !!!!
                    }
                }
                command = " Update Ticket_Information SET Status='Assigned' , Attachment= Attachment + '" + allFiles + "' WHERE  Number=" + ticketNum + " AND Sequence =" + seq;//sequece has been updated earlier
                sqlCommand = new OdbcCommand(command, dbConn);
                sqlCommand.ExecuteNonQuery();// even if no one preceeding
            }   
        }
    }
}
