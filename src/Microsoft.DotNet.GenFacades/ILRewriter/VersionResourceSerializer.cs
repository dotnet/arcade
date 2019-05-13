// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This code is from Roslyn/Source/Compilers/Core/CvtRes.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DWORD = System.UInt32;
using WCHAR = System.Char;
using WORD = System.UInt16;

namespace Microsoft.DotNet.GenFacades
{
    internal class VersionResourceSerializer
    {
        private readonly string _commentsContents;
        private readonly string _companyNameContents;
        private readonly string _fileDescriptionContents;
        private readonly Version _fileVersion;
        private readonly string _internalNameContents;
        private readonly string _legalCopyrightContents;
        private readonly string _legalTrademarksContents;
        private readonly string _originalFileNameContents;
        private readonly string _productNameContents;
        private readonly Version _productVersion;
        private readonly Version _assemblyVersion;

        private const string vsVersionInfoKey = "VS_VERSION_INFO";
        private const string varFileInfoKey = "VarFileInfo";
        private const string translationKey = "Translation";
        private const string stringFileInfoKey = "StringFileInfo";
        private readonly string _langIdAndCodePageKey; //should be 8 characters
        private const DWORD CP_WINUNICODE = 1200;

        private const ushort sizeVS_FIXEDFILEINFO = sizeof(DWORD) * 13;
        private readonly bool _isDll;

        internal VersionResourceSerializer(bool isDll, string comments, string companyName, string fileDescription, Version fileVersion,
            string internalName, string legalCopyright, string legalTrademark, string originalFileName, string productName, Version productVersion,
            Version assemblyVersion)
        {
            _isDll = isDll;
            _commentsContents = comments;
            _companyNameContents = companyName;
            _fileDescriptionContents = fileDescription;
            _fileVersion = fileVersion;
            _internalNameContents = internalName;
            _legalCopyrightContents = legalCopyright;
            _legalTrademarksContents = legalTrademark;
            _originalFileNameContents = originalFileName;
            _productNameContents = productName;
            _productVersion = productVersion;
            _assemblyVersion = assemblyVersion;
            _langIdAndCodePageKey = System.String.Format("{0:x4}{1:x4}", 0 /*langId*/, CP_WINUNICODE /*codepage*/);
        }

        private const uint VFT_APP = 0x00000001;
        private const uint VFT_DLL = 0x00000002;

        private IEnumerable<KeyValuePair<string, string>> GetVerStrings()
        {
            if (_commentsContents != null) yield return new KeyValuePair<string, string>("Comments", _commentsContents);
            if (_companyNameContents != null) yield return new KeyValuePair<string, string>("CompanyName", _companyNameContents);
            if (_fileDescriptionContents != null) yield return new KeyValuePair<string, string>("FileDescription", _fileDescriptionContents);
            if (_fileVersion != null) yield return new KeyValuePair<string, string>("FileVersion", _fileVersion.ToString());
            if (_internalNameContents != null) yield return new KeyValuePair<string, string>("InternalName", _internalNameContents);
            if (_legalCopyrightContents != null) yield return new KeyValuePair<string, string>("LegalCopyright", _legalCopyrightContents);
            if (_legalTrademarksContents != null) yield return new KeyValuePair<string, string>("LegalTrademarks", _legalTrademarksContents);
            if (_originalFileNameContents != null) yield return new KeyValuePair<string, string>("OriginalFilename", _originalFileNameContents);
            if (_productNameContents != null) yield return new KeyValuePair<string, string>("ProductName", _productNameContents);
            if (_productVersion != null) yield return new KeyValuePair<string, string>("ProductVersion", _productVersion.ToString());
            if (_assemblyVersion != null) yield return new KeyValuePair<string, string>("Assembly Version", _assemblyVersion.ToString());
        }

        private uint FileType { get { return (_isDll) ? VFT_DLL : VFT_APP; } }

        private void WriteVSFixedFileInfo(BinaryWriter writer)
        {
            writer.Write((DWORD)0xFEEF04BD);
            writer.Write((DWORD)0x00010000);
            writer.Write((DWORD)(_fileVersion.Major << 16) | (uint)_fileVersion.Minor);
            writer.Write((DWORD)(_fileVersion.Build << 16) | (uint)_fileVersion.Revision);
            writer.Write((DWORD)(_productVersion.Major << 16) | (uint)_productVersion.Minor);
            writer.Write((DWORD)(_productVersion.Build << 16) | (uint)_productVersion.Revision);
            writer.Write((DWORD)0x0000003F);   //VS_FFI_FILEFLAGSMASK  (EDMAURER) really? all these bits are valid?
            writer.Write((DWORD)0);    //file flags
            writer.Write((DWORD)0x00000004);   //VOS__WINDOWS32
            writer.Write((DWORD)this.FileType);
            writer.Write((DWORD)0);    //file subtype
            writer.Write((DWORD)0);    //date most sig
            writer.Write((DWORD)0);    //date least sig
        }

