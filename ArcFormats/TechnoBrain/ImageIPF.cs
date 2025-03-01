//! \file       ImageIPF.cs
//! \date       Sat Jan 14 13:26:13 2017
//! \brief      TechnoBrain's "Inteligent Picture Format"
//
// Copyright (C) 2017 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameRes.Utility;

namespace GameRes.Formats.TechnoBrain
{
    internal class IpfMetaData : ImageMetaData
    {
        public bool HasPalette;
        public bool IsCompressed;
        public long PalOffset;
        public int  PalSize;
        public long BmpOffset;
    }

    [Export(typeof(ImageFormat))]
    public class IpfFormat : ImageFormat
    {
        public override string         Tag { get { return "IPF"; } }
        public override string Description { get { return "TechnoBrain's 'Inteligent Picture Format'"; } }
        public override uint     Signature { get { return 0; } } // 'RIFF'

        public override ImageMetaData ReadMetaData (IBinaryStream file)
        {
            // 'RIFF' isn't included into signature to avoid auto-detection of the WAV files as IPF images.
            if (0x46464952 != file.Signature) // 'RIFF'
                return null;
            var header = file.ReadHeader (0x14);
            if (!header.AsciiEqual (8, "IPF fmt "))
                return null;
            int fmt_size = header.ToInt32 (0x10);
            if (fmt_size < 0x24)
                return null;
            header = file.ReadHeader (0x14 + fmt_size);
            bool has_palette = header.ToInt32 (0x18) != 0;
            bool has_bitmap = header.ToInt32 (0x28) != 0;
            if (!has_bitmap)
                return null;
            var info = new IpfMetaData { BPP = 8, HasPalette = has_palette };
            if (has_palette)
            {
                if (0x206C6170 != file.ReadInt32()) // 'pal '
                    return null;
                info.PalSize = file.ReadInt32();
                if (info.PalSize < 0x24)
                    return null;
                info.PalOffset = file.Position;
                file.Position = info.PalOffset + info.PalSize;
            }
            if (0x20706D62 != file.ReadInt32()) // 'bmp '
                return null;
            int bmp_size = file.ReadInt32();
            if (bmp_size <= 0x20)
                return null;
            info.BmpOffset = file.Position + 0x18;
            info.Width  = file.ReadUInt16();
            info.Height = file.ReadUInt16();
            file.Seek (0xE, SeekOrigin.Current);
            info.IsCompressed = 0 != (file.ReadByte() & 1);
            return info;
        }

        public override ImageData Read (IBinaryStream file, ImageMetaData info)
        {
            var reader = new IpfReader (file, (IpfMetaData)info);
            var pixels = reader.Unpack();
            return ImageData.Create (info, reader.Format, reader.Palette, pixels);
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException ("IpfFormat.Write not implemented");
        }

        public override ImageData ReadAndExport(IBinaryStream file, ImageMetaData info, Stream exportFile)
        {
            throw new System.NotImplementedException();
        }

        public override void Pack(Stream file, IBinaryStream inputFile, ImageData bitmap)
        {
            throw new System.NotImplementedException();
        }
    }

    internal sealed class IpfReader
    {
        IBinaryStream   m_input;
        IpfMetaData     m_info;
        byte[]          m_output;

        public BitmapPalette Palette { get; private set; }
        public PixelFormat    Format { get; private set; }
        public byte[]           Data { get { return m_output; } }

        public IpfReader (IBinaryStream input, IpfMetaData info)
        {
            m_input = input;
            m_info = info;
            m_output = new byte[m_info.Width*m_info.Height];
            Format = m_info.HasPalette ? PixelFormats.Indexed8 : PixelFormats.Gray8;
        }

        public byte[] Unpack ()
        {
            if (m_info.HasPalette)
            {
                UnpackPalette();
            }
            m_input.Position = m_info.BmpOffset;
            if (m_info.IsCompressed)
                UnpackBitmap();
            else
                m_input.Read (m_output, 0, m_output.Length);

            return m_output;
        }

        void UnpackPalette ()
        {
            m_input.Position = m_info.PalOffset+4;
            var header = m_input.ReadBytes (0x20);
            var data = m_input.ReadBytes (m_info.PalSize - 0x24);

            const int min_index = 0x0A;
            const int max_index = 0xF6;

            var color_map = new Color[0x100];
            int src = 0;

            for (int i = 0; i < 0x20; ++i)
            {
                var bits = header[i];
                for (int j = 0; j < 8; ++j)
                {
                    int dst = (i << 3) + j;
                    if (dst >= 0x0A && 0 != (bits & 0x80))
                    {
                        if (dst >= min_index && dst <= max_index)
                        {
                            color_map[dst] = Color.FromRgb (data[src], data[src+1], data[src+2]);
                        }
                        src += 3;
                    }
                    bits <<= 1;
                }
            }
            Palette = new BitmapPalette (color_map);
        }

        void UnpackBitmap () // IPF_12
        {
            int dst = 0;
            while (dst < m_output.Length)
            {
                byte ctl = m_input.ReadUInt8();
                if (0xF == ctl)
                {
                    break;
                }
                else if (0xE == ctl)
                {
                    m_output[dst++] = ctl;
                }
                else if (ctl < 0x10)
                {
                    int count = (ctl << 8 | m_input.ReadUInt8()) + 1;
                    byte b = m_input.ReadUInt8();
                    for (int i = 0; i < count; ++i)
                        m_output[dst++] = b;
                }
                else if (ctl < 0x20)
                {
                    int offset = ((ctl - 0x10) << 8 | m_input.ReadUInt8()) + 1;
                    int count = m_input.ReadUInt8() + 1;
                    Binary.CopyOverlapped (m_output, dst - offset, dst, count);
                    dst += count;
                }
                else
                {
                    m_output[dst++] = (byte)(ctl - 0x10);
                }
            }
        }
    }
}
