using System;
using System.Collections.Generic;

namespace BucketDatabase
{
    public class QueryParameter
    {
        public Guid FileId { get; set; }
        public Guid Id { get; set; }
        public List<QueryableEntry> QueryableEntries { get; set; } = new List<QueryableEntry>();
    }
}