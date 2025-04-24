using Nexus.Archive;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TableExtractor
{
    class Program
    {
        private static readonly string[] IndexFiles = new[]
        {
            "ClientData.index",
            "ClientDataEN.index",
            "ClientDataFR.index",
            "ClientDataDE.index"
        };

        private const string TableFolder = "DB";

        private static readonly string[] LanguageFiles = new[]
        {
            "en-US.bin",
            "de-DE.bin",
            "fr-FR.bin",
            "en-GB.bin"
        };

        private static void ExtractFile(Archive archive, IArchiveFileEntry file, string target, bool validate = true)
        {
            if (file == null)
            {
                Console.WriteLine("Skipping file: {0}, file as not found in {1}", Path.GetFileName(target),
                    Path.GetFileName(archive.IndexFile.FileName));
                return;
            }

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

            using (var stream = archive.OpenFileStream(file))
            using (var targetStream = File.Open(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(targetStream, (length, progress) => WriteProgress(length, progress));
            }

            ClearProgress();

            var fileInfo = new FileInfo(target);
            Debug.Assert(fileInfo.Length == file.UncompressedSize);

            // This only works on uncompressed data.
            // For compressed data, it's the hash of the compressed data in the archive.
            //if (validate)
            //{
            //    using (var stream = File.Open(target, FileMode.Open, FileAccess.Read, FileShare.None))
            //    using (var sha1 = SHA1.Create())
            //    {
            //        var hash = sha1.ComputeHash(stream);
            //        Debug.Assert(hash.SequenceEqual(file.Hash));
            //    }
            //}

            //Console.WriteLine("EXTRACTED: {0}", Path.GetFileName(target));
        }

        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("TableExtractor.exe PatchFolderPath <output directory>");
                return;
            }

            var outputPath = "tbl";
            if (args.Length == 2)
            {
                outputPath = args[1];
            }

            var coreDataArchivePath = Path.Combine(args[0], "CoreData.archive");
            ArchiveFile coreDataArchive = null;
            if (File.Exists(coreDataArchivePath))
                coreDataArchive = ArchiveFileBase.FromFile(Path.Combine(args[0], "CoreData.archive")) as ArchiveFile;
            outputPath = Directory.CreateDirectory(outputPath).FullName;

            Console.WriteLine("Extracting files from index files in {0} to target path {1}", args[0], outputPath);

            foreach (var index in IndexFiles.Select(i => Path.Combine(args[0], i)))
            {
                if (!File.Exists(index))
                {
                    Console.WriteLine("Skipping missing file {0}", index);
                    continue;
                }

                try
                {
                    var archive = Archive.FromFile(index, coreDataArchive);
                    ProcessArchive(archive, outputPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error processing {0}", index);
                    Console.WriteLine(ex);
                }
            }
        }

        private static void ProcessArchive(Archive archive, string outputPath)
        {
            foreach (var language in LanguageFiles.Select(archive.IndexFile.FindEntry).OfType<IArchiveFileEntry>())
            {
                if (language == null) continue;
                ExtractFile(archive, language, Path.Combine(outputPath, language.FileName));
            }

            var databaseFolder = archive.IndexFile.FindEntry(TableFolder) as IArchiveFolderEntry;
            if (databaseFolder == null) return;
            //Parallel.ForEach(databaseFolder.EnumerateFiles(true),
            //    tblFile =>
            //    {
            //        ExtractFile(archive, tblFile, Path.Combine(outputPath, tblFile.FileName));
            //    });
            foreach (var tblFile in databaseFolder.EnumerateFiles(true))
            {
                ExtractFile(archive, tblFile, Path.Combine(outputPath, Path.GetFileName(tblFile.Path)));
            }
        }

    }
}
