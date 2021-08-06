using System;
using System.IO;
using System.Threading.Tasks;
using BucketDatabase.Interfaces;

namespace BucketDatabase
{
    public class DatabaseReader
    {
        public string DatabaseRoot { get; private set;}
        internal DatabaseNode RootNode { get; set; }
        public DatabaseReader(string dbRoot)
        {
            DatabaseRoot = dbRoot;
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            var rootFiles = Directory.GetFiles(DatabaseRoot);

            if (rootFiles.Length > 1)
            {
                throw new ArgumentException("Root is invalid");
            }

            RootNode = new DatabaseNode(DatabaseRoot);
        }

        public async Task WriteItem(ILogEntry entry)
        {
            
        }

    }
}