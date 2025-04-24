using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Archive.Patcher
{
    public class Patcher
    {
        private const int FileRetryCount = 5;
        IPatchWriter PatchWriter { get; }
        private IPatchSource PatchSource { get; }
        public event EventHandler<ProgressUpdateEventArgs> Progress;
        public Patcher(IPatchSource patchSource, IndexFile index, ArchiveFile coreData = null, string target = null)
        {
            PatchSource = patchSource;
            Index = index;
            if (target == null)
            {
                target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(index.FileName), "..",
                    Path.GetFileNameWithoutExtension(index.FileName)));
            }

            if (Path.GetExtension(target).TrimStart('.').ToLower() != "archive")
            {
                PatchWriter = new FolderPatchWriter(target, coreData);
            }
            else
            {
                // TODO: Implement actual archive writing.
                throw new InvalidOperationException();
            }
            PatchWriter.ProgressUpdated += PatchWriter_ProgressUpdated;
        }

        private void PatchWriter_ProgressUpdated(object sender, ProgressUpdateEventArgs e)
        {
            OnProgress(e);
        }

        public IndexFile Index { get; set; }

        public async Task PatchSingleItem(IArchiveFileEntry file, CancellationToken cancellationToken)
        {
            OnProgress(new ProgressUpdateEventArgs(file, 0, 0));
            if (await PatchWriter.Exists(file).ConfigureAwait(false))
                return;
            int counter = 0;
            Stream fileData = null;
            List<Exception> exceptions = new List<Exception>();
            do
            {
                try
                {
                    fileData = await PatchSource.DownloadHashAsync((int)Index.RootIndex.BuildNumber, file.Hash,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    counter++;
                    exceptions.Add(ex);
                }
            } while (fileData == null && counter < FileRetryCount);

            if (fileData == null)
                throw new AggregateException(exceptions);

            counter = 0;
            do
            {
                try
                {
                    await PatchWriter.AppendAsync(fileData, file).ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    counter++;
                }
            } while (counter < FileRetryCount);

            if (counter >= FileRetryCount)
                throw new AggregateException(exceptions);
        }
        public async Task Patch(CancellationToken cancellationToken)
        {
            if (!PatchWriter.IsThreadSafe)
            {
                foreach (var file in Index.GetFiles())
                {
                    await PatchSingleItem(file, cancellationToken);
                }
            }
            else
            {
                Parallel.ForEach(Index.GetFiles(), new ParallelOptions() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 4 }, i => PatchSingleItem(i, cancellationToken).GetAwaiter().GetResult());
            }
        }

        protected virtual void OnProgress(ProgressUpdateEventArgs e)
        {
            Progress?.Invoke(this, e);
        }
    }
}
