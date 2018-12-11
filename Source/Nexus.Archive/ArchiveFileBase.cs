using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Archive
{
    public abstract class ArchiveFileBase : IDisposable
    {
        private delegate ArchiveFileBase ArchiveFactory(string filePath, MemoryMappedFile file, ArchiveHeader header, BlockInfoHeader[] blockTable, RootIndexBlock rootBlock);
        private static readonly Dictionary<ArchiveType, ArchiveFactory> TypeHandlers =
            new Dictionary<ArchiveType, ArchiveFactory>();

        static ArchiveFileBase()
        {
            TypeHandlers.Add(ArchiveType.Index, (filePath, file, header, blockTable, rootIndexBlock) => new IndexFile(filePath, file, header, blockTable, rootIndexBlock));
            TypeHandlers.Add(ArchiveType.Archive, (filePath, file, header, blockTable, rootIndexBlock) => new ArchiveFile(filePath, file, header, blockTable, rootIndexBlock));
            //foreach (var type in typeof(ArchiveFileBase).Assembly.GetTypes()
            //    .Where(i => typeof(ArchiveFileBase).IsAssignableFrom(i) && !i.IsAbstract))
            //{
            //    var attribute = type.GetCustomAttribute<ArchiveFileTypeAttribute>();
            //    if (attribute == null) continue;
            //    var argumentTypes = new[]
            //    {
            //        typeof(string), typeof(MemoryMappedFile), typeof(ArchiveHeader), typeof(BlockInfoHeader[]),
            //        typeof(RootIndexBlock)
            //    };
            //    MemberInfo constructor = type.GetConstructor(argumentTypes);
            //    MemberInfo method = type.GetMethod("FromFile", BindingFlags.Static, null, argumentTypes, null);
            //    if (!(method is MethodInfo methodInfo) ||
            //        !typeof(ArchiveFileBase).IsAssignableFrom(methodInfo.ReturnType))
            //        method = null;
            //    if (constructor == null && method == null) continue;
            //    TypeHandlers[attribute.Type] = method ?? constructor;
            //}
        }

        protected ArchiveFileBase(string fileName, MemoryMappedFile file, ArchiveHeader header,
            BlockInfoHeader[] blockInfoHeaders, RootIndexBlock rootIndex)
        {
            FileName = fileName;
            File = file;
            Header = header;
            BlockPointers = blockInfoHeaders;
            RootIndex = rootIndex;
        }

        public string FileName { get; }
        protected MemoryMappedFile File { get; }
        public ArchiveHeader Header { get; }
        public BlockInfoHeader[] BlockPointers { get; }
        public RootIndexBlock RootIndex { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static RootIndexBlock ReadRootBlock(MemoryMappedFile file, BlockInfoHeader rootBlockInfo)
        {
            using (var reader = new BinaryReader(GetBlockView(rootBlockInfo, file)))
            {
                return RootIndexBlock.FromReader(reader);
            }
        }

        internal BinaryReader GetBlockReader(int index, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            return new BinaryReader(GetBlockView(index), encoding, false);
        }

        internal Stream GetBlockView(int index)
        {
            return GetBlockView(BlockPointers[index]);
        }

        private static Stream GetBlockView(BlockInfoHeader blockInfo, MemoryMappedFile file)
        {
            if (blockInfo.Size == 0) return null;
            return file.CreateViewStream((long)blockInfo.Offset, (long)blockInfo.Size, MemoryMappedFileAccess.Read);
        }

        protected Stream GetBlockView(BlockInfoHeader blockInfo)
        {
            return GetBlockView(blockInfo, File);
        }

        /// <summary>
        /// </summary>
        /// <returns>Index block number</returns>
        private static (int rootDescriptorIndex, BlockInfoHeader[] blockPointers) ReadBlockPointers(
            MemoryMappedFile file, ArchiveHeader header)
        {
            var blockPointers = new BlockInfoHeader[header.DataHeader.BlockCount];
            var startPosition = header.DataHeader.BlockTableOffset;
            var length = header.DataHeader.BlockCount * Marshal.SizeOf<BlockInfoHeader>();
            var archiveDescriptorIndex = -1;
            using (var reader = new BinaryReader(file.CreateViewStream((long)startPosition, length, MemoryMappedFileAccess.Read)))
            {
                for (var x = 0; x < header.DataHeader.BlockCount; x++)
                {
                    blockPointers[x] = BlockInfoHeader.FromReader(reader);
                    if (blockPointers[x].Size == 16)
                        archiveDescriptorIndex = x;
                }
            }

            if (archiveDescriptorIndex < 0)
                throw new InvalidDataException("No root block found (AIDX or AARC)! This file appears to be corrupt!");
            return (archiveDescriptorIndex, blockPointers);
        }


        private static ArchiveHeader ReadHeader(MemoryMappedFile file)
        {
            var length = Marshal.SizeOf<ArchiveHeader>();
            using (var stream = file.CreateViewStream(0, length, MemoryMappedFileAccess.Read))
            {
                return ArchiveHeader.ReadFrom(stream);
            }
        }

        private static MemoryMappedFile OpenFile(string fileName)
        {
            return MemoryMappedFile.CreateFromFile(System.IO.File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), null, 0, MemoryMappedFileAccess.Read, HandleInheritability.Inheritable, false);
        }

        public static ArchiveFileBase FromFile(string fileName)
        {
            var file = OpenFile(fileName); //MemoryMappedFile.CreateFromFile(fileName, FileMode.Open);
            try
            {
                var header = ReadHeader(file);
                var blockPointerInfo = ReadBlockPointers(file, header);
                var rootBlock = ReadRootBlock(file,
                    blockPointerInfo.blockPointers[blockPointerInfo.rootDescriptorIndex]);
                if (!TypeHandlers.TryGetValue(rootBlock.ArchiveType, out var creator))
                    throw new InvalidOperationException($"Unknown archive type: {rootBlock.ArchiveType:G}");
                return creator(fileName, file, header, blockPointerInfo.blockPointers, rootBlock);
            }
            catch
            {
                try
                {
                    file.Dispose();
                }
                catch
                {
                    // Ignored.
                }

                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) File?.Dispose();
        }
    }
}