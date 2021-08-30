// using System;
// using System.IO;
// using BucketDatabase.Attributes;

// namespace BucketDatabase
// {
//     public abstract class DatabaseModel
//     {
//         public DatabaseModel(string dbRoot, int? maxNodeSize = null)
//         {
//             GenerateTableFiles(dbRoot);
//         }

//         private void GenerateTableFiles(string dbRoot, int? maxNodeSize = null)
//         {
//             var props = this.GetType().GetProperties();

//             foreach (var i in props)
//             {
//                 if (Attribute.IsDefined(i, typeof(TableAttribute)))
//                 {
//                     var rootPath = Path.Combine(dbRoot, i.Name);

//                     if (!Directory.Exists(rootPath))
//                     {
//                         Directory.CreateDirectory(rootPath);
//                     }

//                     var node = new DatabaseNode(rootPath, maxNodeSize);

//                     i.SetValue(this, node);
//                 }
//             }
//         }
//     }
// }