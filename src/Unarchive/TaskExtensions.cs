using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Unarchive;

public static class TaskExtensions
{
    public static TaskAwaiter<T[]> GetAwaiter<T>(this IList<Task<T>> task)
    {
        if (task.Count == 0)
        {
            return Task.FromResult(Array.Empty<T>()).GetAwaiter();
        }

        return Task.WhenAll(task).GetAwaiter();
    }
}