using System.Collections.Generic;
using BucketDatabase.Interfaces;

namespace BucketDatabase.Query
{
    public class QueryReturn<T> where T: IDbEntry
    {
        public ICollection<T> FileMatches { get; internal set;}
        public T IdMatch { get; internal set;}
        public ICollection<T> QueryableMatches { get; internal set; }

        public QueryReturn(ICollection<T> fileMatches, ICollection<T> queryableMatches, T idMatch)
        {
            FileMatches = fileMatches;
            QueryableMatches = queryableMatches;
            IdMatch = idMatch;
        }

        public QueryReturn() 
        { 
            FileMatches = new List<T>();
            QueryableMatches = new List<T>();
        }
    }
}