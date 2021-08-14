using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BucketDatabase.Attributes;
using BucketDatabase.Interfaces;
using BucketDatabase.Query;

namespace BucketDatabase
{
    public class DatabaseNode
    {
        
        // Constants
        // let's say ~2KB for now
        private int MaxFileSize = 2_000;
        private string RightNodeName = "RightNode";
        private string LeftNodeName = "LeftNode";

        // properties
        public Guid FileId { get; set; }
        private DatabaseNode RightNode { get; set; }
        private DatabaseNode LeftNode { get; set; }
        internal string NodeRoot { get; }
        private string FilePath { get { return Path.Combine(NodeRoot, $"{FileId.ToString()}.bdb"); } }
        private string QueryTermFilePath { get { return Path.Combine(NodeRoot, $"QueryTerms.bdb"); } }
        private string IdIndexFilePath { get { return Path.Combine(NodeRoot, $"Index.bdb"); } }
        internal DatabaseNode(string folderPath)
        {
            NodeRoot = folderPath;

            var rigthNodePath = Path.Combine(folderPath, RightNodeName);
            var leftNodePath = Path.Combine(folderPath, LeftNodeName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (Directory.Exists(rigthNodePath))
            {
                RightNode = new DatabaseNode(rigthNodePath);
            }

            if (Directory.Exists(leftNodePath))
            {
                LeftNode = new DatabaseNode(leftNodePath);
            }
        }

        public async Task WriteToNode<T>(T entry) where T: IDbEntry
        {
            entry.Id = Guid.NewGuid();
            entry.FileId = Guid.NewGuid();

            if (File.Exists(FilePath))
            {
                // first check if the file is over the maximum allowed size
                var nodeFileInfo = new FileInfo(FilePath);

                if (nodeFileInfo.Length > MaxFileSize)
                {
                    // node already has enough information in it, write to one of the sub nodes
                    switch (entry.Id.CompareTo(FileId))
                    {
                        case -1 :
                            
                            if (LeftNode == null)
                            {
                                var nodePath = Path.Combine(NodeRoot, LeftNodeName);
                                LeftNode = new DatabaseNode(nodePath);
                            }

                            await LeftNode.WriteToNode<T>(entry);

                            break;

                        case 1:

                            if (RightNode == null)
                            {
                                var nodePath = Path.Combine(NodeRoot, RightNodeName);
                                RightNode = new DatabaseNode(nodePath);
                            }

                            await RightNode.WriteToNode<T>(entry);

                            break;

                        default:
                            throw new Exception("The object already exists, please use the update method instead");
                    }
                }
                else
                {
                    // overwrite the generated fileId with the one for this node
                    entry.FileId = FileId;

                    // node file size is less than maximum allowed, write to it
                    await Write<T>(entry);
                }
            }
            else
            {
                FileId = entry.FileId;
                // file doesn't exist, so write it
                await Write<T>(entry);
            }

        }

        public async Task UpdateNode<T>(T entry) where T: IDbEntry
        {
            switch (entry.FileId.CompareTo(FileId))
            {
                case -1:
                    // entry guid is less than current node guid, go left
                    if (LeftNode == null)
                    {
                        throw new ArgumentException($"object '{entry.Id}' does not exist");
                    }
                    else
                    {
                        await LeftNode.UpdateNode<T>(entry);
                    }

                    break;

                case 1:
                    // entry guid is greater than current node guid, go right
                    if (RightNode == null)
                    {
                        throw new ArgumentException($"object '{entry.Id}' does not exist");
                    }
                    else
                    {
                        await RightNode.UpdateNode<T>(entry);
                    }

                    break;

                case 0:
                    // found the file, load the objects, modify the right one
                    var nodeEntries = await ReadNode<T>();

                    foreach (var i in nodeEntries)
                    {
                        if (i.Id == entry.Id)
                        {
                            // we never wrote this?
                        }
                    }

                    await Write<T>(entry);
                    break;
                
                default:
                    throw new Exception("Oops");
            }
        }

        public async Task<QueryReturn<T>> Query<T>(QueryParameter param) where T: IDbEntry
        {
            var queryReturn = new QueryReturn<T>();

            if (param.Id != null && param.Id != Guid.Empty)
            {
                var idResult = await QueryId<T>(param);
                if (!EqualityComparer<T>.Default.Equals(idResult, default(T)))
                {
                    queryReturn.IdMatch = idResult;
                }
            }

            // break each section up
            if (param.FileId != new Guid())
            {
                var result = await QueryFile<T>(param);
                if (result != null && result.FileMatches != null && result.FileMatches.Count > 0)
                {
                    queryReturn.FileMatches = result.FileMatches;
                }
            }

            if (param.QueryableEntries.Count > 0)
            {
                var result = await QueryQueryable<T>(param);
                if (result != null && result.QueryableMatches != null && result.QueryableMatches.Count > 0)
                {
                    queryReturn.QueryableMatches = result.QueryableMatches;
                }
            }

            return queryReturn;
        }

        private async Task<QueryReturn<T>> QueryFile<T>(QueryParameter param) where T: IDbEntry
        {
            switch (param.FileId.CompareTo(FileId))
            {
                case -1:
                    if (LeftNode != null)
                    {
                        return await LeftNode.Query<T>(param);
                    }
                    
                    return null;

                case 1:
                    if (RightNode != null)
                    {
                        return await RightNode.Query<T>(param);
                    }

                    return null;

                case 0:
                    var nodeEntries = await ReadNode<T>();
                    return new QueryReturn<T>() { FileMatches = nodeEntries };

                default:
                    throw new Exception("this wasn't supposed to happen");
            }
        }

        private async Task<T> QueryId<T>(QueryParameter param) where T: IDbEntry
        {
            var idItems = await ReadIndex();

            var idMatchEntry = idItems.FirstOrDefault(x => x.Id == param.Id);

            if (idMatchEntry != null)
            {
                var nodeEntries = await ReadNode<T>();

                var item = nodeEntries.FirstOrDefault(x => x.Id == param.Id);

                return item;
            }

            else
            {
                if (LeftNode != null)
                {
                    var leftNodeItem = await LeftNode.QueryId<T>(param);
                    
                    if (leftNodeItem != null)
                    {
                        return leftNodeItem;
                    }
                }

                if (RightNode != null)
                {
                    var rightNodeItem = await RightNode.QueryId<T>(param);
                    
                    if (rightNodeItem != null)
                    {
                        return rightNodeItem;
                    }
                }

                return default(T);
            }
        }

        private async Task<QueryReturn<T>> QueryQueryable<T>(QueryParameter param) where T: IDbEntry
        {
            var queryableEntries = await ReadQueryables();

            var idsToCheck = new List<Guid>();

            foreach (var i in param.QueryableEntries)
            {
                var queryableMatches = queryableEntries.Where(x => x.PropertyName == i.PropertyName && x.PropertyValue == i.PropertyValue).ToList();
                
                if (queryableMatches.Count > 0)
                {
                    foreach (var j in queryableMatches)
                    {
                        idsToCheck.Add(j.Id);
                    }
                }
            }

            var nodeEntries = await ReadNode<T>();

            var matchedEntries = nodeEntries.Where(x => idsToCheck.Contains(x.Id)).ToList();

            if (LeftNode != null)
            {
                var leftNodeEntries = await LeftNode.QueryQueryable<T>(param);

                if (leftNodeEntries.QueryableMatches.Count > 0)
                {
                    matchedEntries.AddRange(leftNodeEntries.QueryableMatches);
                }
            }

            if (RightNode != null)
            {
                var rightNodeEntries = await RightNode.QueryQueryable<T>(param);

                if (rightNodeEntries.QueryableMatches.Count > 0)
                {
                    matchedEntries.AddRange(rightNodeEntries.QueryableMatches);
                }
            }

            return new QueryReturn<T>() { QueryableMatches = matchedEntries };
        }

        private async Task<ICollection<QueryableEntry>> ReadQueryables()
        {
            var fileLines = await File.ReadAllLinesAsync(QueryTermFilePath);

            var queryableEntries = new List<QueryableEntry>();

            foreach (var i in fileLines)
            {
                var entry = JsonSerializer.Deserialize<QueryableEntry>(i);

                queryableEntries.Add(entry);
            }

            return queryableEntries;
        }

        private async Task<ICollection<IndexEntry>> ReadIndex()
        {
            var fileLines = await File.ReadAllLinesAsync(IdIndexFilePath);

            var indexEntries = new List<IndexEntry>();

            foreach (var i in fileLines)
            {
                var entry = JsonSerializer.Deserialize<IndexEntry>(i);

                indexEntries.Add(entry);
            }

            return indexEntries;
        }

        private async Task<List<T>> ReadNode<T>() where T: IDbEntry
        {
            var fileLines = await File.ReadAllLinesAsync(FilePath);

            var itemCollection = new List<T>();

            foreach (var i in fileLines)
            {
                var entry = JsonSerializer.Deserialize<T>(i);

                itemCollection.Add(entry);
            }

            return itemCollection;
        }

        private async Task Write<T>(T entry) where T: IDbEntry
        {
            var objectString = JsonSerializer.Serialize<T>(entry);

            await File.AppendAllTextAsync(FilePath, objectString + Environment.NewLine);

            await WriteQueryTerms(entry);

            await WriteIdIndex<T>(entry);
        }

        private async Task WriteQueryTerms<T>(T entry) where T: IDbEntry
        {
            var entryProps = entry.GetType().GetProperties();

            var termEntries = new List<string>();

            foreach (var i in entryProps)
            {
                var attributeDefined = Attribute.IsDefined(i, typeof(QueryableAttribute));
                if (attributeDefined)
                {
                    var queryEntry = new QueryableEntry(i.Name, i.GetValue(entry) == null ? "" : i.GetValue(entry).ToString(), entry.Id);
                    var queryTermString = JsonSerializer.Serialize(queryEntry);
                    termEntries.Add(queryTermString);
                }
            }

            await File.AppendAllLinesAsync(QueryTermFilePath, termEntries);
        }

        private async Task WriteIdIndex<T>(T entry) where T: IDbEntry
        {
            var indexEntry = new IndexEntry(entry.Id);
            var indexEntryString = JsonSerializer.Serialize(indexEntry);
            await File.AppendAllTextAsync(IdIndexFilePath, indexEntryString + Environment.NewLine);
        }
    }
}