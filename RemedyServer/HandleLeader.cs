using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.Odbc;
using System.Text;
/* Created By: Brahim Al-Hawwas
 * Last Modified: 7:35 13/12/2007
 * Notices: Tested with telnet, but file reading does not work
 * 
 */
namespace RemedyServer
{
    class HandleLeader
    {
        private NetworkStream ns;
        private StreamReader reader;
        private StreamWriter writer;
        private OdbcConnection dbConn;
        private OdbcCommand sqlCommand;
        private OdbcDataReader dbReader;
        private Handle_File fileHandler;
        private string line, command, userName, assigned, memEmail,fileName, requirenments;
        private string due_Date, memName, email, password, body, subject;
        private string[] fileNames;
        private int fileNumber,fileLength, seq, ticketNum;
        public HandleLeader(OdbcConnection dbConn,NetworkStream ns, StreamReader reader, StreamWriter writer, string userName)
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
                email = DB_Handler.GetEmail(userName, dbConn);
                // command = " SELECT T.Number, T.Issue_Date, I.Assigned, I.Attachment, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements" +
                //        " FROM Ticket AS T, Ticket_Information AS I WHERE T.Number=I.Number";
                command = " SELECT T.Number, T.Issue_Date, I.Assigned,I.Status, I.Due_Date" +
                        " FROM Ticket AS T, Ticket_Information AS I WHERE T.Number=I.Number AND T.Assigner ='" + userName + "'";
                sqlCommand = new OdbcCommand(command, dbConn);
                dbReader = sqlCommand.ExecuteReader();
                while (dbReader.Read())
                {
                    line = "";//empty the line so you can read the next row
                    for (int i = 0; i < dbReader.FieldCount-1; i++)
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
            }
            catch
            {
                Console.WriteLine(" The Database is down please try again later");//for debugging server
                DB_Handler.DisposeAll(sqlCommand, dbReader);
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
                        case "Create Ticket":
                            SendMemNum();
                            SendMembers();
                            seq = int.Parse(reader.ReadLine());
                            Console.WriteLine("the sequence is  " + seq);
                            CreateTicket();//Create an initial ticket that is mapped to Ticket Table
                            for (int i = 1; i <= seq; i++)
                                ReadTicket(i);
                            //end of reading the number of members and sending it to client
                            writer.WriteLine("OK");
                            writer.Flush();
                            break;

                        case "Create Member":
                            memName = reader.ReadLine();
                            memEmail = reader.ReadLine();
                            password = reader.ReadLine();
                            body = "Dear " + memName + " :\n Welcome to our Group that is lead by " + 
                                userName + "\n name: " + memName + "\n password: " + password;
                            Mail_Sender.SendMail(email, memEmail, "You are joining a new group", body); 
                            command = "INSERT INTO User_Information Values('" + memName + "','" + memEmail + "','" + password +"','Member')";//name,email,pass
                            sqlCommand = new OdbcCommand(command, dbConn);
                            sqlCommand.ExecuteNonQuery();
                            AcceptedCommand();
                            break;

                        case "Ticket Info":
                            GetTicketInfo();
                            AcceptedCommand();
                            break;

                        case "Get Attachment":
                            GetAttachment();
                            AcceptedCommand();
                            break;

                        case "Delete Member":
                            try
                            {
                                memName = reader.ReadLine();
                                command = "DELETE FROM User_Information WHERE Name ='" + memName + "' AND Role = 'Member'";
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

                        case "Referesh":
                            BeginHandling();
                            break;

                        case "Quit":
                            line = "break";
                            break;

                        default:
                            writer.WriteLine("not known Command");
                            writer.Flush();
                            break;
                    }
                }
                catch
                {
                    Console.WriteLine(" The Database is down please try again later");//for debugging server only
                }
            } while (line!="break");
        }
        
        public void SendMemNum()
        {
            command = "SELECT COUNT(*) FROM User_Information WHERE ROLE='Member'";
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            dbReader.Read();
            Console.WriteLine(" I have got the count" + dbReader.GetString(0));
            writer.WriteLine(dbReader.GetString(0));
            writer.Flush();
        }
        public void SendMembers()
        {
            command = "SELECT Name FROM User_Information WHERE Role='Member'";
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            while (dbReader.Read())
            {
                writer.WriteLine(dbReader.GetString(0));
                writer.Flush();
            }
        }
        public void ReadTicket(int sequence)
        {
            assigned = reader.ReadLine();
            Console.WriteLine("The assigned is: "+assigned);
            fileNumber = int.Parse(reader.ReadLine());
            Console.WriteLine("The number of files are: "+fileNumber);
            fileNames = new string[fileNumber];
            for (int i = fileNumber; i > 0; i--)
            {
                Console.WriteLine("Reading the file #: " + i);
                fileName = reader.ReadLine();
                Console.WriteLine("The file Name is: "+fileName);
                fileNames[i - 1] = fileName;
                fileLength = int.Parse(reader.ReadLine());
                Console.WriteLine("The File Length is: " + fileLength);
                fileHandler.ReadAttachments(ticketNum, assigned, fileName, fileLength);//uploading attachment to the server
                Console.WriteLine("I have read the attachments");
            }
            requirenments = reader.ReadLine();// reading the requirements
            Console.WriteLine("The req are " + requirenments);
            due_Date = reader.ReadLine();//reading the due-date
            Console.WriteLine("the Due_date is: " + due_Date);
            
            string allFiles="##";
            for (int i = 0; i < fileNumber; i++)
            {
                allFiles += fileNames[i]+"##"; //seperate files by ## and insert their names to the database
            }
            if (sequence == 1)
            {
                command = "INSERT INTO Ticket_Information " +
                    "Values('" + ticketNum + "','" + assigned + "','" + allFiles + "','None','Assigned','" + sequence + "',#" + due_Date + "#,'" + requirenments + "')";
                subject = "You have been Assigned a ticket";
                body = "Dear " + assigned + ":\n You have been assigned a Ticket having a number: " +
                    ticketNum + ", The Due Date is: " + due_Date + "\n\n Open Your Remedy Client for more info";
                
            }
            else
            {
                command = "INSERT INTO Ticket_Information " +
                        "Values('" + ticketNum + "','" + assigned + "','" + allFiles + "','None','Waiting','" + sequence + "',#" + due_Date + "#,'" + requirenments + "')";
                subject = "You have been Assigned a ticket but waiting in Sequence";
                body = "Dear " + assigned + ":\n You have been assigned a Ticket having a number: " +
                    ticketNum + ", The Due Date is: " + due_Date + "\n\n Open Your Remedy Client for more info";
                
            }
            memEmail = DB_Handler.GetEmail(assigned,dbConn);
            Mail_Sender.SendMail(email, memEmail, subject, body);
            sqlCommand = new OdbcCommand(command, dbConn);
            sqlCommand.ExecuteNonQuery();
        }
        public void CreateTicket()//No reading/writing   from/to the user, Just setting the ticketNum
        {
            DateTime date = System.DateTime.Now;
            command = "INSERT INTO Ticket (Assigner, Issue_Date) Values('" + userName + "','" + date + "')";
            sqlCommand = new OdbcCommand(command, dbConn);
            sqlCommand.ExecuteNonQuery();
            command = " SELECT Number" +
                        " FROM Ticket WHERE Issue_Date=#" + date + "#";
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            dbReader.Read();
            ticketNum = int.Parse(dbReader.GetString(0));
        }
        public void GetTicketInfo()
        {
            ticketNum = int.Parse(reader.ReadLine());//get the ticket number you want to handle
            assigned = reader.ReadLine();
            command = "SELECT T.Number, T.Issue_Date,I.Assigned, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements, I.Attachment " +
                "FROM Ticket_Information AS I, Ticket AS T WHERE I.Assigned = '"+assigned+"'AND T.Number=" + ticketNum + " AND T.Number=I.Number";//name,email,pass
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            while (dbReader.Read())
            {
                line = "";//empty the line so you can read the next row
                for (int i = 0; i < dbReader.FieldCount ; i++)
                {
                    line += dbReader.GetString(i) + "##";// but the whole row in one column and send it, seperated by ##
                }
                writer.WriteLine(line);
                writer.Flush();
            }
            writer.WriteLine(".");//sending the end of tickets info
            writer.Flush();
        }
        public void GetAttachment()
        {
            ticketNum = int.Parse(reader.ReadLine());
            assigned = reader.ReadLine().Trim();
            fileName = reader.ReadLine().Trim();
            fileHandler.GetAttachment(ticketNum, assigned, fileName);
        }
        public void AcceptedCommand()
        {
            writer.WriteLine("OK");//I am accepting the command and I have executed it
            writer.Flush();
            DB_Handler.DisposeAll(sqlCommand, dbReader);
        }
    }
}
