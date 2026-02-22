using System;
using System.Data;
using System.Data.OleDb;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: MdbVideoMapping <path-to-mdb>");
            return;
        }
        var path = args[0];
        var connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Persist Security Info=False;";
        using var conn = new OleDbConnection(connStr);
        conn.Open();
        // Hole alle Spaltennamen aus SI_T
        DataTable columns = conn.GetSchema("Columns", new string[] { null, null, "SI_T", null });
        var wanted = new List<string> { "SI_Sectionname", "SI_Section_ID", "SI_MediaLocation", "SI_MediaNumber1", "SI_MediaNumber2", "SI_DVD_PDFName", "SI_ProtocolFile", "SI_VCR_PDFName", "SI_VCRNumber1", "SI_VCRNumber2", "SI_Feature1FileName", "SI_Feature2FileName", "SI_Feature3FileName" };
        var available = new List<string>();
        foreach (DataRow col in columns.Rows)
        {
            var name = col["COLUMN_NAME"].ToString();
            if (wanted.Contains(name))
                available.Add(name);
        }
        if (available.Count == 0)
        {
            Console.WriteLine("No relevant fields found in SI_T.");
            return;
        }
        var select = string.Join(", ", available);
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {select} FROM SI_T";
        using var reader = cmd.ExecuteReader();
        Console.WriteLine(string.Join("\t", available));
        while (reader.Read())
        {
            var vals = new List<string>();
            foreach (var col in available)
                vals.Add(reader[col]?.ToString() ?? "");
            Console.WriteLine(string.Join("\t", vals));
        }
    }
}
