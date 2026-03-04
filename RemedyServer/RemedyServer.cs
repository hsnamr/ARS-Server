using System.Data.Odbc;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RemedyServer;

public static class RemedyServerProgram
{
    private const int Port = 9090;
    private static int _connectionCount;

    public static async Task Main(string[] args)
    {
        using var server = new TcpListener(IPAddress.Any, Port);
        server.Start();
        Console.WriteLine($"Waiting for clients on port {Port}");

        while (true)
        {
            try
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleConnectionAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept failed: {ex.Message}");
            }
        }
    }

    private static async Task HandleConnectionAsync(TcpClient client)
    {
        const string connectionString = "Driver={Microsoft Access Driver (*.mdb)};DBQ=Remedy.mdb";
        await using var dbConn = new OdbcConnection(connectionString);
        try
        {
            await dbConn.OpenAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database connection failed: {ex.Message}");
            client.Dispose();
            return;
        }

        try
        {
            using (client)
            using (var ns = client.GetStream())
            using (var reader = new StreamReader(ns, Encoding.UTF8))
            using (var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true })
            {
                Interlocked.Increment(ref _connectionCount);
                Console.WriteLine($"New client accepted: {_connectionCount} active connections");

                await writer.WriteLineAsync("Welcome to my server");

                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    return;

                if (line.Trim().Equals("Auth", StringComparison.OrdinalIgnoreCase))
                {
                    string? userName = await reader.ReadLineAsync();
                    string? password = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                    {
                        await writer.WriteLineAsync("Auth not OK");
                        return;
                    }

                    const string authSql = "SELECT * FROM User_Information WHERE Name=? AND Password=?";
                    await using var sqlCommand = new OdbcCommand(authSql, dbConn);
                    sqlCommand.Parameters.AddWithValue("@name", userName);
                    sqlCommand.Parameters.AddWithValue("@password", password);
                    await using var dbReader = await sqlCommand.ExecuteReaderAsync();

                    if (await dbReader.ReadAsync())
                    {
                        await writer.WriteLineAsync("Auth OK");
                        string role = dbReader.GetString(3);
                        await writer.WriteLineAsync($"Welcome {role}");

                        switch (role)
                        {
                            case "Leader":
                                new HandleLeader(dbConn, ns, reader, writer, userName);
                                break;
                            case "Member":
                                new HandleMember(dbConn, ns, reader, writer, userName);
                                break;
                            case "System":
                                new HandleSystem(dbConn, reader, writer);
                                break;
                            default:
                                await writer.WriteLineAsync("Unknown role");
                                break;
                        }
                    }
                    else
                    {
                        await writer.WriteLineAsync("Auth not OK");
                    }
                }
                else if (line.Trim().Equals("Quit", StringComparison.OrdinalIgnoreCase))
                {
                    // Client requested quit without auth
                }
            }
        }
        catch (SocketException)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
            Console.WriteLine($"Client disconnected: {_connectionCount} active connections");
        }
    }
}
