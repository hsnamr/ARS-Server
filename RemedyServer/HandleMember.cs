using System.Data.Odbc;

namespace RemedyServer;

internal sealed class HandleMember
{
    private readonly OdbcConnection _dbConn;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly HandleFile _fileHandler;
    private readonly string _userName;

    private OdbcCommand? _sqlCommand;
    private OdbcDataReader? _dbReader;
    private string? _line;
    private string? _fileName;
    private string? _jobDone;
    private string? _successor;
    private string _allFiles = "";
    private int _ticketNum;
    private int _fileNumber;
    private int _fileLength;
    private string[] _fileNames = Array.Empty<string>();

    public HandleMember(OdbcConnection dbConn, System.Net.Sockets.NetworkStream ns, StreamReader reader, StreamWriter writer, string userName)
    {
        _dbConn = dbConn;
        _reader = reader;
        _writer = writer;
        _userName = userName;
        _fileHandler = new HandleFile(ns, reader, writer);
        BeginHandling();
    }

    public void BeginHandling()
    {
        try
        {
            _sqlCommand = new OdbcCommand(
                "SELECT T.Number, T.Assigner, T.Issue_Date, I.Assigned, I.Attachment, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements " +
                "FROM Ticket AS T, Ticket_Information AS I WHERE T.Number=I.Number AND I.Assigned=? " +
                "AND (I.Status='Assigned' OR I.Status='Waiting' OR I.Status='Work_In_Progress') AND I.Due_Date>?",
                _dbConn);
            _sqlCommand.Parameters.AddWithValue("@assigned", _userName);
            _sqlCommand.Parameters.AddWithValue("@now", DateTime.Now);
            _dbReader = _sqlCommand.ExecuteReader();
            while (_dbReader.Read())
            {
                _line = string.Join("##", Enumerable.Range(0, _dbReader.FieldCount - 1).Select(i => _dbReader.GetString(i)));
                _writer.WriteLine(_line);
                _writer.Flush();
            }
            _writer.WriteLine(".");
            _writer.Flush();
            DbHandler.DisposeAll(_sqlCommand, _dbReader);
            _sqlCommand = null;
            _dbReader = null;
            WaitForQueries();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database error: {ex.Message}");
        }
    }

    public void WaitForQueries()
    {
        while (true)
        {
            try
            {
                _line = _reader.ReadLine();
                switch (_line)
                {
                    case "Ticket Info":
                        GetTicketInfo();
                        AcceptedCommand();
                        break;
                    case "Get Attachment":
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
                        AcceptedCommand();
                        return;
                    default:
                        _writer.WriteLine("not known Command");
                        _writer.Flush();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error: {ex.Message}");
                DbHandler.DisposeAll(_sqlCommand, _dbReader);
            }
        }
    }

    public void GetTicketInfo()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _sqlCommand = new OdbcCommand(
            "SELECT T.Number, T.Issue_Date, I.Assigned, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements, I.Attachment " +
            "FROM Ticket_Information AS I, Ticket AS T WHERE T.Number=? AND T.Number=I.Number AND I.Assigned=?",
            _dbConn);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.Parameters.AddWithValue("@assigned", _userName);
        _dbReader = _sqlCommand.ExecuteReader();
        while (_dbReader.Read())
        {
            _line = string.Join("##", Enumerable.Range(0, _dbReader.FieldCount - 1).Select(i => _dbReader.GetString(i)));
            _writer.WriteLine(_line);
            _writer.Flush();
        }
        _writer.WriteLine(".");
        _writer.Flush();
    }

    public void GetAttachment()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _fileName = _reader.ReadLine()?.Trim();
        _fileHandler.GetAttachment(_ticketNum, _userName, _fileName!);
    }

    public void UpdateStatus2()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _fileNumber = int.Parse(_reader.ReadLine() ?? "0");
        _fileNames = new string[_fileNumber];
        for (int i = _fileNumber; i > 0; i--)
        {
            _fileName = _reader.ReadLine();
            _fileNames[i - 1] = _fileName!;
            _fileLength = int.Parse(_reader.ReadLine() ?? "0");
            _fileHandler.ReadAttachments(_ticketNum, _userName, _fileName!, _fileLength);
        }
        _jobDone = _reader.ReadLine();
        _allFiles = "##" + string.Join("##", _fileNames) + "##";
        // Access/Jet uses & for string concatenation
        _sqlCommand = new OdbcCommand(
            "UPDATE Ticket_Information SET Status='Done', Attachment=[Attachment] & ?, JobDone=? WHERE Assigned=? AND Number=?",
            _dbConn);
        _sqlCommand.Parameters.AddWithValue("@files", _allFiles);
        _sqlCommand.Parameters.AddWithValue("@jobDone", _jobDone);
        _sqlCommand.Parameters.AddWithValue("@assigned", _userName);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.ExecuteNonQuery();
        UpdateSuccessor();
    }

    public void UpdateStatus1()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _sqlCommand = new OdbcCommand("UPDATE Ticket_Information SET Status='Work_In_Progress' WHERE Assigned=? AND Number=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@assigned", _userName);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.ExecuteNonQuery();
    }

    private void AcceptedCommand()
    {
        _writer.WriteLine("OK");
        _writer.Flush();
        DbHandler.DisposeAll(_sqlCommand, _dbReader);
        _sqlCommand = null;
        _dbReader = null;
    }

    public void UpdateSuccessor()
    {
        _sqlCommand = new OdbcCommand("SELECT Sequence FROM Ticket_Information WHERE Number=? AND Assigned=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.Parameters.AddWithValue("@assigned", _userName);
        _dbReader = _sqlCommand.ExecuteReader();
        if (!_dbReader.Read())
            return;
        int seq = int.Parse(_dbReader.GetString(0));
        DbHandler.DisposeAll(_sqlCommand, _dbReader);

        _sqlCommand = new OdbcCommand("SELECT MAX(Sequence) FROM Ticket_Information WHERE Number=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _dbReader = _sqlCommand.ExecuteReader();
        if (!_dbReader.Read())
            return;
        int seq2 = int.Parse(_dbReader.GetString(0));
        DbHandler.DisposeAll(_sqlCommand, _dbReader);

        if (seq >= seq2)
            return;

        seq++;
        _sqlCommand = new OdbcCommand("SELECT Assigned FROM Ticket_Information WHERE Number=? AND Sequence=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.Parameters.AddWithValue("@seq", seq);
        _dbReader = _sqlCommand.ExecuteReader();
        if (!_dbReader.Read())
            return;
        _successor = _dbReader.GetString(0);
        DbHandler.DisposeAll(_sqlCommand, _dbReader);

        string[] tokens = _allFiles.Split('#', StringSplitOptions.RemoveEmptyEntries);
        var destDir = Path.Combine(_successor!, _ticketNum.ToString());
        Directory.CreateDirectory(destDir);
        var srcDir = Path.Combine(_userName, _ticketNum.ToString());
        foreach (string token in tokens)
        {
            try
            {
                var src = Path.Combine(srcDir, token);
                var dest = Path.Combine(destDir, token);
                File.Copy(src, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        _sqlCommand = new OdbcCommand("UPDATE Ticket_Information SET Status='Assigned', Attachment=[Attachment] & ? WHERE Number=? AND Sequence=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@files", _allFiles);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.Parameters.AddWithValue("@seq", seq);
        _sqlCommand.ExecuteNonQuery();
    }
}
