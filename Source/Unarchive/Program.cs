using System;
using System.IO;
using System.Linq;
using Nexus.Archive;

namespace Unarchive
{
    class Program
    {
        static void Main(string[] args)
        {
            var patchPath = Path.GetFullPath(args[0]);
            var unpackBasePath = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(patchPath, "..", "Data"));
            var archives = Directory.EnumerateFiles(patchPath, "*.index").Select(Archive.FromFile)
                .Where(i => i.HasArchiveFile).ToArray();

            foreach (var archive in archives)
            {
                ExtractArchive(archive, unpackBasePath, true);
            }

        }

        private static void ExtractArchiveDirectory(IArchiveFolderEntry folder, Archive archive, string unpackBasePath, bool decompress)
        {
            unpackBasePath = Directory.CreateDirectory(unpackBasePath).FullName;
            foreach (var file in folder.EnumerateFiles())
            {
                void WriteProgress(long length, long progress)
                {
                    Console.CursorLeft = 0;
                    Console.Write("{0} ", file.FileName);
                    Console.Write(progress);
                    if (length > 0)
                    {
                        Console.Write("/");
                        Console.Write(length);
                    }
                }

                void ClearProgress()
                {
                    Console.CursorLeft = 0;
                    Console.Write("{0} DONE                         ", file.FileName);
                    Console.WriteLine();
                }
                using (var targetStream = File.Open(Path.Combine(unpackBasePath, file.FileName), FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var archiveStream = archive.OpenFileStream(file, decompress))
                {
                    archiveStream.CopyTo(targetStream, 65535, WriteProgress);
                }

                ClearProgress();
            }

            foreach (var innerFolder in folder.EnumerateFolders())
            {
                Console.WriteLine($"Processing folder {innerFolder}");
                var diskFolder = Path.Combine(unpackBasePath, innerFolder.FileName);
                ExtractArchiveDirectory(innerFolder, archive, diskFolder, decompress);
                Console.WriteLine($"Done processing folder {innerFolder}");
            }
        }

        private static void ExtractArchive(Archive archive, string unpackBasePath, bool decompress = false)
        {
            Console.WriteLine($"Extracting {Path.GetFileName(archive.IndexFile.FileName)} to {unpackBasePath}");
            ExtractArchiveDirectory(archive.IndexFile.RootFolder, archive, unpackBasePath, decompress);
        }
    }
}
