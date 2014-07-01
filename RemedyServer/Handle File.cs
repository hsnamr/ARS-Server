using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace RemedyServer
{
    
    class Handle_File
    {
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private FileStream file;
        private DirectoryInfo user;
        private int tillNow,read;
        private byte[] buffer;
        private byte[] buffer2;
        public Handle_File(NetworkStream stream,StreamReader reader, StreamWriter writer)
        {
            this.reader = reader;
            this.writer = writer;
            this.stream = stream;
        }
        public void GetAttachment(int ticketNum, string assigned, string fileName)
        {
            //sending the attachments to the user
            file = new FileStream(assigned + "/" + ticketNum + "/" + fileName, FileMode.Open, FileAccess.Read);
            Console.WriteLine("I have opened the file with length : " + file.Length);
            writer.WriteLine(file.Length);//sending the size to the user
            writer.Flush();
            buffer2 = new byte[1024];
            tillNow = 0;
            while (file.Position < file.Length)
            {
                read = file.Read(buffer2, 0, buffer2.Length);
                stream.Write(buffer2, 0, read);
            }
            stream.Flush();
            file.Close();
        }
        
        public void ReadAttachments(int ticketNum, string assigned, string fileName, int fileLength)
        {// reading the attachment from the user
            
            user = new DirectoryInfo(assigned + "/" + ticketNum);
            user.Create();
            string actualName = fileName.Substring(fileName.LastIndexOf('/') + 1);
            file = new FileStream(assigned + "/" + ticketNum + "/" + actualName, FileMode.Create, FileAccess.Write);
            tillNow = 0;
            buffer = new byte[1024];
            while (tillNow < fileLength)
            {
                read = stream.Read(buffer, 0 ,buffer.Length);
                file.Write(buffer, 0, read);
                tillNow += read;
                Console.WriteLine(tillNow);
            }
            file.Close();
        }
    }
}
