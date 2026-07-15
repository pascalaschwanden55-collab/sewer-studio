using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Common;

public interface ISafeXmlDocumentLoader
{
    XDocument Load(string path);

    XDocument Load(string path, LoadOptions options);
}
