using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Ionic.Zip;
using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

public class ModInstaller : MonoBehaviour {

    public CrossroadsSettings settings;

    public List<CrossroadsSettings.ModSettings> StagedMods { get; private set; }
    public Dictionary<string,string> stagedPaths;

    public Text status;

    [XmlRoot( "ModManifest" )]
    public class ModManifest
    {
        [XmlArray("ModElements")]
        public List<ModElement> modElements;
    }

    [XmlRoot( "ModElement" )]
    public class ModElement
    {
        [XmlElement("ElementName")]
        public string elementName;
        [XmlElement("ElementPath")]
        public string elementPath;
    }

    public string tempExtractionFolder = "Temp";
    public string TempExtractionFolderPath {
        get {
            return Application.dataPath + "/" + tempExtractionFolder;
        }
    }
    
    void Awake () {
        Setup();
    }

    void Setup()
    {
        StagedMods = new List<CrossroadsSettings.ModSettings>();
        stagedPaths = new Dictionary<string, string>();
    }

    public void InstallMod( string modpath, string modname, string defaultInstallPath )
    {
        StageMod( modpath, modname );

        if( stagedPaths.ContainsKey(modname) )
        {
            string stagedPath = stagedPaths[modname];
            CrossroadsSettings.ModSettings stagedModSettings = StagedMods.Select(x => x).Where(x => x.modName == modname).ToList()[0];

            //TODO: read the install path from a ModManfiest element for the mod
            string installPath = settings.Settings.gamePath.TrimEnd('/') + defaultInstallPath;

            InstallModAtPath( stagedPath, installPath );


            settings.AddInstalledModInfo( stagedModSettings );


            status.text = "Installed " + modname;
        }
    }
    
    void InstallModAtPath(string sourcePath, string installPath)
    {
        Debug.Log( sourcePath );
        Debug.Log( installPath );

        DirectoryInfo from = new DirectoryInfo(sourcePath);
        DirectoryInfo to = new DirectoryInfo(installPath);
        CopyAll( from, to );

        //only delete if it contains the Temp path, don't want to accidently delete anything important
        if( sourcePath.Contains("Temp") )
            Directory.Delete( sourcePath, true );

    }

    //method taken from: https://stackoverflow.com/questions/9053564/c-sharp-merge-one-directory-with-another
    public static void CopyAll( DirectoryInfo source, DirectoryInfo target )
    {
        if( source.FullName.ToLower() == target.FullName.ToLower() )
        {
            return;
        }

        // Check if the target directory exists, if not, create it.
        if( Directory.Exists( target.FullName ) == false )
        {
            Directory.CreateDirectory( target.FullName );
        }

        // Copy each file into it's new directory.
        foreach( FileInfo fi in source.GetFiles() )
        {
            Debug.Log( " Copying from " + fi.FullName );
            //Debug.Log( @"Copying " + target.FullName +"/"+ fi.Name );
            fi.CopyTo( Path.Combine( target.ToString(), fi.Name ), true );
            Debug.Log( " to " + Path.Combine( target.ToString(), fi.Name ) );
        }

        // Copy each subdirectory using recursion.
        foreach( DirectoryInfo diSourceSubDir in source.GetDirectories() )
        {
            DirectoryInfo nextTargetSubDir =
            target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAll( diSourceSubDir, nextTargetSubDir );
        }
    }

    void StageMod( string modpath, string modname )
    {
        if( !File.Exists( modpath ) )
            return;

        string extractTo = TempExtractionFolderPath;
        string zipfilePath = modpath;

        if( !Directory.Exists( extractTo ) )
        {
            Directory.CreateDirectory( extractTo );
        }

        CrossroadsSettings.ModSettings modSettings = new CrossroadsSettings.ModSettings();
        modSettings.modName = modname;

        bool nameSet = false;
        bool abort = false;
        try
        {

            Debug.Log( "decompressed files" );

            Debug.Log( "zipfilePath = " + zipfilePath );
            using( ZipFile zip = ZipFile.Read( zipfilePath ) )
            {
                foreach( ZipEntry e in zip )
                {
                    if( !nameSet && e.IsDirectory )
                    {
                        //if( e.FileName.TrimEnd( '/' ).Contains("hollow_knight_Data") )
                        //{
                        //    //for some reason, the root folder of lightbringer is never "extracted" so see if we can kinda cheat here
                        //    stagedPaths[ modname ] = extractTo + "/";
                        //}
                        //else
                        //{
                        //    stagedPaths[ modname ] = extractTo + "/" + e.FileName.TrimEnd( '/' );
                        //}
                        //Debug.Log( "stagedPaths[ modname ] = "+ stagedPaths[ modname ] );

                        nameSet = true;
                    }
                    Debug.Log( e.FileName );

                    e.Extract( extractTo, ExtractExistingFileAction.OverwriteSilently );
                }
            }
        }
        catch( Exception e )
        {
            //TODO: convert to message box popup
            Debug.LogError( "Error extracting mod: " + e.Message );
            System.Windows.Forms.MessageBox.Show( "Error extracting mod from " + zipfilePath + " to "+ extractTo + " --- Error: "+ e.Message );
            abort = true;
        }

        if( abort )
            return;

        Debug.Log( extractTo );
        List<string> dllFiles = FileFinder.FindFiles( extractTo, "dll", true );

        //TODO: handle this case
        if( dllFiles.Count < 1 )
        {
            Debug.LogError( "No dlls found in mod!" );
            System.Windows.Forms.MessageBox.Show( "No dlls found in mod!" );
            return;
        }
        //TODO: handle this case
        if( dllFiles.Count > 1 )
        {
            Debug.LogError( "Many dlls found in mod!" );
        }

        modSettings.modDll = dllFiles[ 0 ];

        Debug.Log( "dll found at: " + modSettings.modDll );

        int startIndex = modSettings.modDll.IndexOf("Temp") + "Temp".Length;
        Debug.Log( "startIndex = " + startIndex );

        int length = modSettings.modDll.IndexOf('\\',startIndex+1) - startIndex;

        string realModPath = "";

        if( length < 0 )
        {
            length = 0;
        }
        else
        {
            Debug.Log( "length = " + length );

            realModPath = modSettings.modDll.Substring( startIndex, length );
        }

        if( realModPath.TrimEnd( '/' ).Contains( "hollow_knight_Data" ) )
        {
            //for some reason, the root folder of lightbringer is never "extracted" so see if we can kinda cheat here
            stagedPaths[ modname ] = extractTo + "/";
        }
        else
        {
            stagedPaths[ modname ] = extractTo + realModPath + "/";
        }

        Debug.Log( "MOD ROOT FOLDER = " + realModPath );

        Debug.Log( "Decompressed and staged " + modSettings.modName );


        StagedMods.Add( modSettings );
    }
    

    bool ReadSettingsFromFile( string path, out ModManifest manifest )
    {
        manifest = null;

        if( !File.Exists( path ) )
        {
            System.Windows.Forms.MessageBox.Show( "No settings file found at " + path );
            return false;
        }

        bool returnResult = true;

        XmlSerializer serializer = new XmlSerializer(typeof(ModManifest));
        FileStream fstream = null;
        try
        {
            fstream = new FileStream( path, FileMode.Open );
            manifest = serializer.Deserialize( fstream ) as ModManifest;
        }
        catch( System.Exception e )
        {
            System.Windows.Forms.MessageBox.Show( "Error loading manifest file " + e.Message );
            returnResult = false;
        }
        finally
        {
            fstream.Close();
        }

        return returnResult;
    }
}
