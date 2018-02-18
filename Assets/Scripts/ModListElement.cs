using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ModListElement : MonoBehaviour {

    public Text modName;
    public Toggle installStatus;

    public Text modDependencies;

    public Text modType;

    public Color assemblyColor = Color.red;
    public Color apiColor = Color.green;

    public string ModName {
        get {
            return modName.text;
        }
        set {
            modName.text = value;
        }
    }

    public bool InstallStatus {
        get {
            return installStatus.isOn;
        }
        set {
            installStatus.isOn = value;
        }
    }

    public string ModType {
        get {
            return modType.text;
        }
        set {
            modType.text = value;

            if( value.Contains( "Assembly" ) )
                modType.color = assemblyColor;
            else
                modType.color = apiColor;
        }
    }

    public List<string> ModDependencies {
        get {
            if( modDependencies.text.Contains( "None" ) )
                return new List<string>();

            return modDependencies.text.Split(',').Select(x=>x.Trim()).ToList();
        }
        set {

            if( value.Count <= 0 )
            {
                modDependencies.text = "None";
                return;
            }

            string dependencies = "";
            foreach(string s in value)
            {
                if( dependencies.Length > 1 )
                    dependencies += ", ";

                dependencies += s;
            }

            modDependencies.text = dependencies;
        }
    }
}
