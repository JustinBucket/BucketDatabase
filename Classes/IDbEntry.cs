using System;
using System.Collections;
using System.Collections.Generic;

namespace BucketDatabase
{
    public interface IDbEntry
    {
        Guid Id { get; set; }
        Guid FileId { get; set; }
    }
}