using System.Data.Odbc;

namespace RemedyServer;

internal static class DbHandler
{
    public static string? GetEmail(string name, OdbcConnection dbConn)
    {
        const string sql = "SELECT Email FROM User_Information WHERE Name=?";
        using var cmd = new OdbcCommand(sql, dbConn);
        cmd.Parameters.AddWithValue("@name", name);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? reader.GetString(0) : null;
    }

    public static void DisposeAll(OdbcCommand? sqlCommand, OdbcDataReader? dbReader)
    {
        sqlCommand?.Dispose();
        dbReader?.Close();
    }
}
