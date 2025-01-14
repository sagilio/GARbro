//! \file       ArcPCK.cs
//! \date       Fri Feb 05 16:01:26 2016
//! \brief      Circus resource archive.
//
// Copyright (C) 2016 by morkt
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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using ICSharpCode.SharpZipLib;

namespace GameRes.Formats.Circus
{
    [Export(typeof(ArchiveFormat))]
    public class PckOpener : ArchiveFormat
    {
        public override string         Tag { get { return "PCK/CIRCUS"; } }
        public override string Description { get { return "Circus resource archive"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return true; } }

        public PckOpener ()
        {
            Extensions = new string[] { "pck", "dat" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            int count = file.View.ReadInt32 (0);
            if (!IsSaneCount (count))
                return null;
            int index_size = count * 0x48 + 4;
            uint first_offset = file.View.ReadUInt32 (4);
            if (first_offset < index_size || first_offset >= file.MaxOffset)
                return null;
            int index_offset = 4 + count * 8;
            file.View.Reserve (index_offset, (uint)count * 0x40);
            if (first_offset != file.View.ReadUInt32 (index_offset+0x38))
                return null;

            var dir = new List<Entry> (count);
            for (int i = 0; i < count; ++i)
            {
                var name = file.View.ReadString (index_offset, 0x38);
                if (0 == name.Length)
                    return null;
                var entry = FormatCatalog.Instance.Create<Entry> (name);
                entry.Offset = file.View.ReadUInt32 (index_offset+0x38);
                entry.Size   = file.View.ReadUInt32 (index_offset+0x3C);
                if (!entry.CheckPlacement (file.MaxOffset))
                    return null;
                dir.Add (entry);
                index_offset += 0x40;
            }
            return new ArcFile (file, this, dir);
        }

        public override void Create(Stream file, IEnumerable<Entry> list, ResourceOptions options = null, EntryCallback callback = null)
        {
            var entries = list.ToArray();

            var count = entries.Length;

            if (!IsSaneCount(count))
            {
                throw new ValueOutOfRangeException("the count of entries is too long");
            }

            int index_offset = 4 + count * 8;
            int index_size = 4 + count * 0x48;
            var firstOffset = (index_size / 2048 + 1 ) * 2048;

            uint nextOffset = 0;

            using (var binaryWriter = new BinaryWriter(file))
            {
                binaryWriter.Write(count);

                for (var index = 0; index < count; index++)
                {
                    var entry = entries[index];

                    var indexAtHeaderPosition = 4 + index * 8;
                    var indexAtMapPosition = index_offset + index * 0x40;

                    uint offset;
                    if (index == 0)
                    {
                        offset = (uint)firstOffset;
                    }
                    else
                    {
                        offset = nextOffset;
                    }

                    binaryWriter.Seek(indexAtHeaderPosition, SeekOrigin.Begin);
                    binaryWriter.Write(offset);
                    binaryWriter.Write(entry.Size);

                    binaryWriter.Seek(indexAtMapPosition, SeekOrigin.Begin);
                    binaryWriter.Write(entry.Name.ToCharArray());
                    binaryWriter.Seek(indexAtMapPosition + 0x38, SeekOrigin.Begin);
                    binaryWriter.Write(offset);
                    binaryWriter.Write(entry.Size);

                    binaryWriter.Seek((int)offset, SeekOrigin.Begin);
                    var fileAllBytes = File.ReadAllBytes(entry.Name);
                    binaryWriter.Write(fileAllBytes);

                    nextOffset = offset + (entry.Size / 2048 + 1) * 2048;
                }

                binaryWriter.Seek((int) nextOffset - 4, SeekOrigin.Begin);
                binaryWriter.Write(0);
            }
        }
    }
}
