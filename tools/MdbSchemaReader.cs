using System;
using System.Data;
using System.Data.OleDb;

class MdbSchemaReader
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: MdbSchemaReader <path-to-mdb>");
            return;
        }
        var path = args[0];
        var connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Persist Security Info=False;";
        using var conn = new OleDbConnection(connStr);
        conn.Open();
        DataTable tables = conn.GetSchema("Tables");
        foreach (DataRow row in tables.Rows)
        {
            string tableName = row["TABLE_NAME"].ToString();
            Console.WriteLine($"Table: {tableName}");
            DataTable columns = conn.GetSchema("Columns", new string[] { null, null, tableName, null });
            foreach (DataRow col in columns.Rows)
            {
                Console.WriteLine($"  - {col["COLUMN_NAME"]} ({col["DATA_TYPE"]})");
            }
        }
    }
}
