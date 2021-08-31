using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BucketDatabase.Attributes;

namespace BucketDatabase
{
    internal static class Helpers
    {
        public async static Task<List<string>> ReadAllLinesAsync(string filePath)
        {
            byte[] result;

            using (FileStream SourceStream = File.Open(filePath, FileMode.Open))
            {
                result = new byte[SourceStream.Length];
                await SourceStream.ReadAsync(result, 0, (int)SourceStream.Length);
            }

            var readOutput = Encoding.ASCII.GetString(result);
            var fileLines = readOutput.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            return fileLines.ToList();
        }

        public async static Task WriteAllLinesAsync(string filePath, List<string> fileLines, bool overwrite = false)
        {
            var enco = new ASCIIEncoding();

            var fileText = String.Join(Environment.NewLine, fileLines);

            var result = enco.GetBytes(fileText);

            using (FileStream SourceStream = File.Open(filePath, FileMode.OpenOrCreate))
            {
                if (result.Length == 0)
                {
                    SourceStream.SetLength(0);
                }

                if (overwrite)
                {
                    SourceStream.SetLength(0);
                }

                SourceStream.Seek(0, SeekOrigin.Begin);
                await SourceStream.WriteAsync(result, 0, result.Length);
            }
        }

        public async static Task AppendAllLinesAsync(string filePath, IList<string> appendLines)
        {
            var fileLines = new List<String>();

            if (File.Exists(filePath))
            {
                fileLines = await ReadAllLinesAsync(filePath);
            }

            foreach (var i in appendLines)
            {
                fileLines.Add(i);
            }

            await WriteAllLinesAsync(filePath, fileLines);
        }

        public async static Task AppendAllTextAsync(string filePath, string appendText)
        {

            var fileLines = new List<string>();

            if (File.Exists(filePath))
            {
                fileLines = await ReadAllLinesAsync(filePath);
            }

            fileLines.Add(appendText);

            await WriteAllLinesAsync(filePath, fileLines);
        }

        public static void Cascade(this IDbEntry entry)
        {
            var props = entry.GetType().GetProperties();

            foreach (var i in props)
            {
                if (i.PropertyType == typeof(IDbEntry))
                {
                    var propertyValue = i.GetValue(entry) as IDbEntry;

                    if (propertyValue != null)
                    {
                        if (propertyValue.StateDate == default(DateTime))
                        {
                            propertyValue.StateDate = entry.StateDate;
                        }

                        if (propertyValue.FileId == Guid.Empty)
                        {
                            propertyValue.FileId = entry.FileId;
                        }

                        if (propertyValue.Id == Guid.Empty)
                        {
                            propertyValue.Id = Guid.NewGuid();
                        }

                        propertyValue.Cascade();
                    }
                    
                }

                else if (typeof(IEnumerable).IsAssignableFrom(i.PropertyType))
                {
                    var propertyValue = i.GetValue(entry) as IEnumerable<IDbEntry>;

                    if (propertyValue != null)
                    {
                        foreach (var collectionEntry in propertyValue)
                        {
                            if (collectionEntry.StateDate == default(DateTime))
                            {
                                collectionEntry.StateDate = entry.StateDate;
                            }

                            if (collectionEntry.FileId == Guid.Empty)
                            {
                                collectionEntry.FileId = entry.FileId;
                            }

                            if (collectionEntry.Id == Guid.Empty)
                            {
                                collectionEntry.Id = Guid.NewGuid();
                            }
                            
                            collectionEntry.Cascade();
                        }
                    }
                }
            }
        }

        public static List<QueryableEntry> PullQueryables(this IDbEntry entry)
        {
            var entryProps = entry.GetType().GetProperties();

            var termEntries = new List<QueryableEntry>();

            foreach (var i in entryProps)
            {
                var attributeDefined = Attribute.IsDefined(i, typeof(QueryableAttribute));
                if (attributeDefined)
                {
                    var queryEntry = new QueryableEntry(i.Name, i.GetValue(entry) == null ? "" : i.GetValue(entry).ToString(), entry.Id);
                    termEntries.Add(queryEntry);
                }

                else if (i.PropertyType == typeof(IDbEntry))
                {
                    var idbEntryValue = i.GetValue(entry) as IDbEntry;

                    if (idbEntryValue != null)
                    {
                        termEntries.AddRange(idbEntryValue.PullQueryables());
                    }
                }

                else if (typeof(IEnumerable).IsAssignableFrom(i.PropertyType)) 
                {
                    var propertyValue = i.GetValue(entry) as IEnumerable<IDbEntry>;

                    if (propertyValue != null)
                    {
                        foreach (var collectionEntry in propertyValue)
                        {
                            termEntries.AddRange(collectionEntry.PullQueryables());
                        }
                    }
                }
            }

            return termEntries;
        }

    }
}