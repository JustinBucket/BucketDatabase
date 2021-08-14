using System;
using System.Collections.Generic;

namespace BucketDatabase.Query
{
    public class QueryParameter
    {
        // would be nice to have and/or capabilities
        // let's do it globally first
        public Guid FileId { get; set; }
        public Guid Id { get; set; }
        public List<QueryableEntry> QueryableEntries { get; set; } = new List<QueryableEntry>();
    }
}