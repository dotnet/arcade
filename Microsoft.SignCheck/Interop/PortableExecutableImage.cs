using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Interop
{
    public class PortableExecutableImage
    {
        // See winnt.h
        public const UInt16 IMAGE_DOS_SIGNATURE = 0x5a4d; // MZ
        private const UInt32 IMAGE_NT_SIGNATURE = 0x00004550; // PE00

        private IMAGE_DOS_HEADER _imageDOSHeader;
        private IMAGE_NT_HEADERS _imageNTHeaders;
        private List<IMAGE_SECTION_HEADER> _imageSectionHeaders;

        public UInt32 FirstSectionHeaderOffset
        {
            get;
            private set;
        }

        public UInt32 ImageNTHeadersOffset
        {
            get
            {
                return _imageDOSHeader.e_lfanew;
            }
        }

        public ICollection<IMAGE_SECTION_HEADER> SectionHeaders
        {
            get
            {
                if (_imageSectionHeaders == null)
                {
                    _imageSectionHeaders = new List<IMAGE_SECTION_HEADER>();
                }

                return _imageSectionHeaders;
            }
        }

        public bool IsValidDOSHeader
        {
            get
            {
                return _imageDOSHeader.e_magic == IMAGE_DOS_SIGNATURE;
            }
        }

        public bool IsValidNTHeader
        {
            get
            {
                return _imageNTHeaders.Signature == IMAGE_NT_SIGNATURE;
            }
        }

        public UInt16 NumberOfSections
        {
            get
            {
                return _imageNTHeaders.FileHeader.NumberOfSections;
            }
        }

        public PortableExecutableImage(string path)
        {
            Init(path);
        }

        private void Init(string path)
        {
            using (var reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                ReadImageDosHeader(reader);
                ReadImageNTHeaders(reader);
                ReadImageSectionHeaders(reader);
            }
        }

        private void ReadImageDosHeader(BinaryReader reader)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            _imageDOSHeader = new IMAGE_DOS_HEADER
            {
                e_magic = reader.ReadUInt16(),
                e_cblp = reader.ReadUInt16(),
                e_cp = reader.ReadUInt16(),
                e_crlc = reader.ReadUInt16(),
                e_cparhdr = reader.ReadUInt16(),
                e_minalloc = reader.ReadUInt16(),
                e_maxalloc = reader.ReadUInt16(),
                e_ss = reader.ReadUInt16(),
                e_sp = reader.ReadUInt16(),
                e_csum = reader.ReadUInt16(),
                e_ip = reader.ReadUInt16(),
                e_cs = reader.ReadUInt16(),
                elfarlc = reader.ReadUInt16(),
                e_ovno = reader.ReadUInt16(),
                e_res = new UInt16[] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() },
                e_oemid = reader.ReadUInt16(),
                e_oeminfo = reader.ReadUInt16(),
                e_res2 = new UInt16[] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() },
                e_lfanew = reader.ReadUInt32(),
            };

            if (!IsValidDOSHeader)
            {
                throw new InvalidDataException();
            }
        }

        private void ReadImageNTHeaders(BinaryReader reader)
        {
            reader.BaseStream.Seek(ImageNTHeadersOffset, SeekOrigin.Begin);

            _imageNTHeaders.Signature = reader.ReadUInt32();
            _imageNTHeaders.FileHeader.Machine = reader.ReadUInt16();
            _imageNTHeaders.FileHeader.NumberOfSections = reader.ReadUInt16();
            _imageNTHeaders.FileHeader.TimeDateStamp = reader.ReadUInt32();
            _imageNTHeaders.FileHeader.PointerToSymbolTable = reader.ReadUInt32();
            _imageNTHeaders.FileHeader.NumberOfSymbols = reader.ReadUInt32();
            _imageNTHeaders.FileHeader.SizeOfOptionalHeader = reader.ReadUInt16();
            _imageNTHeaders.FileHeader.Characteristics = reader.ReadUInt16();

            if (!IsValidNTHeader)
            {
                throw new InvalidDataException();
            }

            FirstSectionHeaderOffset = ImageNTHeadersOffset + (UInt32)Marshal.SizeOf(_imageNTHeaders) + _imageNTHeaders.FileHeader.SizeOfOptionalHeader;
        }

        private void ReadImageSectionHeaders(BinaryReader reader)
        {
            var sections = new List<IMAGE_SECTION_HEADER>();
            for (int i = 0; i < NumberOfSections; i++)
            {
                reader.BaseStream.Seek(FirstSectionHeaderOffset + (i * Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER))), SeekOrigin.Begin);

                SectionHeaders.Add(ReadImageSectionHeader(reader));
            }
        }

        private IMAGE_SECTION_HEADER ReadImageSectionHeader(BinaryReader reader)
        {
            IMAGE_SECTION_HEADER section = new IMAGE_SECTION_HEADER
            {
                Name = reader.ReadChars(8)
            };

            return section;
        }
    }
}
