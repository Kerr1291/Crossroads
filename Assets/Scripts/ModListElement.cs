using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModListElement : MonoBehaviour {

    public Text modName;
    public Toggle installStatus;

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

}
