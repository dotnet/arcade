using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Microsoft.SignCheck.Verification
{
    public static class Xml
    {
        // See: https://msdn.microsoft.com/en-us/library/ms148731(v=vs.110).aspx
        // See also: https://docs.microsoft.com/en-us/dotnet/standard/security/how-to-verify-the-digital-signatures-of-xml-documents
        // The code differs from the MSDN sample as it checks certificates against the root store instead.
        public static bool IsSigned(string path, out X509Certificate2 signinCertificate)
        {
            signinCertificate = null;
            var xmlDoc = new XmlDocument()
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(path);

            var signatureNodes = xmlDoc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);

            if (signatureNodes.Count == 0)
            {
                return false;
            }

            var signedXml = new SignedXml(xmlDoc);
            signedXml.LoadXml((XmlElement)signatureNodes[0]);

            if (signedXml.Signature.KeyInfo.OfType<KeyInfoX509Data>().Count() == 0)
            {
                return false;
            }

            var certificates = signedXml.Signature.KeyInfo.OfType<KeyInfoX509Data>().First().Certificates;

            foreach (X509Certificate2 certificate in certificates)
            {
                if (signedXml.CheckSignature(certificate, verifySignatureOnly: true))
                {
                    using (var rootStore = new X509Store(StoreName.Root))
                    {
                        rootStore.Open(OpenFlags.IncludeArchived | OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

                        using (var chain = new X509Chain(useMachineContext: true))
                        {
                            chain.Build(certificate);
                            var numberOfChainElements = chain.ChainElements.Count;

                            X509ChainElement rootChainElement = null;

                            var subjectDistinguishedName = certificate.SubjectName;

                            // Locate the last element in the the chain as that should be the root, otherwise use the certificate we have
                            // and try to match that against a root certificate.
                            if (numberOfChainElements > 0)
                            {
                                rootChainElement = chain.ChainElements[numberOfChainElements - 1];
                                subjectDistinguishedName = rootChainElement.Certificate.SubjectName;
                            }

                            var rootCertificates = rootStore.Certificates;
                            var matchingRootCertificates = rootCertificates.Find(X509FindType.FindBySubjectDistinguishedName, subjectDistinguishedName.Name, validOnly: true);

                            if (matchingRootCertificates.Count > 0)
                            {
                                signinCertificate = matchingRootCertificates[0];
                                return true;
                            }
                            else
                            {
                                // cert may not be on the machine
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
