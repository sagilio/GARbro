//! \file       ImageBPD.cs
//! \date       2019 Mar 21
//! \brief      PineSoft image format.
//
// Copyright (C) 2019 by morkt
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

namespace GameRes.Formats.PineSoft
{
    [Export(typeof(ImageFormat))]
    public class BpdFormat : ImageFormat
    {
        public override string         Tag { get { return "BPD"; } }
        public override string Description { get { return "PineSoft image format"; } }
        public override uint     Signature { get { return 0x445042; } } // 'BPD'

        public override ImageMetaData ReadMetaData (IBinaryStream file)
        {
            var header = file.ReadHeader (8);
            return new ImageMetaData {
                Width  = header.ToUInt16 (4),
                Height = header.ToUInt16 (6),
                BPP = 32,
            };
        }

        public override ImageData Read (IBinaryStream file, ImageMetaData info)
        {
            var pixels = new byte[info.iWidth * info.iHeight * 4];
            file.Position = 8;
            file.Read (pixels, 0, pixels.Length);
            return ImageData.Create (info, PixelFormats.Bgra32, null, pixels);
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException ("BpdFormat.Write not implemented");
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
}
