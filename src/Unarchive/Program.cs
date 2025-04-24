using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Humanizer;
using Nexus.Archive;

namespace Unarchive;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var patchPath = Path.GetFullPath(args[0]);
        var coreDataArchivePath = Path.Combine(patchPath, "CoreData.archive");
        ArchiveFile coreDataArchive = File.Exists(coreDataArchivePath)
            ? (ArchiveFile)ArchiveFileBase.FromFile(coreDataArchivePath)
            : null;
        var unpackBasePath = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(patchPath, "..", "Data"));
        var archives = Directory.EnumerateFiles(patchPath, "*.index")
            .Select(i => Archive.FromFile(i, coreDataArchive))
            .Where(i => i.HasArchiveFile).ToArray();
        await Extract(archives, unpackBasePath, true);
    }

    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    private const int Kilobyte = 1024;
    private const int Megabyte = 1024 * Kilobyte;
    private const int BufferSize = 8 * Megabyte;

    private static async Task ExtractArchiveFileAsync(IArchiveFileEntry file,
        string unpackBasePath,
        bool decompress)
    {
        var outputFileName = Path.Combine(unpackBasePath, file.FileName);
            

        await using (var targetStream =
                     File.Open(outputFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        await using (var archiveStream = file.Archive.OpenFileStream(file, decompress))
        {
            await archiveStream.CopyToAsync(targetStream, BufferSize);
        }

        File.SetLastAccessTime(outputFileName, file.WriteTime.LocalDateTime);
        File.SetLastWriteTime(outputFileName, file.WriteTime.LocalDateTime);
        File.SetCreationTime(outputFileName, file.WriteTime.LocalDateTime);
        await WriteLineAsync($"{outputFileName} - {file.UncompressedSize.Bytes().Humanize()}");
    }

    private static async Task WriteLineAsync(string message, params object[] args)
    {
        await Semaphore.WaitAsync();
        try
        {
            Console.WriteLine(message, args);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static async Task Extract(IEnumerable<Archive> archives,
        string unpackBasePath, bool decompress)
    {
        Directory.CreateDirectory(unpackBasePath);
        var workerResult = await StartWorkers(archives.ToImmutableList(), unpackBasePath, decompress, 64);
        await WriteLineAsync($"Done. Processed {workerResult}");
    }

    private record WorkerResult(int Archives, int Folders, int Files)
    {
        public static WorkerResult Add(WorkerResult left, WorkerResult right)
        {
            return new WorkerResult(
                left.Archives + right.Archives,
                left.Folders + right.Folders,
                left.Files + right.Files
            );
        }

        public static WorkerResult Zero { get; } = new WorkerResult(0, 0, 0);

        public override string ToString()
        {
            var archiveSuffix = Archives == 1 ? "" : "s";
            var folderSuffix = Folders == 1 ? "" : "s";
            var fileSuffix = Files == 1 ? "" : "s";
            return $"{Archives} archive{archiveSuffix}, {Folders} folder{folderSuffix}, {Files} file{fileSuffix}";
        }
    }

    private static async Task<WorkerResult> StartWorkers(IList<Archive> archives, string unpackBasePath,
        bool decompress, int parallelism = 64)
    {
        var pendingFileChannel = Channel.CreateUnbounded<(string, IArchiveFileEntry)>(
            new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false
            });
        var pendingFolderChannel = Channel.CreateUnbounded<(string, IArchiveFolderEntry)>(
            new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = false
            });


        var folderWorkerCount = Math.Max(1, parallelism / 4);
        var fileWorkerCount = Math.Max(1, parallelism - folderWorkerCount);
        var totalWorkerCount = folderWorkerCount + fileWorkerCount + archives.Count;

        await WriteLineAsync($"Starting {archives.Count} archive workers.");
        await WriteLineAsync($"Starting {fileWorkerCount} file workers");
        await WriteLineAsync($"Starting {folderWorkerCount} folder workers");
        var fileWorkerTask = StartFileWorkers(decompress, pendingFileChannel.Reader, fileWorkerCount);
        await WriteLineAsync("Started file workers");
        var folderWorkerTask =
            StartFolderWorkers(pendingFolderChannel.Reader, pendingFileChannel.Writer, folderWorkerCount);
        await WriteLineAsync("Started folder workers");
        var archiveWorkerTask = StartArchiveWorkers(archives, unpackBasePath, pendingFolderChannel.Writer);
        await WriteLineAsync("Started archive workers");
        var combinedTask = Task.WhenAll(
            archiveWorkerTask,
            folderWorkerTask,
            fileWorkerTask
        );
        await WriteLineAsync($"Started {totalWorkerCount} workers. Waiting for them to finish.");
        var rawResults = await combinedTask.ConfigureAwait(false);
        var results = rawResults.Aggregate(WorkerResult.Zero, WorkerResult.Add);
        await WriteLineAsync("All workers have completed.");
        return results;
    }


    private static async Task<WorkerResult> StartFileWorkers(bool decompress,
        ChannelReader<(string, IArchiveFileEntry)> pendingFileChannel, int count = 32)
    {
        var workers = Enumerable.Range(0, count)
            .Select(_ => Task.Run(async () => await ExtractFilesWorker(pendingFileChannel, decompress)))
            .ToImmutableList();

        var rawResults = await workers;
        var results = rawResults.Aggregate(WorkerResult.Zero, WorkerResult.Add);
        await WriteLineAsync("Done processing {0} file(s)", results.Files);
        return results;
    }

    private static async Task<WorkerResult> StartFolderWorkers(
        ChannelReader<(string, IArchiveFolderEntry)> pendingFolderChannel,
        ChannelWriter<(string, IArchiveFileEntry)> pendingFileChannel,
        int count = 32
    )
    {
        var tasks = Enumerable.Range(0, count)
            .Select(_ => Task.Run(async () => await FolderScannerWorker(pendingFolderChannel, pendingFileChannel)))
            .ToImmutableList();

        return await WaitForFolderWorkers(tasks, pendingFileChannel);
    }

    private static async Task<WorkerResult> WaitForFolderWorkers(IList<Task<WorkerResult>> tasks,
        ChannelWriter<(string, IArchiveFileEntry)> writer)
    {
        var results = (await tasks).Aggregate(WorkerResult.Zero, WorkerResult.Add);
        writer.Complete();
        await WriteLineAsync("Done processing {0} folder(s), ", results.Folders);
        return results;
    }

    private static async Task<WorkerResult> StartArchiveWorkers(IEnumerable<Archive> archives,
        string unpackBasePath,
        ChannelWriter<(string, IArchiveFolderEntry)> pendingFolderChannel)
    {
        var result = await ScanArchiveWorker(pendingFolderChannel, unpackBasePath, archives.ToImmutableList());
        pendingFolderChannel.Complete();
        await WriteLineAsync("Done processing {0} archives(s), ", result.Archives);
        return result;
    }

    private static async Task<WorkerResult> FolderScannerWorker(
        ChannelReader<(string, IArchiveFolderEntry)> channel,
        ChannelWriter<(string, IArchiveFileEntry)> pendingFileChannel
    )
    {
        try
        {
            await Task.Yield();
            var folderCount = 0;
            await foreach (var (basePath, folder) in channel.ReadAllAsync())
            {
                await WriteLineAsync($"Scanning folder {folder.Archive.Name}:{folder.Path}");

                folderCount++;
                var fileCount = 0;
                foreach (var file in folder.EnumerateFiles())
                {
                    fileCount++;
                    await pendingFileChannel.WriteAsync((basePath, file));
                }

                var suffix = fileCount == 1 ? "" : "s";
                await WriteLineAsync($"Done scanning folder {folder.Path}, found {fileCount} file{suffix}");
            }

            return new WorkerResult(0, folderCount, 0);
        }
        catch (Exception ex)
        {
            await WriteLineAsync(ex.ToString());
            throw;
        }
    }

    private static async Task<WorkerResult> ScanArchiveWorker(
        ChannelWriter<(string, IArchiveFolderEntry)> channel,
        string basePath,
        IList<Archive> archives)
    {
        await Task.Yield();
        if (!Directory.Exists(basePath))
        {
            await WriteLineAsync($"Creating directory {basePath}");
            Directory.CreateDirectory(basePath!);
        }
        foreach (var archive in archives)
        {
            await WriteLineAsync($"Enqueue -> {archive.Name}:$ROOT");
            await channel.WriteAsync((basePath, archive.IndexFile.RootFolder));
            foreach (var folder in archive.IndexFile.RootFolder.EnumerateFolders(true))
            {
                var diskFolder = Path.Combine(basePath, folder.FileName);
                if (!Directory.Exists(diskFolder))
                {
                    await WriteLineAsync($"Creating directory {basePath}");
                    Directory.CreateDirectory(diskFolder);
                }
                    
                await channel.WriteAsync((diskFolder, folder));
                await WriteLineAsync($"Enqueue -> {archive.Name}:{folder.Path}");
                    
            }
        }
        return new WorkerResult(archives.Count, 0, 0);
    }

    private static async Task<WorkerResult> ExtractFilesWorker(
        ChannelReader<(string, IArchiveFileEntry)> channel,
        bool decompress)
    {
        await Task.Yield();
        int fileCount = 0;
        await foreach (var (basePath, fileEntry) in channel.ReadAllAsync())
        {
            await ExtractArchiveFileAsync(fileEntry, basePath, decompress);
            fileCount++;
        }

        return new WorkerResult(0, 0, fileCount);
    }
}