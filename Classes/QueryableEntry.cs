using System;

namespace BucketDatabase
{
    public class QueryableEntry
    {
        public string PropertyName { get; set; }
        public string PropertyValue { get; set; }
        public Guid Id { get; set; }
        public QueryableEntry(string propName, string propValue, Guid id)
        {
            PropertyName = propName;
            PropertyValue = propValue;
            Id = id;
        }
        public QueryableEntry(string propName, string propValue)
        {
            PropertyName = propName;
            PropertyValue = propValue;
        }
        public QueryableEntry() { }
    }
}