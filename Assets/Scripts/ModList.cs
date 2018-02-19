using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ModList : MonoBehaviour {

    public CrossroadsSettings settings;
    public ModDownloader downloader;
    public ModInstaller installer;

    public ModListElement modListElementPrefab;

    public RectTransform contentParent;

    public List<ModListElement> modElements;

    public Dictionary<string,bool> previousState;

    public Text status;

    public string idleStatusMessage = "Configure mods and click Update Mods";

    IEnumerator modUpdater = null;

    IEnumerator modInstaller = null;

    List<string> queuedDepencencyDownloads = new List<string>();

    public void PopulateList()
    {
        previousState = new Dictionary<string, bool>();

        List<string> downloadableMods = downloader.GetListOfDownloadableMods();
        
        //List< ModSettings > installedMods = settings.Settings.installedMods;

        foreach(string s in downloadableMods)
        {
            ModListElement listItem = Instantiate( modListElementPrefab );
            listItem.transform.SetParent( contentParent );

            listItem.ModName = s;

            //bool isInstalled = installedMods.Select(x => x).Where(x => x.modName.Contains(s)).ToList().Count > 0;

            ModSettings installedMod = settings.GetInstalledModByName(s);
            
            listItem.InstallStatus = installedMod != null;

            listItem.ModType = downloader.GetDoesModWriteToAssemblyByName( s ) ? "Assembly" : "API";

            listItem.ModDependencies = downloader.GetModDependenciesByName( s );

            listItem.gameObject.SetActive( true );

            modElements.Add( listItem );

            previousState.Add( listItem.ModName, listItem.InstallStatus );
        }

        status.text = idleStatusMessage;

        if( installer.HasSaveBackups() )
            installer.ActivateRestoreSaveButton();
    }

    public void UpdateMods()
    {
        modUpdater = DoUpdateMods();
        if( modUpdater != null )
            StartCoroutine( modUpdater );
    }

    IEnumerator DoUpdateMods()
    {
        for(int i = 0; i < modElements.Count; )
        {
            yield return null;

            //only update one mod at a time
            if( downloader.IsDownloading() )
                continue;

            //only install one mod at a time
            if( modInstaller != null )
                continue;

            //do we have any dependencies queued?
            if( queuedDepencencyDownloads.Count > 0 )
            {
                string next = queuedDepencencyDownloads[0];
                Debug.Log( "Downloading queued dependency: " + next );
                queuedDepencencyDownloads.RemoveAt( 0 );
                downloader.DownloadModByName( next, InstallDownloadedMod );
                continue;
            }

            yield return null;

            Debug.Log( modElements[i].ModName );
            Debug.Log( modElements[ i ].InstallStatus );
            Debug.Log( previousState[ modElements[ i ].ModName ] );
            //mod was changed to install or un-install
            if( modElements[ i ].InstallStatus != previousState[ modElements[ i ].ModName ] )
            {
                if( modElements[ i ].InstallStatus )
                {
                    InstallMod( modElements[ i ] );
                }
                else
                {
                    RemoveMod( modElements[ i ] );
                }
                previousState[ modElements[ i ].ModName ] = modElements[ i ].InstallStatus;
            }

            ++i;
        }

        yield return new WaitForSeconds( 1f );
        status.text = idleStatusMessage;
        modUpdater = null;
    }

    void InstallMod( ModListElement modElement )
    {
        Debug.Log( "downloading "+ modElement.ModName );
        status.text = "Downloading " + modElement.ModName;
        downloader.DownloadModByName( modElement.ModName, InstallDownloadedMod );
    }

    void InstallDownloadedMod(string modname, string modpath)
    {
        modInstaller = DoInstallDownloadedMod( modname, modpath );
        StartCoroutine( modInstaller );
    }

    IEnumerator DoInstallDownloadedMod(string modname, string modpath)
    {
        Debug.Log( "installing " + modname + "at " + modpath );
        status.text = "Installing " + modname;

        yield return new WaitForSeconds( .1f );

        string defaultInstallPath = downloader.GetDefaultInstallPathByModByName(modname);

        //TODO: post a warning about the conflit and resolution/removal for users

        //remove mods that conflit with this mod
        CheckAndResolveConflicts( modname );

        //install this mod
        //TODO: correctly handle a failed install
        installer.InstallMod( modpath, modname, defaultInstallPath );

        UpdateModListState( modname, true );

        List<string> dependencies = downloader.GetModDependenciesByName(modname);

        modInstaller = null;

        //does it have dependencies?
        if(dependencies.Count > 0)
        {
            //see if we need to get them
            foreach(string s in dependencies)
            {
                Debug.Log( "Queuing dependency for download: " + s );
                //is the mod already installed? if so skip queuing it
                if( settings.GetInstalledModByName( s ) != null )
                    continue;

                //don't double download
                if( !queuedDepencencyDownloads.Contains(s) )
                    queuedDepencencyDownloads.Add( s );
            }
        }

        yield break;
    }

    void RemoveMod( ModListElement modElement )
    {
        status.text = "Unintalling " + modElement.ModName;
        installer.UninstallMod( modElement.ModName );
    }


    void CheckAndResolveConflicts( string modnameToInstall )
    {
        bool writesToAssembly = downloader.GetDoesModWriteToAssemblyByName(modnameToInstall);

        Debug.Log( modnameToInstall + " writes to assembly. Checking if we have a conflict" );

        //if this mod writes to assembly-csharp, remove other assembly mods first
        if( writesToAssembly )
        {
            foreach(var mod in settings.Settings.installedMods)
            {
                string modName = mod.modName;
                bool modWritesToAssembly = downloader.GetDoesModWriteToAssemblyByName( modName );

                //does it write to assembly? uninstall it.
                if(modWritesToAssembly)
                {
                    Debug.Log( modName + " ALSO writes to assembly. Uninstalling before we start the install." );
                    installer.UninstallMod( modName );
                    UpdateModListState( modName, false );
                }
            }
        }
    }

    void UpdateModListState(string modname, bool newState)
    {
        previousState[ modname ] = newState;

        foreach( var m in modElements )
        {
            if( m.ModName == modname )
                m.InstallStatus = newState;
        }
    }
}