        /// <summary>
        /// Assume that 3 WORDs preceded this string and that the they began 32-bit aligned.
        /// Given the string length compute the number of bytes that should be written to end
        /// the buffer on a 32-bit boundary</summary>
        /// <param name="cb"></param>
        /// <returns></returns>
        private static int PadKeyLen(int cb)
        {
            //add previously written 3 WORDS, round up, then subtract the 3 WORDS.
            return PadToDword(cb + 3 * sizeof(WORD)) - 3 * sizeof(WORD);
        }
        /// <summary>
        /// assuming the length of bytes submitted began on a 32-bit boundary,
        /// round up this length as necessary so that it ends at a 32-bit boundary.
        /// </summary>
        /// <param name="cb"></param>
        /// <returns></returns>
        private static int PadToDword(int cb)
        {
            return (cb + 3) & ~3;
        }

        private const int HDRSIZE = 3 * sizeof(ushort);

        private static ushort SizeofVerString(string lpszKey, string lpszValue)
        {
            int cbKey, cbValue;

            cbKey = (lpszKey.Length + 1) * 2;  // Make room for the NULL
            cbValue = (lpszValue.Length + 1) * 2;
            if (cbKey + cbValue >= 0xFFF0)
                return 0xFFFF;

            return (ushort)(PadKeyLen(cbKey) +    // key, 0 padded to DWORD boundary
                            PadToDword(cbValue) +  // value, 0 padded to dword boundary
                            HDRSIZE);             // block header.
        }

        private static void WriteVersionString(KeyValuePair<string, string> keyValuePair, BinaryWriter writer)
        {
            System.Diagnostics.Debug.Assert(keyValuePair.Value != null);

            ushort cbBlock = SizeofVerString(keyValuePair.Key, keyValuePair.Value);
            int cbKey = (keyValuePair.Key.Length + 1) * 2;     // includes terminating NUL
            int cbVal = (keyValuePair.Value.Length + 1) * 2;     // includes terminating NUL

            var startPos = writer.BaseStream.Position;

            writer.Write((WORD)cbBlock);
            writer.Write((WORD)(keyValuePair.Value.Length + 1)); //add 1 for nul
            writer.Write((WORD)1);
            writer.Write(keyValuePair.Key.ToCharArray());
            writer.Write((WORD)'\0');
            writer.Write(new byte[PadKeyLen(cbKey) - cbKey]);
            writer.Write(keyValuePair.Value.ToCharArray());
            writer.Write((WORD)'\0');
            writer.Write(new byte[PadToDword(cbVal) - cbVal]);

            System.Diagnostics.Debug.Assert(cbBlock == writer.BaseStream.Position - startPos);
        }

        /// <summary>
        /// compute number of chars needed to end up on a 32-bit boundary assuming that three
        /// WORDS preceded this string.
        /// </summary>
        /// <param name="sz"></param>
        /// <returns></returns>
        private static int KEYSIZE(string sz)
        {
            return PadKeyLen((sz.Length + 1) * sizeof(WCHAR)) / sizeof(WCHAR);
        }
        private static int KEYBYTES(string sz)
        {
            return KEYSIZE(sz) * sizeof(WCHAR);
        }

        private int GetStringsSize()
        {
            return GetVerStrings().Aggregate(0, (curSum, pair) => SizeofVerString(pair.Key, pair.Value) + curSum);
        }

        internal int GetDataSize()
        {
            int sizeEXEVERRESOURCE = sizeof(WORD) * 3 * 5 + 2 * sizeof(WORD) + //five headers + two words for CP and lang
                KEYBYTES(vsVersionInfoKey) +
                KEYBYTES(varFileInfoKey) +
                KEYBYTES(translationKey) +
                KEYBYTES(stringFileInfoKey) +
                KEYBYTES(_langIdAndCodePageKey) +
                sizeVS_FIXEDFILEINFO;

            return GetStringsSize() + sizeEXEVERRESOURCE;
        }

