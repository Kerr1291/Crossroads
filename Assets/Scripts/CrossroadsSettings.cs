using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Win32;
using System;

public class CrossroadsSettings : MonoBehaviour
{
    //Find us at #modding here
    //Hollow Knight Discord: https://discord.gg/jru7cvT

    [Header("Set to false to keep created files after leaving play mode")]
    public bool removeCreatedFoldersInEditorMode = true;

    //TODO: make these input fields and update the config file when these are changed
    public Text gamePathLabel;
    public Text localRepoLabel;
    public Text modsFolderLabel;

    public bool Loaded { get; private set; }

    //functions to run after loading is complete
    public UnityEngine.Events.UnityEvent OnLoaded;

    [SerializeField]
    bool debugSkipHollowKnightFolderFinder = false;

    bool foundGamePath = false;

    [SerializeField]
    string settingsFolderName = "Settings";
    public string SettingsFolderPath
    {
        get
        {
            return UnityEngine.Application.dataPath + "/"+ settingsFolderName + "/";
        }
    }

    [SerializeField]
    string settingsFileName = "settings.xml";
    public string SettingsFilePath
    {
        get
        {
            return SettingsFolderPath + settingsFileName;
        }
    }
    
    //folder for downloaded mod files
    [SerializeField]
    string localModRepoFolderName = "DownloadedMods";
    //string localModRepoFolderPath = "";
    public string LocalModRepoFolderPath
    {
        get
        {
            //if(!string.IsNullOrEmpty(localModRepoFolderPath))
            //    return localModRepoFolderPath;
            return UnityEngine.Application.dataPath + "/" + localModRepoFolderName;
        }
    }

    [SerializeField]
    string defaultModInstallFolderName = "hollow_knight_Data\\Managed\\Mods";

    [SerializeField]
    string defaultGameFolderName = "Hollow Knight";

    public string BackupPath {
        get {
            if( Settings == null )
                return "Settings not loaded";
            return Settings.gamePath + "/Backup";
        }
    }

    public string ReadmePath {
        get {
            if( Settings == null )
                return "Settings not loaded";
            return Settings.gamePath + "/Readme";
        }
    }

    [XmlRoot("AppSettings")]
    public class AppSettings
    {
        [XmlElement("GamePath")]
        public string gamePath;
        [XmlElement("ModsInstallPath")]
        public string modsInstallPath;
        //[XmlElement("LocalModRepoPath")]
        //public string modRepoPath;

        [XmlArray("InstalledMods")]
        public List<ModSettings> installedMods;
    }

    public AppSettings Settings { get; private set; }
    
    //file finder, use to find the hollow knight folder when first creating the settings file
    FileFinder finder = new FileFinder();

    IEnumerator Start()
    {
        Loaded = false;
        yield return SetupDefaults();

        AppSettings appSettings = new AppSettings();
        if( !ReadSettingsFromFile( out appSettings ) )
        {
            System.Windows.Forms.MessageBox.Show( "Failed to read settings file. " );
            Application.Quit();
        }
        else
        {
            if( File.Exists( appSettings.gamePath + "/hollow_knight.exe" ) )
            {
                foundGamePath = true;
            }
            else
            {
                if( Application.isEditor )
                    Debug.LogError( "Warning: Did not find hollow_knight.exe at " + appSettings.gamePath );
                else
                    System.Windows.Forms.MessageBox.Show( "Warning: Did not find hollow_knight.exe at "+ appSettings.gamePath );
            }
        }
        
        //if the finder has started, don't load yet
        if( finder.Running || !foundGamePath )
        {
            //Error???? Though we may have already errored by now so this may be reduntant
        }

        LoadSettings( appSettings );
    }
    
    public void AddInstalledModInfo( ModSettings modSettings )
    {
        Settings.installedMods.Add( modSettings );
        WriteSettingsToFile( Settings );
    }

    public void RemoveInstalledModInfo(string modName)
    {
        Settings.installedMods = Settings.installedMods.Select(x => x).Where(x => x.modName != modName).ToList();
        WriteSettingsToFile(Settings);
    }

    public ModSettings GetInstalledModByName(string modname)
    {
        if( Settings == null )
            return null;

        foreach(ModSettings ms in Settings.installedMods)
        {
            if(ms.modName == modname)
            {
                return ms;
            }
        }

        return null;
    }

    IEnumerator SetupDefaults()
    {
        if(!Directory.Exists(SettingsFolderPath))
            Directory.CreateDirectory(SettingsFolderPath);

        if(!File.Exists(SettingsFilePath))
        {
            CreateDefaultSettings();

            //use the debug skip while testing
            if(debugSkipHollowKnightFolderFinder)
            {
                Settings.gamePath = SettingsFolderPath;
                Settings.modsInstallPath = SettingsFolderPath + defaultModInstallFolderName;

                if(!Directory.Exists(Settings.modsInstallPath))
                    Directory.CreateDirectory(Settings.modsInstallPath);

                WriteSettingsToFile(Settings);
            }
            else
            {
                //try using the registery to locate steam and then using steamd game directory first
                yield return TryRegisterySteamSearch();

                if( foundGamePath )
                    yield break;
                
                //if that doesn't work, try running the brute force finder
                finder.OnFindCompleteCallback = WriteFoundGamePath;
                yield return finder.ThreadedFind(defaultGameFolderName);
            }
        }

        yield break;
    }
    
