namespace AuswertungPro.Next.Infrastructure.Tests.Import;
public sealed partial class SchachtProQrImportTests
{
// Unabhaengig mit Python/zlib erzeugt, keine Kundendaten.
private const string Fixture = "SPQR1:eJxVkUFrwzAMhf9K0TkNTkbakttg3QaD0rWDHUoPrq0Sr4kdZGejK_nvk5OxtYcQIr3v6Um5gFcVNhJKUFXq8Qtp6kOnjUu5IVUVWnIpP8EpV0MCn0jeOAtlloB3HSlkdDtK1-RYweIPVAHKC1jZxPbrZvqGPkA_NEcn7o74C56jw3qQTIXI2OJ39IpGeCwq50gbKwP6SCviFyzX26cyF8WMBSh5RpnPioUQIs2LBKyjUHHUbJHF0rzgBA2rkB5kkNFFYxsVkKezBVto06Ad94OIxMjKWW3CUOPMlTyGFarT8IE1L4oayh28d_SNtYV9AsT3pFNcyvASB0kT2UyeZe2hH-0sU-zH-Xd8o645IA33DOc2nuu-87XsjjHOkKMQcfvaxaGQ5Zzpn8r_qKWx11Qurqn5DXTH_8G0-Oio4d6msxr6fd__AG8Qphc:1EF7D4BD";
private const string Json = """
{"schema":"ch.sewer-studio.schachtpro.protocol","version":1,"source":"SchachtPro","project":{"name":"QR-Test"},"protocol":{"sourceKey":"SP-Test-001","schachtNr":"QR-001","coordinates":{"crs":"EPSG:2056","east":2658000.25,"north":1181000.75},"masterData":{"depth":"2.68","dimension":"1000"},"condition":{"shaftNeck":{"selected":["Wurzeln"],"remark":"Sichtbar am Hals"}},"connections":[{"number":1,"type":"Auslauf","dn":"150","clock":"12"},{"number":2,"type":"Einlauf","dn":"200","clock":"7"},{"number":3,"pipeForm":"Rund"}]}}
""";
}
