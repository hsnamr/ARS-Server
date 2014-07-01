using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Net;
using System.Net.Mail;
namespace RemedyServer
{
    class Mail_Sender
    {
        
        public static void SendMail(string from, string to, string subject, string body)
        {
            try
            {
                MailMessage mail= new MailMessage();
                SmtpClient client;
                mail.From = new MailAddress(from);
                mail.To.Add(new MailAddress(to));
                mail.Subject = subject;
                mail.Body = body;
                client = new SmtpClient("smtp.kfupm.edu.sa",25);
                client.Credentials = new NetworkCredential("s235865", "s235865");// not secure but when encoded to EXE and protected no problem
                client.SendCompleted += new SendCompletedEventHandler(SendCompletedCallback);
                client.SendAsync(mail, null);
            }
            catch
            {
                Console.WriteLine(" Not able to send the message");
            }
        }
        public static void SendCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
                Console.WriteLine("Send Operation Cancelled", "Error Sending Message");
            else if (e.Error != null)
                Console.WriteLine(e.Error.ToString(), "Error Sending Message");
            else
                Console.WriteLine("Mail Sent Successfully", "Good News");
        }
    }
}
