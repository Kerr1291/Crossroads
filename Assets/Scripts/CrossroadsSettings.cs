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
    //TODO: make these input fields and update the config file when these are changed
    public Text gamePathLabel;
    public Text localRepoLabel;
    public Text modsFolderLabel;

    public bool Loaded { get; private set; }

    //functions to run after loading is complete
    public UnityEngine.Events.UnityEvent OnLoaded;

    [SerializeField]
    bool debugSkipHollowKnightFolderFinder = false;

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
    string localModRepoFolderPath = "";
    public string LocalModRepoFolderPath
    {
        get
        {
            if(!string.IsNullOrEmpty(localModRepoFolderPath))
                return localModRepoFolderPath;
            return SettingsFolderPath + localModRepoFolderName;
        }
    }

    [SerializeField]
    string defaultModInstallFolderName = "hollow_knight_Data\\Managed\\Mods";

    [SerializeField]
    string defaultGameFolderName = "Hollow Knight";

    [XmlRoot("AppSettings")]
    public class AppSettings
    {
        [XmlElement("GamePath")]
        public string gamePath;
        [XmlElement("ModsInstallPath")]
        public string modsInstallPath;
        [XmlElement("LocalModRepoPath")]
        public string modRepoPath;

        [XmlArray("InstalledMods")]
        public List<ModSettings> installedMods;
    }

    public AppSettings Settings { get; private set; }

    [XmlRoot( "ModSettings" )]
    public class ModSettings
    {
        [XmlElement("ModName")]
        public string modName;
        [XmlElement("ModPath")]
        public string modDll;
    }
    
    //file finder, use to find the hollow knight folder when first creating the settings file
    FileFinder finder = new FileFinder();

    void Awake()
    {
        Setup();
    }

    void Setup()
    {
        Loaded = false;
        SetupDefaults();

        AppSettings appSettings = new AppSettings();
        if(!ReadSettingsFromFile(out appSettings))
        {
            System.Windows.Forms.MessageBox.Show( "Failed to read settings file. ");
            Application.Quit();
        }

        //Debug.LogError( "GOT FILE" );
        //if the finder has started, don't load yet
        if(!finder.Running)
            LoadSettings(appSettings);
    }
    
    public void AddInstalledModInfo(ModSettings modSettings)
    {
        Settings.installedMods.Add( modSettings );
        WriteSettingsToFile( Settings );
    }

    public void RemoveInstalledModInfo(string modName)
    {
        Settings.installedMods = Settings.installedMods.Select(x => x).Where(x => x.modName != modName).ToList();
        WriteSettingsToFile(Settings);
    }

    void SetupDefaults()
    {
        if(!Directory.Exists(SettingsFolderPath))
            Directory.CreateDirectory(SettingsFolderPath);

        if(!File.Exists(SettingsFilePath))
        {
            CreateDefaultSettings();

            //TODO: remove later, this is just for demo purposes
            //CreateExampleModInfo();

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
                if( TryRegisterySteamSearch() )
                    return;
                
                finder.OnFindCompleteCallback = FinderComplete;

                //TODO: move coroutine into a variable that can be easily canceled?
                StartCoroutine(finder.ThreadedFind(defaultGameFolderName));
            }
        }
    }

    bool TryRegisterySteamSearch()
    {
        object value = null;

        try
        {
            value = Registry.CurrentUser.OpenSubKey( "Software" ).OpenSubKey( "Valve" ).OpenSubKey( "Steam" ).GetValue( "SteamPath" );
        }
        catch(Exception e)
        {
            Debug.LogError( "Cannot find steam! Skipping the registry detection step..." );
        }

        if( value == null )
            return false;

        string steamConfigPath = (value as string) + "/Config/config.vdf";

        //something is horribly wrong in the universe
        if( !File.Exists( steamConfigPath ) )
        {
            Debug.LogError( "cannot find config at "+ steamConfigPath );
            return false;
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
                        FinderComplete( s );
                        return true;
                    }

                    if( s.Contains( "SteamApps" ) )
                    {
                        foreach( string a in Directory.EnumerateDirectories( gamesPath, "Hollow Knight", SearchOption.AllDirectories ) )
                        {
                            if( a.Contains( "Hollow Knight" ) )
                            {
                                FinderComplete( a );
                                return true;
                            }
                        }
                    }
                }
            }
            counter++;

            //TEST FOR NOW
            if( counter > 1500 )
                return false;
        }

        return false;
    }

    void CreateDefaultSettings()
    {
        AppSettings defaultSettings = new AppSettings
        {
            gamePath = "Path Not Set",
            modsInstallPath = "Path Not Set",
            modRepoPath = LocalModRepoFolderPath,
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

    void FinderComplete(string path)
    {
        Settings.gamePath = path;
        Settings.modsInstallPath = path + "\\" + defaultModInstallFolderName;

        if(!Directory.Exists(Settings.modsInstallPath))
            Directory.CreateDirectory(Settings.modsInstallPath);

        WriteSettingsToFile(Settings);
        finder.OnFindCompleteCallback = null;

        LoadSettings(Settings);
    }

    void LoadSettings(AppSettings settings)
    {
        if( Loaded )
            return;

        if(settings == null)
            return;

        Settings = settings;

        //create the folder to store downloaded mods
        if( !Directory.Exists( Settings.modRepoPath ) )
            Directory.CreateDirectory( Settings.modRepoPath );
        
        //Debug.Log( "Game Path: " + Settings.gamePath );
        //Debug.Log( "Mod Repo Path: " + Settings.modRepoPath );
        //Debug.Log( "Mod Install Path: " + Settings.modsInstallPath );

        gamePathLabel.text = Settings.gamePath;
        localRepoLabel.text = Settings.modRepoPath;
        modsFolderLabel.text = Settings.modsInstallPath;

        //TODO: process the data retrieved from the settings file
        Loaded = true;
        if(OnLoaded != null)
            OnLoaded.Invoke();
    }


    void OnApplicationQuit()
    {
        if( Application.isEditor )
        {
            Directory.Delete( UnityEngine.Application.dataPath + "/" + "Settings" + "/", true );
        }
    }

    //void OnDestroy()
    //{
    //    if( Application.isEditor )
    //    {
    //        Directory.Delete( UnityEngine.Application.dataPath + "/" + "Settings" + "/", true );
    //    }
    //}
}
