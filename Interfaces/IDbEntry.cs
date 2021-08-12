using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace BucketDatabase.Interfaces
{
    public interface IDbEntry : INotifyPropertyChanged, INotifyCollectionChanged
    {
        Guid Id { get; set; }
        Guid FileId { get; set; }
    }
}