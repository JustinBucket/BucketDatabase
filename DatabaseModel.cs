using System;
using System.IO;
using BucketDatabase.Attributes;

namespace BucketDatabase
{
    public abstract class DatabaseModel
    {
        public DatabaseModel(string dbRoot)
        {
            GenerateTableFiles(dbRoot);
        }

        private void GenerateTableFiles(string dbRoot)
        {
            // get all properties
            var props = this.GetType().GetProperties();

            // go through properties looking for tables
            foreach (var i in props)
            {
                if (Attribute.IsDefined(i, typeof(TableAttribute)))
                {
                    // if property has table attribute generate the directory for it
                    var rootPath = Path.Combine(dbRoot, i.Name);

                    if (!Directory.Exists(rootPath))
                    {
                        Directory.CreateDirectory(rootPath);
                    }

                    // assign that property a new node value with the root path
                    var node = new DatabaseNode(rootPath);

                    i.SetValue(this, node);
                }
            }
        }
    }
}