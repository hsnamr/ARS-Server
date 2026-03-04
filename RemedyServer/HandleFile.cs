using System.Data.Odbc;
using System.Net.Sockets;
using System.IO;

namespace RemedyServer;

/// <summary>
/// Handles file transfer over the network stream (attachments).
/// </summary>
internal sealed class HandleFile
{
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public HandleFile(NetworkStream stream, StreamReader reader, StreamWriter writer)
    {
        _reader = reader;
        _writer = writer;
        _stream = stream;
    }

    public void GetAttachment(int ticketNum, string assigned, string fileName)
    {
        var path = Path.Combine(assigned, ticketNum.ToString(), fileName);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _writer.WriteLine(file.Length);
        _writer.Flush();
        var buffer = new byte[1024];
        int read;
        while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
            _stream.Write(buffer, 0, read);
        _stream.Flush();
    }

    public void ReadAttachments(int ticketNum, string assigned, string fileName, int fileLength)
    {
        var dir = Path.Combine(assigned, ticketNum.ToString());
        Directory.CreateDirectory(dir);
        var actualName = fileName.Contains('/') ? fileName[(fileName.LastIndexOf('/') + 1)..] : fileName;
        var filePath = Path.Combine(dir, actualName);
        using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        var buffer = new byte[1024];
        int tillNow = 0;
        while (tillNow < fileLength)
        {
            int read = _stream.Read(buffer, 0, buffer.Length);
            file.Write(buffer, 0, read);
            tillNow += read;
        }
    }
}
