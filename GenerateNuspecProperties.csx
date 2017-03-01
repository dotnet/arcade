#r "System.Xml.Linq"
using System.Xml.Linq;

// Args: 
// - Path to Dependencies.props
// - Output file path

File.WriteAllLines(Args[1], 
    from e in XDocument.Load(Args[0]).Root.Descendants()
    where e.Name.LocalName.EndsWith("Version")
    select $"{e.Name.LocalName}={e.Value}");
