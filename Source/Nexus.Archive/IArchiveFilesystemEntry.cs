namespace Nexus.Archive
{
    public interface IArchiveFilesystemEntry
    {
        string Path { get; }
        string FileName { get; }
        string FolderPath { get; }
    }
}