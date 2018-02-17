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

    public void PopulateList()
    {
        previousState = new Dictionary<string, bool>();

        List<string> downloadableMods = downloader.GetListOfDownloadableMods();

        //TODO: get list of installed mods
        List< CrossroadsSettings.ModSettings > installedMods = settings.Settings.installedMods;

        foreach(string s in downloadableMods)
        {
            ModListElement listItem = Instantiate( modListElementPrefab );
            listItem.transform.SetParent( contentParent );

            listItem.ModName = s;

            bool isInstalled = installedMods.Select(x => x).Where(x => x.modName.Contains(s)).ToList().Count > 0;

            //TODO: set install status
            listItem.InstallStatus = isInstalled;

            listItem.gameObject.SetActive( true );
            modElements.Add( listItem );

            previousState.Add( listItem.ModName, listItem.InstallStatus );
        }

        status.text = "Configure mods and click Update Mods";
    }

    public void UpdateMods()
    {
        //TODO: save a reference to this and make sure it's not already going
        StartCoroutine( DoUpdateMods() );
    }

    IEnumerator DoUpdateMods()
    {
        for(int i = 0; i < modElements.Count; )
        {
            yield return null;

            //only update one mod at a time
            if( downloader.IsDownloading() )
                continue;

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
    }

    void InstallMod( ModListElement modElement )
    {
        Debug.Log( "downloading "+ modElement.ModName );
        status.text = "Downloading " + modElement.ModName;
        downloader.DownloadModByName( modElement.ModName, InstallDownloadedMod );
    }

    void InstallDownloadedMod(string modname, string modpath)
    {
        StartCoroutine( DoInstallDownloadedMod( modname, modpath ) );
    }

    IEnumerator DoInstallDownloadedMod(string modname, string modpath)
    {
        Debug.Log( "installing " + modname + "at " + modpath );
        status.text = "Installing " + modname;

        yield return new WaitForSeconds( .1f );

        string defaultInstallPath = downloader.GetDefaultInstallPathByModByName(modname);

        //Debug.Log( defaultInstallPath );

        installer.InstallMod( modpath, modname, defaultInstallPath );

        yield break;
    }

    void RemoveMod( ModListElement modElement )
    {
        //TODO:
    }
}
