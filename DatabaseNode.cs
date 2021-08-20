using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BucketDatabase.Attributes;
using BucketDatabase.Interfaces;
using BucketDatabase.Query;
using System.Text;

namespace BucketDatabase
{
    public class DatabaseNode
    {
        
        // Constants
        private int MaxFileSize = 5_000;
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
        internal DatabaseNode(string folderPath, int? maxNodeSize = null)
        {
            if (maxNodeSize != null)
            {
                MaxFileSize = maxNodeSize.Value;
            }

            NodeRoot = folderPath;

            var rigthNodePath = Path.Combine(folderPath, RightNodeName);
            var leftNodePath = Path.Combine(folderPath, LeftNodeName);

            var nodeExists = Directory.Exists(NodeRoot);

            var rootFiles = new List<string>();

            if (nodeExists)
            {
                rootFiles = Directory.GetFiles(NodeRoot).ToList();
            }

            if (Directory.Exists(NodeRoot) == false || rootFiles.Count == 0)
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                var dbFile = rootFiles.FirstOrDefault(x => x.Contains("Query") == false && x.Contains("Index") == false);

                FileId = Guid.Parse(Path.GetFileNameWithoutExtension(dbFile));
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
                var nodeFileInfo = new FileInfo(FilePath);

                if (nodeFileInfo.Length > MaxFileSize)
                {
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
                    entry.FileId = FileId;
                    await Write<T>(entry);
                }
            }
            else
            {
                FileId = entry.FileId;
                await Write<T>(entry);
            }

        }

        public async Task UpdateNode<T>(T entry) where T: IDbEntry
        {
            switch (entry.FileId.CompareTo(FileId))
            {
                case -1:
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

                    var fileLines = await Helpers.ReadAllLinesAsync(FilePath);

                    var newFileLines = new List<string>();
                    
                    foreach (var i in fileLines)
                    {
                        if (i.Contains(entry.Id.ToString()))
                        {
                            var objectString = JsonSerializer.Serialize<T>(entry);
                            newFileLines.Add(objectString);
                        }
                        else
                        {
                            newFileLines.Add(i);
                        }
                    }

                    File.Delete(FilePath);
                    await Helpers.WriteAllLinesAsync(FilePath, newFileLines);

                    break;
                
                default:
                    throw new Exception("Oops");
            }
        }

        public async Task<QueryReturn<T>> Query<T>(QueryParameter param) where T: IDbEntry
        {
            var queryReturn = new QueryReturn<T>();

            if (param.Id != new Guid())
            {
                var idResult = await QueryId<T>(param);
                if (!EqualityComparer<T>.Default.Equals(idResult, default(T)))
                {
                    queryReturn.IdMatch = idResult;
                }
            }

            if (param.QueryableEntries.Count() > 0)
            {
                var result = await QueryQueryable<T>(param);
                if (result != null && result.QueryableMatches != null && result.QueryableMatches.Count() > 0)
                {
                    queryReturn.QueryableMatches = result.QueryableMatches;
                }
            }

            return queryReturn;
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
                var queryableMatches = queryableEntries.Where(x => x.PropertyName.ToLower() == i.PropertyName.ToLower() && x.PropertyValue.ToLower() == i.PropertyValue.ToLower()).ToList();
                
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

                if (leftNodeEntries.QueryableMatches.Count() > 0)
                {
                    matchedEntries.AddRange(leftNodeEntries.QueryableMatches);
                }
            }

            if (RightNode != null)
            {
                var rightNodeEntries = await RightNode.QueryQueryable<T>(param);

                if (rightNodeEntries.QueryableMatches.Count() > 0)
                {
                    matchedEntries.AddRange(rightNodeEntries.QueryableMatches);
                }
            }

            return new QueryReturn<T>() { QueryableMatches = matchedEntries };
        }

        public async Task<IList<T>> ReadAllNodes<T>() where T: IDbEntry
        {
            var items = new List<T>();

            items.AddRange(await ReadNode<T>());

            if (LeftNode != null)
            {
                items.AddRange(await LeftNode.ReadAllNodes<T>());
            }

            if (RightNode != null)
            {
                items.AddRange(await RightNode.ReadAllNodes<T>());
            }

            return items;
        }

        public async Task DeleteEntry<T>(T entry) where T: IDbEntry
        {
            switch (FileId.CompareTo(entry.FileId))
            {
                case -1:
                    if (LeftNode == null)
                    {
                        throw new ArgumentException("entry does not exist");
                    }
                    await LeftNode.DeleteEntry(entry);
                    break;

                case 1:
                    
                    if (RightNode == null)
                    {
                        throw new ArgumentException("entry does not exist");
                    }
                    
                    await RightNode.DeleteEntry(entry);
                    break;

                case 0:

                    var fileLines = await Helpers.ReadAllLinesAsync(FilePath);
                    var newFileLines = new List<string>();

                    foreach (var i in fileLines)
                    {
                        if (!i.Contains(entry.Id.ToString()))
                        {
                            newFileLines.Add(i);
                        }
                    }

                    await Helpers.WriteAllLinesAsync(FilePath, newFileLines);
                    break;

                default:
                    throw new Exception("this shouldn't have happened");
            }
        }

        private async Task<IList<QueryableEntry>> ReadQueryables()
        {
            var fileLines = await Helpers.ReadAllLinesAsync(QueryTermFilePath);

            var queryableEntries = new List<QueryableEntry>();

            foreach (var i in fileLines)
            {
                var entry = JsonSerializer.Deserialize<QueryableEntry>(i);

                queryableEntries.Add(entry);
            }

            return queryableEntries;
        }

        private async Task<IList<IndexEntry>> ReadIndex()
        {
            var fileLines = await Helpers.ReadAllLinesAsync(IdIndexFilePath);

            var indexEntries = new List<IndexEntry>();

            foreach (var i in fileLines)
            {
                var entry = JsonSerializer.Deserialize<IndexEntry>(i);

                indexEntries.Add(entry);
            }

            return indexEntries;
        }

        private async Task<IList<T>> ReadNode<T>() where T: IDbEntry
        {
            var fileLines = await Helpers.ReadAllLinesAsync(FilePath);

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

            await Helpers.AppendAllTextAsync(FilePath, objectString + Environment.NewLine);

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

            await Helpers.AppendAllLinesAsync(QueryTermFilePath, termEntries);
        }

        private async Task WriteIdIndex<T>(T entry) where T: IDbEntry
        {
            var indexEntry = new IndexEntry(entry.Id);
            var indexEntryString = JsonSerializer.Serialize(indexEntry);
            await Helpers.AppendAllTextAsync(IdIndexFilePath, indexEntryString + Environment.NewLine);
        }
    }
}