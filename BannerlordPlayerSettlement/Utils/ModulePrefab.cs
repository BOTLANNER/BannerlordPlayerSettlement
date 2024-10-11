using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using TaleWorlds.ModuleManager;

namespace BannerlordPlayerSettlement.Utils
{
    public static class ModulePrefab
    {
        public static XmlDocument LoadResourceAsXML(string embedPath)
        {
            using var stream = typeof(ModulePrefab).Assembly.GetManifestResourceStream(embedPath);
            if (stream is null)
                throw new NullReferenceException($"Could not find embed resource '{embedPath}'!");
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true });
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }

        public static string LoadModuleFile(string moduleName, params string[] filePaths)
        {
            string fullPath = GetModuleFilePath(moduleName, filePaths);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Unable to find specified file", fullPath);
            }

            return File.ReadAllText(fullPath);
        }

        public static XmlDocument LoadModuleFileAsXML(string moduleName, params string[] filePaths)
        {
            string fullPath = GetModuleFilePath(moduleName, filePaths);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Unable to find specified file", fullPath);
            }

            var doc = new XmlDocument();
            doc.LoadXml(File.ReadAllText(fullPath));
            return doc;
        }

        private static string GetModuleFilePath(string moduleName, string[] filePaths)
        {
            var fileSegments = new List<string>();
            fileSegments.Add(ModuleHelper.GetModuleInfo(moduleName).FolderPath);
            fileSegments.AddRange(filePaths);

            var fullPath = System.IO.Path.Combine(fileSegments.ToArray());
            return fullPath;
        }
    }
}