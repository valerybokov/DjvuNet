using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DjvuNet.Build.Tasks
{
    public class ResolveNativePdbs : Task
    {
        [Required]
        public ITaskItem[] DllFiles { get; set; }

        [Output]
        public ITaskItem[] ResolvedPdbs { get; set; }

        public override bool Execute()
        {
            TaskLogger.Current = this.Log;
            if (DllFiles == null || DllFiles.Length == 0)
            {
                ResolvedPdbs = Array.Empty<ITaskItem>();
                return true;
            }

            var pdbs = new List<ITaskItem>();

            foreach (var dll in DllFiles)
            {
                string dllPath = dll.GetMetadata("FullPath");
                if (!File.Exists(dllPath)) continue;

                try
                {
                    using (var stream = File.OpenRead(dllPath))
                    using (var reader = new PEReader(stream))
                    {
                        foreach (var entry in reader.ReadDebugDirectory())
                        {
                            if (entry.Type == DebugDirectoryEntryType.CodeView)
                            {
                                var data = reader.ReadCodeViewDebugDirectoryData(entry);
                                string pdbPath = data.Path;

                                if (string.IsNullOrEmpty(pdbPath)) continue;

                                // If the embedded absolute path doesn't exist, check alongside the DLL
                                int lastSlash = Math.Max(pdbPath.LastIndexOf('\\'), pdbPath.LastIndexOf('/'));
                                string actualFileName = lastSlash >= 0 ? pdbPath.Substring(lastSlash + 1) : pdbPath;

                                if (!File.Exists(pdbPath))
                                {
                                    pdbPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? string.Empty, actualFileName);
                                }

                                if (File.Exists(pdbPath))
                                {
                                    var item = new TaskItem(pdbPath);

                                    // Preserve any custom metadata from the parent DLL
                                    dll.CopyMetadataTo(item);

                                    // Force the PDB UP one level into the RID directory, just like the DLL
                                    item.SetMetadata("Link", "../" + actualFileName);
                                    item.SetMetadata("CopyToOutputDirectory", "PreserveNewest");
                                    item.SetMetadata("CopyToPublishDirectory", "PreserveNewest");

                                    pdbs.Add(item);
                                    Log.LogMessage(MessageImportance.Low, $"Resolved PDB for {Path.GetFileName(dllPath)} -> {pdbPath}");
                                    break; // Assume only one CodeView entry per DLL
                                }
                                else
                                {
                                    Log.LogMessage(MessageImportance.Normal, $"PDB encoded in {Path.GetFileName(dllPath)} not found on disk: {data.Path}");
                                }
                            }
                        }
                    }
                }
                catch (BadImageFormatException ex)
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipping non-PE file: {dllPath}. Exception: {ex.Message}");
                }
                catch (IOException ex)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Skipping unreadable file: {dllPath}. Exception: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Access denied when reading file: {dllPath}. Exception: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.High, $"Unexpected error processing file: {dllPath}. Exception: {ex.Message}");
                }
            }

            ResolvedPdbs = pdbs.ToArray();
            return true;
        }
    }
}
