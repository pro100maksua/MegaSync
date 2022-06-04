using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace MegaSync
{
    public class MegaService
    {
        public async Task SyncUserData(string email, string password, string outputDirectory, bool unzip)
        {
            var client = new MegaApiClient();
            await client.LoginAsync(email, password);

            var nodes = (await client.GetNodesAsync()).ToList();

            var files = nodes.Where(n => n.Type == NodeType.File).OrderBy(n => n.Name);

            foreach (var file in files)
            {
                var parents = GetNodeParents(file, nodes.Where(e => e.Type != NodeType.Root));

                if (parents.Any(e => e.Type == NodeType.Trash))
                {
                    continue;
                }

                var pathSegments = new List<string> { outputDirectory };
                pathSegments.AddRange(parents.Select(e => e.Name).ToArray());

                var filePath = Path.Combine(pathSegments.ToArray());
                var fileDirectory = Path.GetDirectoryName(filePath);

                if (!File.Exists(filePath))
                {
                    if (unzip && Path.GetExtension(filePath).ToUpper() == ".ZIP")
                    {
                        await ExtractFile(client, file, nodes, filePath);
                    }
                    else
                    {
                        Console.Write($"Downloading \"{file.Name}\"... ");

                        Directory.CreateDirectory(fileDirectory);

                        await client.DownloadFileAsync(file, filePath);

                        Console.WriteLine("Done.");
                        Console.WriteLine();
                    }
                }
            }

            await client.LogoutAsync();
        }

        private List<INode> GetNodeParents(INode node, IEnumerable<INode> nodes)
        {
            var parentNodes = new List<INode> { node };

            var parentId = node.ParentId;

            while (parentId != null)
            {
                var parent = nodes.FirstOrDefault(e => e.Id == parentId);

                if (parent != null)
                {
                    parentNodes.Add(parent);
                }

                parentId = parent?.ParentId;
            }

            parentNodes.Reverse();

            return parentNodes;
        }

        private async Task ExtractFile(IMegaApiClient client, INode file, IEnumerable<INode> nodes, string filePath)
        {
            var fileDirectory = Path.GetDirectoryName(filePath);

            if (Directory.Exists(fileDirectory) &&
                Directory.EnumerateFiles(fileDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.*").Any())
            {
                return;
            }

            Console.WriteLine($"Unzipping \"{file.Name}\"...");

            await using var fileStream = await client.DownloadAsync(file);

            await using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            ms.Position = 0;

            var archive = ArchiveFactory.Open(ms, new ReaderOptions { Password = "thatnovelcorner.com" });
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    Directory.CreateDirectory(fileDirectory);

                    entry.WriteToDirectory(fileDirectory, new ExtractionOptions { ExtractFullPath = true });

                    var extractedFilePath = Path.Combine(Path.GetDirectoryName(filePath), entry.Key);

                    Console.Write($"Replacing with \"{entry.Key}\"... ");

                    await ReplaceFileWithExtracted(client, file, nodes, extractedFilePath);

                    Console.WriteLine("Done.");
                    Console.WriteLine();
                }
            }
        }

        private async Task ReplaceFileWithExtracted(IMegaApiClient client, INode file, IEnumerable<INode> nodes, string filePath)
        {
            var parent = nodes.FirstOrDefault(n => n.Id == file.ParentId);

            await client.UploadFileAsync(filePath, parent);

            await client.DeleteAsync(file);
        }
    }
}