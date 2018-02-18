using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

[XmlRoot( "ModSettings" )]
public class ModSettings
{
    [XmlElement("ModName")]
    public string modName;
    [XmlArray("ModFiles")]
    public List<string> modFiles;
    [XmlArray("BackupFiles")]
    public List<string> backupFiles;
}
