using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BucketDatabase.Attributes;
using System.Text;
using Newtonsoft.Json;

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
        public DatabaseNode(string folderPath, int? maxNodeSize = null)
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

        public async Task WriteEntry(IDbEntry entry)
        {
            if (entry.Id != Guid.Empty)
            {
                throw new ArgumentException("entry should not have the id properties pre-populated, if these are pre-existing entries, please use the update method");
            }

            entry.Id = Guid.NewGuid();

            entry.CascadeEntryIds();

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

                            await LeftNode.WriteEntry(entry);

                            break;

                        case 1:

                            if (RightNode == null)
                            {
                                var nodePath = Path.Combine(NodeRoot, RightNodeName);
                                RightNode = new DatabaseNode(nodePath);
                            }

                            await RightNode.WriteEntry(entry);

                            break;

                        default:
                            throw new Exception("The object already exists, please use the update method instead");
                    }
                }
                else
                {
                    await Write(entry);
                }
            }
            else
            {
                FileId = Guid.NewGuid();
                await Write(entry);
            }
        }

        public async Task UpdateEntry(IDbEntry entry)
        {
            // we're comparing the entry's ID to the file ID
            // but we want to do this after we've checked if this file contains the id

            var indexEntries = await ReadIndex();

            var matchedEntry = indexEntries.FirstOrDefault(x => x.Id == entry.Id);

            if (matchedEntry != null)
            {
                var fileLines = await Helpers.ReadAllLinesAsync(FilePath);

                var newFileLines = new List<string>();
                
                foreach (var i in fileLines)
                {
                    if (i.Contains(entry.Id.ToString()))
                    {
                        var objectString = JsonConvert.SerializeObject(entry, entry.GetType(), new JsonSerializerSettings());
                        newFileLines.Add(objectString);
                    }
                    else
                    {
                        newFileLines.Add(i);
                    }
                }

                // the below is possibly an issue as it's not async
                File.Delete(FilePath);
                await Helpers.WriteAllLinesAsync(FilePath, newFileLines);
            }

            else
            {
                switch (entry.Id.CompareTo(FileId))
                {
                    case -1:
                        if (LeftNode == null)
                        {
                            throw new ArgumentException($"object '{entry.Id}' does not exist");
                        }
                        else
                        {
                            await LeftNode.UpdateEntry(entry);
                        }

                        break;

                    case 1:
                        if (RightNode == null)
                        {
                            throw new ArgumentException($"object '{entry.Id}' does not exist");
                        }
                        else
                        {
                            await RightNode.UpdateEntry(entry);
                        }

                        break;

                    
                    
                    default:
                        throw new Exception("Oops");
                }
            }
        }

        public async Task<T> Query<T>(Guid entryId) where T: IDbEntry
        {
            var nodeLines = await Helpers.ReadAllLinesAsync(FilePath);

            foreach (var i in nodeLines)
            {
                // if the line contains the id -> pull it apart
                if (i.Contains(entryId.ToString()))
                {
                    return ParseEntry<T>(i, entryId);
                }
            }

            if (LeftNode != null)
            {
                var leftNodeEntry = await LeftNode.Query<T>(entryId);

                if (leftNodeEntry != null)
                {
                    return leftNodeEntry;
                }
            }

            if (RightNode != null)
            {
                var rightNodeEntry = await RightNode.Query<T>(entryId);

                if (rightNodeEntry != null)
                {
                    return rightNodeEntry;
                }
            }

            return default(T);
        }

        private T ParseEntry<T>(string line, Guid entryId) where T: IDbEntry
        {
            // we are assuming the id is at the start of the string
            
            var idIndex = line.IndexOf(entryId.ToString());

            var entryStartIndex = ParseObjectStartIndex(line, idIndex);

            var entryEndIndex = ParseObjectEndIndex(line, entryStartIndex);

            // the end of the string is relative to the start?
            var entryString = line.Substring(entryStartIndex, entryEndIndex + 1 - entryStartIndex);

            return JsonConvert.DeserializeObject<T>(entryString);
        }

        private int ParseObjectStartIndex(string line, int idIndex)
        {
            var openParIndices = new List<int>();
            var indicesIndex = -1;

            for (int i = 0; i < idIndex; i++)
            {
                if (line[i] == '{')
                {
                    openParIndices.Add(i);
                    indicesIndex++;
                }

                if (line[i] == '}')
                {
                    indicesIndex--;
                }
            }

            return openParIndices[indicesIndex];
        }

        private int ParseObjectEndIndex(string line, int entryStartIndex)
        {
            var openCount = 0;
            var entryEndIndex = 0;

            for (int i = entryStartIndex; i < line.Length; i++)
            {
                if (line[i] == '{')
                {
                    openCount++;
                }

                if (line[i] == '}')
                {
                    openCount--;
                    if (openCount == 0)
                    {
                        entryEndIndex = i;
                        break;
                    }
                }
            }

            return entryEndIndex;
        }

        // private async Task<QueryReturn<T>> QueryQueryable<T>(QueryParameter param) where T: IDbEntry
        // {
        //     var queryableEntries = await ReadQueryables();

        //     var idsToCheck = new List<Guid>();

        //     foreach (var i in param.QueryableEntries)
        //     {
        //         var queryableMatches = queryableEntries.Where(x => x.PropertyName.ToLower() == i.PropertyName.ToLower() && x.PropertyValue.ToLower() == i.PropertyValue.ToLower()).ToList();
                
        //         if (queryableMatches.Count > 0)
        //         {
        //             foreach (var j in queryableMatches)
        //             {
        //                 idsToCheck.Add(j.Id);
        //             }
        //         }
        //     }

        //     var nodeEntries = await ReadNode<T>();

        //     var matchedEntries = nodeEntries.Where(x => idsToCheck.Contains(x.Id)).ToList();

        //     if (LeftNode != null)
        //     {
        //         var leftNodeEntries = await LeftNode.QueryQueryable<T>(param);

        //         if (leftNodeEntries.QueryableMatches.Count() > 0)
        //         {
        //             matchedEntries.AddRange(leftNodeEntries.QueryableMatches);
        //         }
        //     }

        //     if (RightNode != null)
        //     {
        //         var rightNodeEntries = await RightNode.QueryQueryable<T>(param);

        //         if (rightNodeEntries.QueryableMatches.Count() > 0)
        //         {
        //             matchedEntries.AddRange(rightNodeEntries.QueryableMatches);
        //         }
        //     }

        //     return new QueryReturn<T>() { QueryableMatches = matchedEntries };
        // }

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

        public async Task DeleteEntry(IDbEntry entry)
        {
            switch (FileId.CompareTo(entry.Id))
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
                var entry = JsonConvert.DeserializeObject<QueryableEntry>(i);

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
                var entry = JsonConvert.DeserializeObject<IndexEntry>(i);

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
                var entry = JsonConvert.DeserializeObject<T>(i);

                itemCollection.Add(entry);
            }

            return itemCollection;
        }

        private async Task Write(IDbEntry entry)
        {
            var jsonOptions = new JsonSerializerSettings();
            jsonOptions.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

            var objectString = JsonConvert.SerializeObject(entry, entry.GetType(), jsonOptions);

            await Helpers.AppendAllTextAsync(FilePath, objectString + Environment.NewLine);

            await WriteQueryTerms(entry);

            await WriteIdIndex(entry);
        }

        private async Task WriteQueryTerms(IDbEntry entry)
        {
            var entryProps = entry.GetType().GetProperties();

            var termEntries = new List<string>();

            foreach (var i in entryProps)
            {
                var attributeDefined = Attribute.IsDefined(i, typeof(QueryableAttribute));
                if (attributeDefined)
                {
                    var queryEntry = new QueryableEntry(i.Name, i.GetValue(entry) == null ? "" : i.GetValue(entry).ToString(), entry.Id);
                    var queryTermString = JsonConvert.SerializeObject(queryEntry);
                    termEntries.Add(queryTermString);
                }
            }

            await Helpers.AppendAllLinesAsync(QueryTermFilePath, termEntries);
        }

        private async Task WriteIdIndex(IDbEntry entry)
        {
            var indexEntry = new IndexEntry(entry.Id);
            var indexEntryString = JsonConvert.SerializeObject(indexEntry);
            await Helpers.AppendAllTextAsync(IdIndexFilePath, indexEntryString + Environment.NewLine);
        }
    }
}