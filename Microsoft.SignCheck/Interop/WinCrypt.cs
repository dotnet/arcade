using System;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Interop
{
    // See WinCrypt.h
    // See https://support.microsoft.com/en-us/help/323809/how-to-get-information-from-authenticode-signed-executables
    public static class WinCrypt
    {
        //-------------------------------------------------------------------------
        // dwObjectType for CryptQueryObject
        //-------------------------------------------------------------------------
        public const int CERT_QUERY_OBJECT_FILE = 0x00000001;
        public const int CERT_QUERY_OBJECT_BLOB = 0x00000002;

        //-------------------------------------------------------------------------
        //dwContentType for CryptQueryObject
        //-------------------------------------------------------------------------
        //encoded single certificate
        public const int CERT_QUERY_CONTENT_CERT = 1;
        //encoded single CTL
        public const int CERT_QUERY_CONTENT_CTL = 2;
        //encoded single CRL
        public const int CERT_QUERY_CONTENT_CRL = 3;
        //serialized store
        public const int CERT_QUERY_CONTENT_SERIALIZED_STORE = 4;
        //serialized single certificate
        public const int CERT_QUERY_CONTENT_SERIALIZED_CERT = 5;
        //serialized single CTL
        public const int CERT_QUERY_CONTENT_SERIALIZED_CTL = 6;
        //serialized single CRL
        public const int CERT_QUERY_CONTENT_SERIALIZED_CRL = 7;
        //a PKCS#7 signed message
        public const int CERT_QUERY_CONTENT_PKCS7_SIGNED = 8;
        //a PKCS#7 message, such as enveloped message.  But it is not a signed message,
        public const int CERT_QUERY_CONTENT_PKCS7_UNSIGNED = 9;
        //a PKCS7 signed message embedded in a file
        public const int CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED = 10;
        //an encoded PKCS#10
        public const int CERT_QUERY_CONTENT_PKCS10 = 11;
        //an encoded PFX BLOB
        public const int CERT_QUERY_CONTENT_PFX = 12;
        //an encoded CertificatePair (contains forward and/or reverse cross certs)
        public const int CERT_QUERY_CONTENT_CERT_PAIR = 13;
        //an encoded PFX BLOB, which was loaded to phCertStore
        public const int CERT_QUERY_CONTENT_PFX_AND_LOAD = 14;

        //-------------------------------------------------------------------------
        //dwExpectedConentTypeFlags for CryptQueryObject
        //-------------------------------------------------------------------------
        //encoded single certificate
        public const int CERT_QUERY_CONTENT_FLAG_CERT = (1 << CERT_QUERY_CONTENT_CERT);
        //encoded single CTL
        public const int CERT_QUERY_CONTENT_FLAG_CTL = (1 << CERT_QUERY_CONTENT_CTL);
        //encoded single CRL
        public const int CERT_QUERY_CONTENT_FLAG_CRL = (1 << CERT_QUERY_CONTENT_CRL);
        //serialized store
        public const int CERT_QUERY_CONTENT_FLAG_SERIALIZED_STORE = (1 << CERT_QUERY_CONTENT_SERIALIZED_STORE);
        //serialized single certificate
        public const int CERT_QUERY_CONTENT_FLAG_SERIALIZED_CERT = (1 << CERT_QUERY_CONTENT_SERIALIZED_CERT);
        //serialized single CTL
        public const int CERT_QUERY_CONTENT_FLAG_SERIALIZED_CTL = (1 << CERT_QUERY_CONTENT_SERIALIZED_CTL);
        //serialized single CRL
        public const int CERT_QUERY_CONTENT_FLAG_SERIALIZED_CRL = (1 << CERT_QUERY_CONTENT_SERIALIZED_CRL);
        //an encoded PKCS#7 signed message
        public const int CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED = (1 << CERT_QUERY_CONTENT_PKCS7_SIGNED);
        //an encoded PKCS#7 message.  But it is not a signed message
        public const int CERT_QUERY_CONTENT_FLAG_PKCS7_UNSIGNED = (1 << CERT_QUERY_CONTENT_PKCS7_UNSIGNED);
        //the content includes an embedded PKCS7 signed message
        public const int CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED = (1 << CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED);
        //an encoded PKCS#10
        public const int CERT_QUERY_CONTENT_FLAG_PKCS10 = (1 << CERT_QUERY_CONTENT_PKCS10);
        //an encoded PFX BLOB
        public const int CERT_QUERY_CONTENT_FLAG_PFX = (1 << CERT_QUERY_CONTENT_PFX);
        //an encoded CertificatePair (contains forward and/or reverse cross certs)
        public const int CERT_QUERY_CONTENT_FLAG_CERT_PAIR = (1 << CERT_QUERY_CONTENT_CERT_PAIR);
        //an encoded PFX BLOB, and we do want to load it (not included in
        //CERT_QUERY_CONTENT_FLAG_ALL)
        public const int CERT_QUERY_CONTENT_FLAG_PFX_AND_LOAD = (1 << CERT_QUERY_CONTENT_PFX_AND_LOAD);
        //content can be any type
        public const int CERT_QUERY_CONTENT_FLAG_ALL = (
            CERT_QUERY_CONTENT_FLAG_CERT |
            CERT_QUERY_CONTENT_FLAG_CTL |
            CERT_QUERY_CONTENT_FLAG_CRL |
            CERT_QUERY_CONTENT_FLAG_SERIALIZED_STORE |
            CERT_QUERY_CONTENT_FLAG_SERIALIZED_CERT |
            CERT_QUERY_CONTENT_FLAG_SERIALIZED_CTL |
            CERT_QUERY_CONTENT_FLAG_SERIALIZED_CRL |
            CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED |
            CERT_QUERY_CONTENT_FLAG_PKCS7_UNSIGNED |
            CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED |
            CERT_QUERY_CONTENT_FLAG_PKCS10 |
            CERT_QUERY_CONTENT_FLAG_PFX |
            CERT_QUERY_CONTENT_FLAG_CERT_PAIR);

        //-------------------------------------------------------------------------
        //dwFormatType for CryptQueryObject
        //-------------------------------------------------------------------------
        //the content is in binary format
        public const int CERT_QUERY_FORMAT_BINARY = 1;
        //the content is base64 encoded
        public const int CERT_QUERY_FORMAT_BASE64_ENCODED = 2;
        //the content is ascii hex encoded with "{ASN}" prefix
        public const int CERT_QUERY_FORMAT_ASN_ASCII_HEX_ENCODED = 3;

        //-------------------------------------------------------------------------
        //dwExpectedFormatTypeFlags for CryptQueryObject
        //-------------------------------------------------------------------------
        //the content is in binary format
        public const int CERT_QUERY_FORMAT_FLAG_BINARY = (1 << CERT_QUERY_FORMAT_BINARY);
        //the content is base64 encoded
        public const int CERT_QUERY_FORMAT_FLAG_BASE64_ENCODED = (1 << CERT_QUERY_FORMAT_BASE64_ENCODED);
        //the content is ascii hex encoded with "{ASN}" prefix
        public const int CERT_QUERY_FORMAT_FLAG_ASN_ASCII_HEX_ENCODED = (1 << CERT_QUERY_FORMAT_ASN_ASCII_HEX_ENCODED);
        //the content can be of any format
        public const int CERT_QUERY_FORMAT_FLAG_ALL = (
            CERT_QUERY_FORMAT_FLAG_BINARY |
            CERT_QUERY_FORMAT_FLAG_BASE64_ENCODED |
            CERT_QUERY_FORMAT_FLAG_ASN_ASCII_HEX_ENCODED);

        //-------------------------------------------------------------------------
        //  Get parameter types and their corresponding data structure definitions.
        //-------------------------------------------------------------------------
        public const int CMSG_TYPE_PARAM = 1;
        public const int CMSG_CONTENT_PARAM = 2;
        public const int CMSG_BARE_CONTENT_PARAM = 3;
        public const int CMSG_INNER_CONTENT_TYPE_PARAM = 4;
        public const int CMSG_SIGNER_COUNT_PARAM = 5;
        public const int CMSG_SIGNER_INFO_PARAM = 6;
        public const int CMSG_SIGNER_CERT_INFO_PARAM = 7;
        public const int CMSG_SIGNER_HASH_ALGORITHM_PARAM = 8;
        public const int CMSG_SIGNER_AUTH_ATTR_PARAM = 9;
        public const int CMSG_SIGNER_UNAUTH_ATTR_PARAM = 10;
        public const int CMSG_CERT_COUNT_PARAM = 11;
        public const int CMSG_CERT_PARAM = 12;
        public const int CMSG_CRL_COUNT_PARAM = 13;
        public const int CMSG_CRL_PARAM = 14;
        public const int CMSG_ENVELOPE_ALGORITHM_PARAM = 15;
        public const int CMSG_RECIPIENT_COUNT_PARAM = 17;
        public const int CMSG_RECIPIENT_INDEX_PARAM = 18;
        public const int CMSG_RECIPIENT_INFO_PARAM = 19;
        public const int CMSG_HASH_ALGORITHM_PARAM = 20;
        public const int CMSG_HASH_DATA_PARAM = 21;
        public const int CMSG_COMPUTED_HASH_PARAM = 22;
        public const int CMSG_ENCRYPT_PARAM = 26;
        public const int CMSG_ENCRYPTED_DIGEST = 27;
        public const int CMSG_ENCODED_SIGNER = 28;
        public const int CMSG_ENCODED_MESSAGE = 29;
        public const int CMSG_VERSION_PARAM = 30;
        public const int CMSG_ATTR_CERT_COUNT_PARAM = 31;
        public const int CMSG_ATTR_CERT_PARAM = 32;
        public const int CMSG_CMS_RECIPIENT_COUNT_PARAM = 33;
        public const int CMSG_CMS_RECIPIENT_INDEX_PARAM = 34;
        public const int CMSG_CMS_RECIPIENT_ENCRYPTED_KEY_INDEX_PARAM = 35;
        public const int CMSG_CMS_RECIPIENT_INFO_PARAM = 36;
        public const int CMSG_UNPROTECTED_ATTR_PARAM = 37;
        public const int CMSG_SIGNER_CERT_ID_PARAM = 38;
        public const int CMSG_CMS_SIGNER_INFO_PARAM = 39;

        public const string szOID_RSA_signingTime = "1.2.840.113549.1.9.5";
        public const string szOID_RSA_counterSign = "1.2.840.113549.1.9.6";
        public const string szOID_RFC3161_counterSign = "1.3.6.1.4.1.311.3.3.1";
        public const string szOID_NESTED_SIGNATURE = "1.3.6.1.4.1.311.2.4.1";
        public const string szOID_TIMESTAMP_TOKEN = "1.2.840.113549.1.9.16.1.4";

        [DllImport("Crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CryptQueryObject(
            int dwObjectType,
            IntPtr pvObject,
            int dwExpectedContentTypeFlags,
            int dwExpectedFormatTypeFlags,
            int dwFlags,
            out int pdwMsgAndCertEncodingType,
            out int pdwContentType,
            out int pdwFormatType,
            ref IntPtr phCertStore,
            ref IntPtr phMsg,
            ref IntPtr ppvContext);

        [DllImport("Crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CryptMsgGetParam(
            IntPtr hCryptMsg,
            int dwParamType,
            int dwIndex,
            IntPtr pvData,
            ref int pcbData);

        [DllImport("Crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CryptMsgGetParam(
            IntPtr hCryptMsg,
            int dwParamType,
            int dwIndex,
            [In, Out] byte[] pvData,
            ref int pcbData);
    }
}
