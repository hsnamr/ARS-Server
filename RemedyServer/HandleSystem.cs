using System.Data.Odbc;

namespace RemedyServer;

internal sealed class HandleSystem
{
    private readonly OdbcConnection _dbConn;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private OdbcCommand? _sqlCommand;
    private string? _line;
    private string? _userName;
    private string? _leaderName;
    private string? _password;
    private string? _email;

    public HandleSystem(OdbcConnection dbConn, StreamReader reader, StreamWriter writer)
    {
        _dbConn = dbConn;
        _reader = reader;
        _writer = writer;
        BeginHandling();
    }

    public void BeginHandling()
    {
        do
        {
            _line = _reader.ReadLine();
            switch (_line)
            {
                case "Create Leader":
                    try
                    {
                        _userName = _reader.ReadLine();
                        _email = _reader.ReadLine();
                        _password = _reader.ReadLine();
                        _sqlCommand = new OdbcCommand("INSERT INTO User_Information VALUES(?,?,?,'Leader')", _dbConn);
                        _sqlCommand.Parameters.AddWithValue("@name", _userName);
                        _sqlCommand.Parameters.AddWithValue("@email", _email);
                        _sqlCommand.Parameters.AddWithValue("@password", _password);
                        _sqlCommand.ExecuteNonQuery();
                        _writer.WriteLine("OK");
                        _writer.Flush();
                    }
                    catch
                    {
                        _writer.WriteLine("Not OK");
                        _writer.Flush();
                    }
                    break;
                case "Delete Leader":
                    try
                    {
                        _leaderName = _reader.ReadLine();
                        _sqlCommand = new OdbcCommand("DELETE FROM User_Information WHERE Name=? AND Role='Leader'", _dbConn);
                        _sqlCommand.Parameters.AddWithValue("@name", _leaderName);
                        _sqlCommand.ExecuteNonQuery();
                        AcceptedCommand();
                    }
                    catch
                    {
                        _writer.WriteLine("Not OK");
                        _writer.Flush();
                    }
                    break;
                case "Quit":
                    return;
                default:
                    _writer.WriteLine("not known Command");
                    _writer.Flush();
                    break;
            }
        } while (true);
    }

    private void AcceptedCommand()
    {
        _writer.WriteLine("OK");
        _writer.Flush();
    }
}
