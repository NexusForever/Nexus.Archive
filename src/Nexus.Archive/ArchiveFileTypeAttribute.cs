using System;

namespace Nexus.Archive;

[AttributeUsage(AttributeTargets.Class)]
public class ArchiveFileTypeAttribute : Attribute
{
    public ArchiveFileTypeAttribute(ArchiveType type)
    {
        Type = type;
    }

    public ArchiveType Type { get; }
}