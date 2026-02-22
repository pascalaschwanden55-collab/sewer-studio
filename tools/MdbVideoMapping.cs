using System;
using System.Data;
using System.Data.OleDb;

class MdbVideoMapping
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
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SI_Sectionname, SI_Section_ID, SI_MediaLocation, SI_MediaNumber1, SI_MediaNumber2, SI_DVD_PDFName, SI_ProtocolFile, SI_VCR_PDFName, SI_VCRNumber1, SI_VCRNumber2, SI_Virtual~ClipFilename FROM SI_T";
        using var reader = cmd.ExecuteReader();
        Console.WriteLine("Haltung\tMediaLocation\tMediaNumber1\tMediaNumber2\tDVD_PDFName\tProtocolFile\tVCR_PDFName\tVCRNumber1\tVCRNumber2\tVirtualClipFilename");
        while (reader.Read())
        {
            Console.WriteLine($"{reader["SI_Sectionname"]}\t{reader["SI_MediaLocation"]}\t{reader["SI_MediaNumber1"]}\t{reader["SI_MediaNumber2"]}\t{reader["SI_DVD_PDFName"]}\t{reader["SI_ProtocolFile"]}\t{reader["SI_VCR_PDFName"]}\t{reader["SI_VCRNumber1"]}\t{reader["SI_VCRNumber2"]}\t{reader["SI_Virtual~ClipFilename"]}");
        }
    }
}
