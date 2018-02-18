using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

//ModUnintaller
public partial class ModInstaller
{
    public string ConvertBackupPathToGamePath( string path, string modname )
    {
        string backupPathRoot = GetModBackupPath(modname);
        int startIndex = backupPathRoot.Length;

        string subPath = path.Substring(startIndex);
        
        string convertedPath = settings.Settings.gamePath +@"\"+ subPath;
        return convertedPath;
    }

    public void UninstallMod(string modname)
    {
        ModSettings modSettings = settings.GetInstalledModByName(modname);

        //remove mod files
        foreach(string path in modSettings.modFiles)
        {
            //check for "Hollow Knight" to only allow deleting game paths
            if( File.Exists( path ) && path.Contains("Hollow Knight") )
            {
                File.Delete( path );
            }
        }

        //copy in backed up files and delete backup
        foreach( string path in modSettings.backupFiles )
        {
            string backupFileDest = ConvertBackupPathToGamePath( path, modname );

            Debug.Log( "Copying backed up file from "+path+" to : " +backupFileDest );

            if( File.Exists( path ) )
            {
                File.Copy( path, backupFileDest, true );
            }
        }

        string backupPathRoot = GetModBackupPath(modname);

        //check for "Backup" to only allow deleting backup files
        if(Directory.Exists(backupPathRoot) && backupPathRoot.Contains("Backup"))
        {
            Directory.Delete( backupPathRoot, true );
        }


        string readMePath = GetModReadmePath(modname);

        //check for "Readme" to only allow deleting readme files
        if( Directory.Exists( readMePath ) && readMePath.Contains( "Readme" ) )
        {
            Directory.Delete( readMePath, true );
        }

        settings.RemoveInstalledModInfo( modname );
        status.text = "Uninstalled " + modname;
    }
}
