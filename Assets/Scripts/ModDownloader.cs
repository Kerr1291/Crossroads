using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Ionic.Zip;
using System;

//TODO: add support for custom compatibilities
//Example: bonfire doesn't work with blackmoth

public class ModDownloader : MonoBehaviour 
{
    public CrossroadsSettings settings;

    public ModLinks modLinks;

    public Text status;

    public string ModLinksPath
    {
        get
        {
            return Application.streamingAssetsPath + "/modlinks.xml";
        }
    }

    void Awake()
    {
        Setup();
    }

    void Setup()
    {
        ReadModLinksFromFile( ModLinksPath, out modLinks );
    }

    public List<string> GetListOfDownloadableMods()
    {
        return modLinks.modList.Select( x => x.name ).ToList();
    }

    IEnumerator currentDownload = null;

    public bool IsDownloading()
    {
        return currentDownload != null;
    }

    public void DownloadModByName(string modName, Action<string, string> onDownloadComplete = null)
    {
        List<ModLink> foundModLink = modLinks.modList.Select(x => x).Where(x => x.name.Contains(modName)).ToList();

        if( foundModLink.Count <= 0 )
        {
            Debug.Log( "could not find any mod with name " + modName );
            return;
        }

        ModLink toDownload = foundModLink[0];

        string downloadLink = toDownload.link;

        if(settings.Settings.isGOG != null && settings.Settings.isGOG == true && toDownload.gogLink != null && toDownload.gogLink.Length > 1 )
        {
            downloadLink = toDownload.gogLink;
        }

        currentDownload = DownloadMod( downloadLink, toDownload.name, onDownloadComplete );
        //TODO: keep track of these so we can stop them?
        StartCoroutine( currentDownload );      
    }


    public string GetDefaultInstallPathByModByName( string modName )
    {
        List<ModLink> foundModLink = modLinks.modList.Select(x => x).Where(x => x.name.Contains(modName)).ToList();

        if( foundModLink.Count <= 0 )
            return string.Empty;

        return foundModLink[0].defaultInstallPath;
    }

    public bool GetDoesModWriteToAssemblyByName( string modName )
    {
        List<ModLink> foundModLink = modLinks.modList.Select(x => x).Where(x => x.name.Contains(modName)).ToList();

        if( foundModLink.Count <= 0 )
            return false;

        return foundModLink[ 0 ].writesOverAssembly;
    }

    public List<string> GetModDependenciesByName( string modName )
    {
        List<ModLink> foundModLink = modLinks.modList.Select(x => x).Where(x => x.name.Contains(modName)).ToList();

        if( foundModLink.Count <= 0 )
            return new List<string>();

        return foundModLink[ 0 ].dependencies;
    }

    public ModLink GetModLinkInfoByModByName( string modName )
    {
        List<ModLink> foundModLink = modLinks.modList.Select(x => x).Where(x => x.name.Contains(modName)).ToList();

        if( foundModLink.Count <= 0 )
            return null;

        return foundModLink[ 0 ];
    }

    IEnumerator DownloadMod(string modurl, string modname, Action<string, string> onDownloadComplete = null )
    {
        string downloadPath = settings.LocalModRepoFolderPath;
        string modDownloadPath = downloadPath + "/" + modname + ".zip";
        if( File.Exists( modDownloadPath ) )
        {
            onDownloadComplete?.Invoke( modname, modDownloadPath );
            currentDownload = null;
            yield break;
        }

        Debug.Log( "Downloading " + modname );
        
        WWW www = new WWW(modurl);
        while(!www.isDone)
        {
            //TODO: enable a cancel button for them

            //TODO: have this set the value on a progress bar? may not be possible since the www.progress is inaccurate...?

            status.text = "Downloading "+modname+": " + www.bytesDownloaded + "  bytes downloaded...";

            if( www.bytesDownloaded == 0 )
                status.text = "Connecting...";

            yield return null;
        }

        Debug.Log("Downloaded bytes: " + www.bytesDownloaded);
        status.text = modname+" Downloaded " + www.bytesDownloaded + " bytes. Download Complete.";
        //TODO: fail/break here if didn't download enough bytes

        FileStream fstream = null;
        try
        {
            fstream = new FileStream(modDownloadPath, FileMode.Create);

            BinaryWriter writer = new BinaryWriter(fstream);
            writer.Write(www.bytes);
            writer.Close();
        }
        catch(System.Exception e)
        {
            System.Windows.Forms.MessageBox.Show("Error creating/saving mod to repo: " + e.Message);
        }
        finally
        {
        }

        fstream?.Close();
        www?.Dispose();

        yield return new WaitForSeconds(1f);
        
        currentDownload = null;
        onDownloadComplete?.Invoke( modname, modDownloadPath );

        yield break;
    }    
    

    [XmlRoot("ModLinks")]
    public class ModLinks
    {
        [XmlElement("DriveLink")]
        public string driveLink;

        [XmlArray("ModList")]
        public List<ModLink> modList;
    }

    [XmlRoot("ModLink")]
    public class ModLink
    {
        [XmlElement("Name")]
        public string name;
        [XmlElement("Link")]
        public string link;
        [XmlElement(ElementName = "GOGLink", IsNullable = true)]
        public string gogLink;
        [XmlElement("DefaultInstallPath")]
        public string defaultInstallPath;
        [XmlElement("WritesOverAssembly")]
        public bool writesOverAssembly;

        [XmlArray("Dependencies")]
        public List<string> dependencies;
    }
    


    bool ReadModLinksFromFile(string path, out ModLinks modlinks)
    {
        modlinks = null;

        if(!File.Exists(path))
        {
            System.Windows.Forms.MessageBox.Show("No modlinks file found at " + path);
            return false;
        }

        bool returnResult = true;

        XmlSerializer serializer = new XmlSerializer(typeof(ModLinks));
        FileStream fstream = null;
        try
        {
            fstream = new FileStream( path, FileMode.Open);
            modlinks = serializer.Deserialize(fstream) as ModLinks;
        }
        catch(System.Exception e)
        {
            System.Windows.Forms.MessageBox.Show("Error loading modlinks file " + e.Message);
            returnResult = false;
        }
        finally
        {
            fstream.Close();
        }

        return returnResult;
    }
}
