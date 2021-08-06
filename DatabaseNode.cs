using System;
using System.IO;
using System.Threading.Tasks;

namespace BucketDatabase
{
    internal class DatabaseNode
    {
        public Guid FileId { get; set; }
        public DatabaseNode RightNode { get; set; }
        public DatabaseNode LeftNode { get; set; }
        private string filePath { get; }
        public DatabaseNode(string folderPath)
        {
            var rootFiles = Directory.GetFiles(folderPath);

            if (rootFiles.Length > 1)
            {
                throw new ArgumentException($"Node root '{folderPath}' is invalid");
            }


            foreach (var i in rootFiles)
            {
                filePath = i;
                // file name will just be the FileId
                FileId = Guid.Parse(Path.GetFileNameWithoutExtension(i));
                break;
            }

            var rigthNodePath = Path.Combine(folderPath, "RightNode");
            var leftNodePath = Path.Combine(folderPath, "LeftNode");

            if (Directory.Exists(rigthNodePath))
            {
                RightNode = new DatabaseNode(rigthNodePath);
            }

            if (Directory.Exists(leftNodePath))
            {
                LeftNode = new DatabaseNode(leftNodePath);
            }
        }

        public async Task<string> ReadNode()
        {
            return await File.ReadAllTextAsync(filePath);
        }
    }
}