using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Microsoft.DotNet.Build.Tasks.SharedFramework.Sdk.src
{
    public class CreateMsiPackageDrop : BuildTask
    {
        private const int _fieldsArtifactId = 0;
        private const int _fieldsArtifactPath1 = 6;
        private const int _fieldsArtifactPath2 = 1;

        [Required]
        public string WixObjPackageDir { get; set; }
        public bool NoLogo { get; set; }
        public string Culture { get; set; }
        [Required]
        public string OutFile { get; set; }
        public ITaskItem [] WixExtensions { get; set; }
        [Required]
        public ITaskItem [] WixSrcFiles { get; set; }

        // The light command that was originally used to generate the MSI.  This is purely used for informational purposes
        // and to validate that the light command being created by this task is correct (assist with debugging).
        public string OriginalLightCommand { get; set; }

        public override bool ExecuteCore()
        {
            string packageDropOutputFolder = Path.Combine(WixObjPackageDir, Path.GetFileNameWithoutExtension(OutFile));
            if (!Directory.Exists(packageDropOutputFolder))
            {
                Directory.CreateDirectory(packageDropOutputFolder);
            }
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
            nsmgr.AddNamespace("wix", "http://schemas.microsoft.com/wix/2006/objects");

            foreach (var wixobj in WixSrcFiles)
            {
                // copy the file to outputPath
                string newWixObjFile = Path.Combine(packageDropOutputFolder, Path.GetFileName(wixobj.ItemSpec));
                Log.LogMessage(MessageImportance.Normal, $"Creating modified wixobj file '{newWixObjFile}'...");
                if (File.Exists(newWixObjFile))
                {
                    Log.LogMessage(MessageImportance.Low, $"wixobj '{newWixObjFile}' file already exists, skipping");
                    continue;
                }
                File.Copy(wixobj.ItemSpec, newWixObjFile);

                XDocument doc = XDocument.Load(newWixObjFile);
                if (doc == null)
                {
                    Log.LogError($"Failed to open the wixobj file '{newWixObjFile}'");
                    continue;
                }

                // process fragment - WixFile elements
                // path in field 7
                string xpath = "//wix:wixObject/wix:section[@type='fragment']/wix:table[@name='WixFile']/wix:row";
                ProcessXPath(doc, xpath, packageDropOutputFolder, nsmgr, _fieldsArtifactPath1);

                // process product - WixFile elements
                // path in field 7
                xpath = "//wix:wixObject/wix:section[@type='product']/wix:table[@name='WixFile']/wix:row";
                ProcessXPath(doc, xpath, packageDropOutputFolder, nsmgr, _fieldsArtifactPath1);

                // process fragment - Binary elements
                // path in field 2
                xpath = "//wix:wixObject/wix:section[@type='fragment']/wix:table[@name='Binary']/wix:row";
                ProcessXPath(doc, xpath, packageDropOutputFolder, nsmgr, _fieldsArtifactPath2);

                // process product - Icon elements
                // path in field 2
                xpath = "//wix:wixObject/wix:section[@type='product']/wix:table[@name='Icon']/wix:row";
                ProcessXPath(doc, xpath, packageDropOutputFolder, nsmgr, _fieldsArtifactPath2);

                // process product - WixVariable elements
                // path in field 2
                xpath = "//wix:wixObject/wix:section[@type='product']/wix:table[@name='WixVariable']/wix:row";
                ProcessXPath(doc, xpath, packageDropOutputFolder, nsmgr, _fieldsArtifactPath2);

                doc.Save(newWixObjFile);
            }

            // Write Light command to file
            string commandFilename = Path.Combine(packageDropOutputFolder, "light.cmd");
            string commandString = string.Empty;
            if(OriginalLightCommand != null)
            {
                commandString += "REM Original light command" + Environment.NewLine;
                commandString += "REM " + OriginalLightCommand + Environment.NewLine;
            }
            commandString += "REM Modified light command" + Environment.NewLine;
            commandString += "light.exe";
            if(NoLogo)
            {
                commandString += " -nologo";
            }
            if (Culture != null)
            {
                commandString += $" -culture:{Culture}";
            }
            commandString += $" -out {OutFile}";
            if(WixExtensions != null)
            {
                foreach(var wixExtension in WixExtensions)
                {
                    commandString += $" -ext {wixExtension.ItemSpec}";
                }
            }
            if(WixSrcFiles != null)
            {
                foreach(var wixSrcFile in WixSrcFiles)
                {
                    commandString += $" {Path.GetFileName(wixSrcFile.ItemSpec)}";
                }
            }
            File.WriteAllText(commandFilename, commandString);

            return !Log.HasLoggedErrors;
        }
        void ProcessXPath(XDocument doc, string xpath, string outputPath, XmlNamespaceManager nsmgr, int pathField1, int pathField2 = 0)
        {
            IEnumerable<XElement> iels = doc.XPathSelectElements(xpath, nsmgr);
            if (iels != null && iels.Count() > 0)
            {
                // there are 7 <field> child elements in every 'row'
                // first one is an ID - will use if for folder name
                // third and six fields are full paths - the same value - update both
                foreach (XElement row in iels)
                {
                    IEnumerable<XElement> fields = row.XPathSelectElements("wix:field", nsmgr);
                    if (fields == null || fields.Count() == 0)
                    {
                        // no fields in payload's row?!
                        continue;
                    }

                    int count = 0;
                    string id = "";
                    string oldPath = "";
                    string newRelativePath = "";
                    bool foundArtifact = false;

                    foreach (XElement field in fields)
                    {
                        if (count == _fieldsArtifactId)
                        {
                            id = field.Value;
                        }
                        else if (count == pathField1)
                        {
                            // TODO: any checks?
                            oldPath = field.Value;
                            if (!File.Exists(oldPath))
                            {
                                break;
                            }

                            foundArtifact = true;
                            newRelativePath = Path.Combine(id, Path.GetFileName(oldPath));
                            field.Value = newRelativePath;
                        }
                        else if (pathField2 != 0 && count == pathField2)
                        {
                            // TODO: any checks?
                            field.Value = newRelativePath;
                            break;
                        }
                        count++;
                    }

                    if (foundArtifact)
                    {
                        string newFolder = Path.Combine(outputPath, id);
                        if (!Directory.Exists(newFolder))
                        {
                            Directory.CreateDirectory(newFolder);
                        }

                        File.Copy(oldPath, Path.Combine(outputPath, newRelativePath));
                    }
                }
            }
        }
    }
}
