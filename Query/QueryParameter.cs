using System;
using System.Collections.Generic;

namespace BucketDatabase.Query
{
    public class QueryParameter
    {
        public Guid Id { get; set; }
        public IList<QueryableEntry> QueryableEntries { get; set; } = new List<QueryableEntry>();
        public QueryParameter() { }
        public QueryParameter(Guid id, IList<QueryableEntry> entries)
        {
            Id = id;
            QueryableEntries = entries;
        }
        public QueryParameter(Guid id)
        {
            Id = id;
        }
        public QueryParameter(IList<QueryableEntry> entries)
        {
            QueryableEntries = entries;
        }
    }
}