    IEnumerator TryRegisterySteamSearch()
    {
        object value = null;

        try
        {
            value = Registry.CurrentUser.OpenSubKey( "Software" ).OpenSubKey( "Valve" ).OpenSubKey( "Steam" ).GetValue( "SteamPath" );
        }
        catch(Exception e)
        {
            Debug.LogError( "Cannot find steam! Skipping the registry detection step... Error message: "+e.Message );
        }

        if( value == null )
            yield break;

        string steamConfigPath = (value as string) + "/Config/config.vdf";

        //something is horribly wrong in the universe
        if( !File.Exists( steamConfigPath ) )
        {
            Debug.LogError( "Cannot find config at "+ steamConfigPath );
            yield break;
        }

        //BaseInstallFolder

        int counter = 0;
        string line;
        Debug.Log( "parsing config" );

        // Read the file and display it line by line.  
        System.IO.StreamReader file = new System.IO.StreamReader(steamConfigPath);
        while( ( line = file.ReadLine() ) != null )
        {
            if(line.Contains( "BaseInstallFolder"))
            {
                int startIndex = line.IndexOf("_");

                startIndex = line.IndexOf('\"',startIndex+3) + 1;

                int endIndex = line.LastIndexOf('\"');

                int length = endIndex - startIndex;

                string gamesPath = line.Substring(startIndex,length);

                gamesPath = gamesPath.Replace(@"\\",@"\");

                Debug.Log( gamesPath );

                foreach(string s in Directory.EnumerateDirectories( gamesPath ))
                {
                    Debug.Log( s );

                    if( s.Contains( "Hollow Knight" ) )
                    {
                        WriteFoundGamePath( s );
                        yield break;
                    }

                    if( s.Contains( "SteamApps" ) )
                    {
                        foreach( string a in Directory.EnumerateDirectories( gamesPath, "Hollow Knight", SearchOption.AllDirectories ) )
                        {
                            if( a.Contains( "Hollow Knight" ) )
                            {
                                WriteFoundGamePath( a );
                                yield break;
                            }
                            yield return null;
                        }
                    }
                    yield return null;
                }
            }
            counter++;

            //TEST FOR NOW (update/remove me)
            if( counter > 200000 )
                yield break;

            if( counter % 1000 == 0 )
                yield return null;
        }

        yield break;
    }

    void CreateDefaultSettings()
    {
        AppSettings defaultSettings = new AppSettings
        {
            gamePath = "Path Not Set",
            modsInstallPath = "Path Not Set",
            //modRepoPath = LocalModRepoFolderPath,
            installedMods = new List<ModSettings>()
        };
        Settings = defaultSettings;
        WriteSettingsToFile(Settings);
    }

    void WriteSettingsToFile(AppSettings settings)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
        FileStream fstream = null;
        try
        {
            fstream = new FileStream(SettingsFilePath, FileMode.Create);
            serializer.Serialize(fstream, settings);
        }
        catch(System.Exception e)
        {
            System.Windows.Forms.MessageBox.Show("Error creating/saving settings file "+ e.Message);
        }
        finally
        {
            fstream.Close();
        }
    }

    bool ReadSettingsFromFile(out AppSettings settings)
    {
        settings = null;

        if(!File.Exists( SettingsFilePath ) )
        {
            System.Windows.Forms.MessageBox.Show("No settings file found at " + SettingsFilePath );
            return false;
        }

        bool returnResult = true;

        XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
        FileStream fstream = null;
        try
        {
            fstream = new FileStream(SettingsFilePath, FileMode.Open);
            settings = serializer.Deserialize(fstream) as AppSettings;
        }
        catch(System.Exception e)
        {
            System.Windows.Forms.MessageBox.Show("Error loading settings file " + e.Message);
            returnResult = false;
        }
        finally
        {
            fstream.Close();
        }

        return returnResult;
    }

    void WriteFoundGamePath(string path)
    {
        foundGamePath = true;

        Settings.gamePath = path;
        Settings.modsInstallPath = path + "\\" + defaultModInstallFolderName;

        if(!Directory.Exists(Settings.modsInstallPath))
            Directory.CreateDirectory(Settings.modsInstallPath);

        WriteSettingsToFile(Settings);
        finder.OnFindCompleteCallback = null;
    }

    void LoadSettings(AppSettings settings)
    {
        if( Loaded )
            return;

        if(settings == null)
            return;

        Settings = settings;

        //create the folder to store downloaded mods
        if( !Directory.Exists( LocalModRepoFolderPath ) )
            Directory.CreateDirectory( LocalModRepoFolderPath );
        
        //Debug.Log( "Game Path: " + Settings.gamePath );
        //Debug.Log( "Mod Repo Path: " + Settings.modRepoPath );
        //Debug.Log( "Mod Install Path: " + Settings.modsInstallPath );

        gamePathLabel.text = Settings.gamePath;
        localRepoLabel.text = LocalModRepoFolderPath;
        modsFolderLabel.text = Settings.modsInstallPath;

        //TODO: process the data retrieved from the settings file
        Loaded = true;
        if(OnLoaded != null)
            OnLoaded.Invoke();
    }

    //Cleanup install folders on quit in editor mode
    void OnApplicationQuit()
    {
        if( Application.isEditor && removeCreatedFoldersInEditorMode )
        {
            if( Directory.Exists( UnityEngine.Application.dataPath + "/" + "Settings" + "/" ) )
                Directory.Delete( UnityEngine.Application.dataPath + "/" + "Settings" + "/", true );

            if( Directory.Exists( UnityEngine.Application.dataPath + "/" + "DownloadedMods" + "/" ) )
                Directory.Delete( UnityEngine.Application.dataPath + "/" + "DownloadedMods" + "/", true );            
        }
    }
}
