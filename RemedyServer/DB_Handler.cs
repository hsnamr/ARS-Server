using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Text;

namespace RemedyServer
{
    class DB_Handler
    {
        /*
        public string[] ExecuteReader(string command)
        {
            string[] result;
            sqlCommand = new OdbcCommand(command, dbConn);
            dbReader = sqlCommand.ExecuteReader();
            int rwosCount=0;
            while(dbReader.HasRows)
                rwosCount++;
            Console.WriteLine("The rowsCount is " + rwosCount);
            result = new string[rwosCount];
            for (int j =0;dbReader.Read();j++)
            {
                for (int i = 0; i < dbReader.FieldCount - 1; i++)
                {
                    result[j] += dbReader.GetString(i) + "##";// but the whole row in one column and send it, seperated by commas
                }
            }
            return result;
        }
        */
        public static string GetEmail(string name, OdbcConnection dbConn)
        {
            string command = "Select Email From User_Information Where Name='" + name + "'";
            OdbcCommand sqlCommand = new OdbcCommand(command, dbConn);
            OdbcDataReader dbReader = sqlCommand.ExecuteReader();
            dbReader.Read();
            Console.WriteLine(dbReader.GetString(0));
            return dbReader.GetString(0);

        }
        public static void DisposeAll(OdbcCommand sqlCommand, OdbcDataReader dbReader)
        {
            sqlCommand.Dispose();
            dbReader.Close();
        }
    }
}
