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

    [Header("Set to false to keep created files after leaving play mode")]
    public bool removeCreatedFoldersInEditorMode = true;

    public CrossroadsSettings settings;

    //Key: mod name   Value: list of paths to copy/install
    public Dictionary<string,List<string>> stagedPaths;

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
        stagedPaths = new Dictionary<string, List<string>>();
    }

    //TODO: wrap all the file creation/access parts in try-catches
    bool StageMod( string modpath, string modname )
    {
        if( !File.Exists( modpath ) )
            return false;

        string extractTo = TempExtractionFolderPath;
        string zipfilePath = modpath;

        if( !Directory.Exists( extractTo ) )
            Directory.CreateDirectory( extractTo );

        bool abort = false;
        try
        {
            Debug.Log( "Source zip file: " + zipfilePath );
            Debug.Log( "Printing unzipped files" );

            using( ZipFile zip = ZipFile.Read( zipfilePath ) )
            {
                foreach( ZipEntry e in zip )
                {
                    Debug.Log( e.FileName );

                    e.Extract( extractTo, ExtractExistingFileAction.OverwriteSilently );
                }
            }
        }
        catch( Exception e )
        {
            Debug.LogError( "Error extracting mod: " + e.Message );
            System.Windows.Forms.MessageBox.Show( "Error extracting mod from " + zipfilePath + " to " + extractTo + " --- Error: " + e.Message );
            abort = true;
        }

        if( abort )
            return false;

        Debug.Log( "Searching for dlls in "+extractTo );
        List<string> dllFiles = FileFinder.FindFiles( extractTo, "dll", true );
        
        //NOTE: May need to revisit this for mods that have no dll files, if any ever exist
        if( dllFiles.Count < 1 )
        {
            Debug.LogError( "No dlls found in mod!" );
            System.Windows.Forms.MessageBox.Show( "No dlls found in mod!" );
            return false;
        }

        stagedPaths[ modname ] = new List<string>();

        //create a staged path using each dll
        foreach(string dllFile in dllFiles)
        {
            Debug.Log( "Mod dll found at: " + dllFile );

            //Parse the path to get the root of the dll
            int startIndex = dllFile.IndexOf("Temp") + "Temp".Length;
            int length = dllFile.IndexOf('\\',startIndex+1) - startIndex;

            string localModPath = "";

            Debug.Log( "dll path parsing: startIndex = " + startIndex );

            if( length < 0 )
            {
                length = 0;
            }
            else
            {
                Debug.Log( "dll path parsing: length = " + length );

                localModPath = dllFile.Substring( startIndex, length );
            }

            if( localModPath.TrimEnd( '/' ).Contains( "hollow_knight_Data" ) )
            {
                //for some reason, the root folder of lightbringer is never "extracted" so see if we can kinda cheat here
                stagedPaths[ modname ].Add( extractTo + "/" );
            }
            else
            {
                stagedPaths[ modname ].Add( extractTo + localModPath + "/" );
            }

            Debug.Log( "Root folder for dll: " + localModPath );
        }

        Debug.Log( "Decompressed and staged " + modname );
        
        return true;
    }

    public void InstallMod( string modpath, string modname, string defaultInstallPath )
    {
        bool success = StageMod( modpath, modname );

        //if staging fails, clean up the temp folder
        if( !success )
        {
            CleanPath( TempExtractionFolderPath );
            return;
        }        

        //StageMod will add a stagedPaths entry for this modname if it is successful
        if( stagedPaths.ContainsKey(modname) )
        {
            CrossroadsSettings.ModSettings modSettings = new CrossroadsSettings.ModSettings();
            modSettings.modName = modname;

            status.text = "Installing files for " + modname;
            foreach( string stagedPath in stagedPaths[ modname ] )
            {
                //TODO: read the install path from a ModManfiest element for the mod
                string installPath = settings.Settings.gamePath.TrimEnd('/') + defaultInstallPath;

                //install the mod files and record them in our mod settings
                InstallModAtPath( stagedPath, installPath, modSettings );
            }

            status.text = "Cleaning staged files for " + modname;
            foreach( string stagedPath in stagedPaths[ modname ] )
            {
                CleanPath( stagedPath.TrimEnd('/') );
            }

            //record the mod as "installed"
            settings.AddInstalledModInfo( modSettings );
            status.text = "Installed " + modname;
        }
    }
    
    void InstallModAtPath(string sourcePath, string installPath, CrossroadsSettings.ModSettings modSettings)
    {
        Debug.Log( "Copying mod files from: "+ sourcePath +" to "+installPath);

        string backupRoot = GetModBackupPath(modSettings.modName);

        DirectoryInfo from = new DirectoryInfo(sourcePath);
        DirectoryInfo to = new DirectoryInfo(installPath);
        DirectoryInfo backup = new DirectoryInfo(backupRoot);

        BackupAll( from, to, backup, modSettings );
        CopyAll( from, to, modSettings );
    }

    static bool ReadSettingsFromFile( string path, out ModManifest manifest )
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
    
    static void CleanPath( string path )
    {
        //only delete if it contains the Temp path, don't want to accidently delete anything important
        if( !path.Contains( "Temp" ) )
        {
            System.Windows.Forms.MessageBox.Show( @"Failed to clean path " + path + @" because path did not contain 'Temp'." );
            return;
        }

        //Exit silently if the path doesn't exist (nothing to delete... so we're good)
        if( !Directory.Exists( path ) )
        {
            return;
        }

        try
        {
            Directory.Delete( path, true );
        }
        catch( Exception e )
        {
            System.Windows.Forms.MessageBox.Show( "Failed to clean path " + path + " with Error: " + e.Message );
        }
    }

    public static void BackupAll( DirectoryInfo source, DirectoryInfo target, DirectoryInfo backup, CrossroadsSettings.ModSettings modSettings = null )
    {
        if( !Directory.Exists( backup.FullName ) )
            Directory.CreateDirectory( backup.FullName );

        // Backup each file into it's backup directory.
        foreach( FileInfo fi in source.GetFiles() )
        {
            string destFile = Path.Combine( target.ToString(), fi.Name );
            string backupFile = Path.Combine( backup.ToString(), fi.Name );
            
            //don't backup readme files
            if( destFile.ToLower().Contains( "readme" ) )
                continue;

            if( File.Exists( destFile ) )
            {
                try
                {
                    //don't backup a file that's already been backed up
                    if( modSettings == null || modSettings.backupFiles == null || !modSettings.backupFiles.Contains( destFile ) )
                    {
                        //Don't double-backup a file, we might destroy an original 
                        if( !File.Exists( backupFile ) )
                        {
                            Debug.Log( " Backing up " + destFile + " to " + backupFile );
                            File.Copy( destFile, backupFile );
                        }

                        //record the install location of this file so we can uninstall it later
                        if( modSettings != null )
                        {
                            if( modSettings.backupFiles == null )
                                modSettings.backupFiles = new List<string>();

                            modSettings.backupFiles.Add( backupFile );
                        }
                    }
                }
                catch( Exception e )
                {
                    System.Windows.Forms.MessageBox.Show( "Error backup up mod file from " + destFile + " to " + backupFile + ". Error Message: " + e.Message );
                }
            }
        }

        // Backup each subdirectory using recursion.
        foreach( DirectoryInfo diSourceSubDir in source.GetDirectories() )
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            DirectoryInfo nextBackupSubDir = backup.CreateSubdirectory(diSourceSubDir.Name);
            BackupAll( diSourceSubDir, nextTargetSubDir, nextBackupSubDir, modSettings );
        }
    }
        
    //original method taken from: https://stackoverflow.com/questions/9053564/c-sharp-merge-one-directory-with-another
    public void CopyAll( DirectoryInfo source, DirectoryInfo target, CrossroadsSettings.ModSettings modSettings = null )
    {
        if( source.FullName.ToLower() == target.FullName.ToLower() )
            return;

        try
        {
            // Check if the target directory exists, if not, create it.
            if( Directory.Exists( target.FullName ) == false )
                Directory.CreateDirectory( target.FullName );
        }
        catch( Exception e )
        {
            System.Windows.Forms.MessageBox.Show( "Copy aborted. Error creating target directory at " + target.FullName + "; Error Message: " + e.Message );
            return;
        }

        // Copy each file into it's new directory.
        foreach( FileInfo fi in source.GetFiles() )
        {
            string destFile = Path.Combine( target.ToString(), fi.Name );
            
            if( File.Exists( destFile ) )
                File.SetAttributes( destFile, FileAttributes.Normal );

            //Attempting to handle UnauthorizedAccessException and anything else that might happen when copying
            
            //don't copy the readme files to the normal location
            if( modSettings != null && destFile.ToLower().Contains( "readme" ) )
            {
                string modReadmePath = GetModReadmePath(modSettings.modName);
                string modReadme = Path.Combine( modReadmePath, fi.Name );

                try
                {
                    if( !Directory.Exists( modReadmePath ) )
                        Directory.CreateDirectory( modReadmePath );

                    Debug.Log( " Copying readme from " + fi.FullName + " to " + modReadme );
                    fi.CopyTo( modReadme, true );

                    continue;
                }
                catch( Exception e )
                {
                    System.Windows.Forms.MessageBox.Show( "Error copying readme file from " + fi.FullName + " to " + modReadme + ". Error Message: " + e.Message );
                }
            }

            try
            {
                //don't copy a file that's already been copied
                if( modSettings == null || modSettings.modFiles == null || !modSettings.modFiles.Contains( destFile ) )
                {
                    //if( modSettings != null && modSettings.modFiles != null )
                    //{
                    //    Debug.Log( "List of files already copied" );
                    //    foreach( var v in modSettings.modFiles )
                    //        Debug.Log( v );
                    //}

                    Debug.Log( " Copying from " + fi.FullName + " to " + destFile );
                    fi.CopyTo( destFile, true );

                    //record the install location of this file so we can uninstall it later
                    if( modSettings != null )
                    {
                        if( modSettings.modFiles == null )
                            modSettings.modFiles = new List<string>();

                        modSettings.modFiles.Add( destFile );
                    }
                }
            }
            catch( Exception e )
            {
                System.Windows.Forms.MessageBox.Show( "Error copying mod file from " + fi.FullName + " to " + destFile + ". Error Message: " + e.Message );
            }
        }

        // Copy each subdirectory using recursion.
        foreach( DirectoryInfo diSourceSubDir in source.GetDirectories() )
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAll( diSourceSubDir, nextTargetSubDir, modSettings );
        }
    }

    string GetModBackupPath( string modname )
    {
        return settings.BackupPath + "/" + modname + "/";
    }

    string GetModReadmePath( string modname )
    {
        return settings.ReadmePath + "/" + modname + "/";
    }

    //Cleanup install folders on quit in editor mode
    void OnApplicationQuit()
    {
        if( Application.isEditor && removeCreatedFoldersInEditorMode )
        {
            if( Directory.Exists( Application.dataPath + "/" + "Temp" + "/" ) )
                Directory.Delete( Application.dataPath + "/" + "Temp" + "/", true );
        }
    }
}