        internal void WriteVerResource(BinaryWriter writer)
        {
            /*
                must be assumed to start on a 32-bit boundary.
             * 
             * the sub-elements of the VS_VERSIONINFO consist of a header (3 WORDS) a string
             * and then beginning on the next 32-bit boundary, the elements children
                 
                struct VS_VERSIONINFO
                {
                    WORD cbRootBlock;                                     // size of whole resource
                    WORD cbRootValue;                                     // size of VS_FIXEDFILEINFO structure
                    WORD fRootText;                                       // root is text?
                    WCHAR szRootKey[KEYSIZE("VS_VERSION_INFO")];          // Holds "VS_VERSION_INFO"
                    VS_FIXEDFILEINFO vsFixed;                             // fixed information.
                    WORD cbVarBlock;                                      // size of VarFileInfo block
                    WORD cbVarValue;                                      // always 0
                    WORD fVarText;                                        // VarFileInfo is text?
                    WCHAR szVarKey[KEYSIZE("VarFileInfo")];               // Holds "VarFileInfo"
                    WORD cbTransBlock;                                    // size of Translation block
                    WORD cbTransValue;                                    // size of Translation value
                    WORD fTransText;                                      // Translation is text?
                    WCHAR szTransKey[KEYSIZE("Translation")];             // Holds "Translation"
                    WORD langid;                                          // language id
                    WORD codepage;                                        // codepage id
                    WORD cbStringBlock;                                   // size of StringFileInfo block
                    WORD cbStringValue;                                   // always 0
                    WORD fStringText;                                     // StringFileInfo is text?
                    WCHAR szStringKey[KEYSIZE("StringFileInfo")];         // Holds "StringFileInfo"
                    WORD cbLangCpBlock;                                   // size of language/codepage block
                    WORD cbLangCpValue;                                   // always 0
                    WORD fLangCpText;                                     // LangCp is text?
                    WCHAR szLangCpKey[KEYSIZE("12345678")];               // Holds hex version of language/codepage
                    // followed by strings
                };
            */

            var debugPos = writer.BaseStream.Position;
            var dataSize = GetDataSize();

            writer.Write((WORD)dataSize);
            writer.Write((WORD)sizeVS_FIXEDFILEINFO);
            writer.Write((WORD)0);
            writer.Write(vsVersionInfoKey.ToCharArray());
            writer.Write(new byte[KEYBYTES(vsVersionInfoKey) - vsVersionInfoKey.Length * 2]);
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);
            WriteVSFixedFileInfo(writer);
            writer.Write((WORD)(sizeof(WORD) * 2 + 2 * HDRSIZE + KEYBYTES(varFileInfoKey) + KEYBYTES(translationKey)));
            writer.Write((WORD)0);
            writer.Write((WORD)1);
            writer.Write(varFileInfoKey.ToCharArray());
            writer.Write(new byte[KEYBYTES(varFileInfoKey) - varFileInfoKey.Length * 2]);   //padding
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);
            writer.Write((WORD)(sizeof(WORD) * 2 + HDRSIZE + KEYBYTES(translationKey)));
            writer.Write((WORD)(sizeof(WORD) * 2));
            writer.Write((WORD)0);
            writer.Write(translationKey.ToCharArray());
            writer.Write(new byte[KEYBYTES(translationKey) - translationKey.Length * 2]);   //padding
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);
            writer.Write((WORD)0);      //langId; MAKELANGID(LANG_NEUTRAL, SUBLANG_NEUTRAL)) = 0
            writer.Write((WORD)CP_WINUNICODE);   //codepage; 1200 = CP_WINUNICODE
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);
            writer.Write((WORD)(2 * HDRSIZE + KEYBYTES(stringFileInfoKey) + KEYBYTES(_langIdAndCodePageKey) + GetStringsSize()));
            writer.Write((WORD)0);
            writer.Write((WORD)1);
            writer.Write(stringFileInfoKey.ToCharArray());      //actually preceded by 5 WORDS so not consistent with the
            //assumptions of KEYBYTES, but equivalent.
            writer.Write(new byte[KEYBYTES(stringFileInfoKey) - stringFileInfoKey.Length * 2]); //padding. 
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);
            writer.Write((WORD)(HDRSIZE + KEYBYTES(_langIdAndCodePageKey) + GetStringsSize()));
            writer.Write((WORD)0);
            writer.Write((WORD)1);
            writer.Write(_langIdAndCodePageKey.ToCharArray());
            writer.Write(new byte[KEYBYTES(_langIdAndCodePageKey) - _langIdAndCodePageKey.Length * 2]); //padding
            System.Diagnostics.Debug.Assert((writer.BaseStream.Position & 3) == 0);

            System.Diagnostics.Debug.Assert(writer.BaseStream.Position - debugPos == dataSize - GetStringsSize());
            debugPos = writer.BaseStream.Position;

            foreach (var entry in GetVerStrings())
            {
                System.Diagnostics.Debug.Assert(entry.Value != null);
                WriteVersionString(entry, writer);
            }

            System.Diagnostics.Debug.Assert(writer.BaseStream.Position - debugPos == GetStringsSize());
        }
    }
}
