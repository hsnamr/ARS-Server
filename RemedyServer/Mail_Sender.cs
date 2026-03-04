using System.Net;
using System.Net.Mail;

namespace RemedyServer;

internal static class MailSender
{
    // TODO: Move to configuration (e.g. appsettings.json, environment variables)
    private const string SmtpHost = "smtp.kfupm.edu.sa";
    private const int SmtpPort = 25;

    public static void SendMail(string from, string to, string subject, string body)
    {
        try
        {
            var mail = new MailMessage(
                from,
                to,
                subject,
                body);
            using var client = new SmtpClient(SmtpHost, SmtpPort);
            client.Credentials = new NetworkCredential("s235865", "s235865");
            client.SendCompleted += (sender, e) =>
            {
                try
                {
                    if (e.UserState is MailMessage m)
                        m.Dispose();
                }
                finally
                {
                    SendCompletedCallback(sender, e);
                }
            };
            client.SendAsync(mail, mail);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Not able to send the message: {ex.Message}");
        }
    }

    private static void SendCompletedCallback(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
        if (e.Cancelled)
            Console.WriteLine("Send operation cancelled");
        else if (e.Error is { } err)
            Console.WriteLine($"Error sending message: {err.Message}");
        else
            Console.WriteLine("Mail sent successfully");
    }
}
