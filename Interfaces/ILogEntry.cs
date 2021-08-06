using System;

namespace BucketDatabase.Interfaces
{
    public interface ILogEntry
    {
        // FileId is used to find the file
        Guid FileId { get; set; }
        // Id is used to find the object in the file
        Guid Id { get; set; }
    }
}