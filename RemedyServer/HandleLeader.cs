using System.Data.Odbc;

namespace RemedyServer;

/// <summary>
/// Handles leader-role commands (tickets, members, attachments).
/// </summary>
internal sealed class HandleLeader
{
    private readonly OdbcConnection _dbConn;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly HandleFile _fileHandler;
    private readonly string _userName;

    private OdbcCommand? _sqlCommand;
    private OdbcDataReader? _dbReader;
    private string? _line;
    private string? _assigned;
    private string? _memEmail;
    private string? _fileName;
    private string? _requirements;
    private string? _dueDate;
    private string? _memName;
    private string? _email;
    private string? _password;
    private string? _body;
    private string? _subject;
    private string[] _fileNames = Array.Empty<string>();
    private int _fileNumber;
    private int _fileLength;
    private int _seq;
    private int _ticketNum;

    public HandleLeader(OdbcConnection dbConn, System.Net.Sockets.NetworkStream ns, StreamReader reader, StreamWriter writer, string userName)
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
            _email = DbHandler.GetEmail(_userName, _dbConn);
            const string sql = "SELECT T.Number, T.Issue_Date, I.Assigned, I.Status, I.Due_Date " +
                               "FROM Ticket AS T, Ticket_Information AS I WHERE T.Number=I.Number AND T.Assigner =?";
            _sqlCommand = new OdbcCommand(sql, _dbConn);
            _sqlCommand.Parameters.AddWithValue("@assigner", _userName);
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
            DbHandler.DisposeAll(_sqlCommand, _dbReader);
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
                    case "Create Ticket":
                        SendMemNum();
                        SendMembers();
                        _seq = int.Parse(_reader.ReadLine() ?? "0");
                        CreateTicket();
                        for (int i = 1; i <= _seq; i++)
                            ReadTicket(i);
                        _writer.WriteLine("OK");
                        _writer.Flush();
                        break;
                    case "Create Member":
                        _memName = _reader.ReadLine();
                        _memEmail = _reader.ReadLine();
                        _password = _reader.ReadLine();
                        _body = $"Dear {_memName} :\nWelcome to our Group that is lead by {_userName}\nname: {_memName}\npassword: {_password}";
                        MailSender.SendMail(_email!, _memEmail!, "You are joining a new group", _body);
                        _sqlCommand = new OdbcCommand("INSERT INTO User_Information VALUES(?,?,?,'Member')", _dbConn);
                        _sqlCommand.Parameters.AddWithValue("@name", _memName);
                        _sqlCommand.Parameters.AddWithValue("@email", _memEmail);
                        _sqlCommand.Parameters.AddWithValue("@password", _password);
                        _sqlCommand.ExecuteNonQuery();
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
                            _memName = _reader.ReadLine();
                            _sqlCommand = new OdbcCommand("DELETE FROM User_Information WHERE Name=? AND Role='Member'", _dbConn);
                            _sqlCommand.Parameters.AddWithValue("@name", _memName);
                            _sqlCommand.ExecuteNonQuery();
                            AcceptedCommand();
                        }
                        catch
                        {
                            _writer.WriteLine("Not OK");
                            _writer.Flush();
                        }
                        break;
                    case "Referesh":
                        BeginHandling();
                        break;
                    case "Quit":
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
            }
        }
    }

    public void SendMemNum()
    {
        _sqlCommand = new OdbcCommand("SELECT COUNT(*) FROM User_Information WHERE ROLE='Member'", _dbConn);
        _dbReader = _sqlCommand.ExecuteReader();
        if (_dbReader.Read())
            _writer.WriteLine(_dbReader.GetString(0));
        _writer.Flush();
    }

    public void SendMembers()
    {
        DbHandler.DisposeAll(_sqlCommand, _dbReader);
        _sqlCommand = new OdbcCommand("SELECT Name FROM User_Information WHERE Role='Member'", _dbConn);
        _dbReader = _sqlCommand.ExecuteReader();
        while (_dbReader.Read())
        {
            _writer.WriteLine(_dbReader.GetString(0));
            _writer.Flush();
        }
    }

    public void ReadTicket(int sequence)
    {
        _assigned = _reader.ReadLine();
        _fileNumber = int.Parse(_reader.ReadLine() ?? "0");
        _fileNames = new string[_fileNumber];
        for (int i = _fileNumber; i > 0; i--)
        {
            _fileName = _reader.ReadLine();
            _fileNames[i - 1] = _fileName!;
            _fileLength = int.Parse(_reader.ReadLine() ?? "0");
            _fileHandler.ReadAttachments(_ticketNum, _assigned!, _fileName!, _fileLength);
        }
        _requirements = _reader.ReadLine();
        _dueDate = _reader.ReadLine();
        var allFiles = "##" + string.Join("##", _fileNames) + "##";
        string status = sequence == 1 ? "Assigned" : "Waiting";
        _sqlCommand = new OdbcCommand(
            "INSERT INTO Ticket_Information (Number, Assigned, Attachment, JobDone, Status, Sequence, Due_Date, Requirements) VALUES(?,?,?,'None',?," + sequence + ",?,?)", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _sqlCommand.Parameters.AddWithValue("@assigned", _assigned);
        _sqlCommand.Parameters.AddWithValue("@files", allFiles);
        _sqlCommand.Parameters.AddWithValue("@status", status);
        _sqlCommand.Parameters.AddWithValue("@due", _dueDate);
        _sqlCommand.Parameters.AddWithValue("@req", _requirements ?? "");
        _sqlCommand.ExecuteNonQuery();

        _subject = sequence == 1 ? "You have been Assigned a ticket" : "You have been Assigned a ticket but waiting in Sequence";
        _body = $"Dear {_assigned}:\nYou have been assigned a Ticket having a number: {_ticketNum}, The Due Date is: {_dueDate}\n\nOpen Your Remedy Client for more info";
        _memEmail = DbHandler.GetEmail(_assigned!, _dbConn);
        if (_memEmail is { } email)
            MailSender.SendMail(_email!, email, _subject, _body);
    }

    public void CreateTicket()
    {
        var date = DateTime.Now;
        _sqlCommand = new OdbcCommand("INSERT INTO Ticket (Assigner, Issue_Date) VALUES(?,?)", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@assigner", _userName);
        _sqlCommand.Parameters.AddWithValue("@date", date);
        _sqlCommand.ExecuteNonQuery();
        _sqlCommand = new OdbcCommand("SELECT Number FROM Ticket WHERE Issue_Date=?", _dbConn);
        _sqlCommand.Parameters.AddWithValue("@date", date);
        _dbReader = _sqlCommand.ExecuteReader();
        if (_dbReader.Read())
            _ticketNum = int.Parse(_dbReader.GetString(0));
    }

    public void GetTicketInfo()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _assigned = _reader.ReadLine();
        _sqlCommand = new OdbcCommand(
            "SELECT T.Number, T.Issue_Date, I.Assigned, I.JobDone, I.Status, I.Sequence, I.Due_Date, I.Requirements, I.Attachment " +
            "FROM Ticket_Information AS I, Ticket AS T WHERE I.Assigned=? AND T.Number=? AND T.Number=I.Number",
            _dbConn);
        _sqlCommand.Parameters.AddWithValue("@assigned", _assigned);
        _sqlCommand.Parameters.AddWithValue("@num", _ticketNum);
        _dbReader = _sqlCommand.ExecuteReader();
        while (_dbReader.Read())
        {
            _line = string.Join("##", Enumerable.Range(0, _dbReader.FieldCount).Select(i => _dbReader.GetString(i)));
            _writer.WriteLine(_line);
            _writer.Flush();
        }
        _writer.WriteLine(".");
        _writer.Flush();
    }

    public void GetAttachment()
    {
        _ticketNum = int.Parse(_reader.ReadLine() ?? "0");
        _assigned = _reader.ReadLine()?.Trim();
        _fileName = _reader.ReadLine()?.Trim();
        _fileHandler.GetAttachment(_ticketNum, _assigned!, _fileName!);
    }

    private void AcceptedCommand()
    {
        _writer.WriteLine("OK");
        _writer.Flush();
        DbHandler.DisposeAll(_sqlCommand, _dbReader);
        _sqlCommand = null;
        _dbReader = null;
    }
}
