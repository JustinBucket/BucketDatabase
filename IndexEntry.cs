using System;

namespace BucketDatabase
{
    public class IndexEntry
    {
        public Guid Id { get; set; }
        public IndexEntry(Guid id)
        {
            Id = id;
        }
    }
}