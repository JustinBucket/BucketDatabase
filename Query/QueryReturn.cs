using System.Collections.Generic;
using BucketDatabase.Interfaces;

namespace BucketDatabase.Query
{
    public class QueryReturn<T> where T: IDbEntry
    {
        public T IdMatch { get; internal set;}
        public IList<T> QueryableMatches { get; internal set; }

        public QueryReturn(IList<T> queryableMatches, T idMatch)
        {
            QueryableMatches = queryableMatches;
            IdMatch = idMatch;
        }

        public QueryReturn() 
        { 
            QueryableMatches = new List<T>();
        }
    }
}