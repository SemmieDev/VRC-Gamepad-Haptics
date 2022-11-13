using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using WebSocketSharp;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading;

public class VRChatAPI : MonoBehaviour {
    public MonoBehaviour vghComponent;
    public VGH vgh;
    public GameObject loginScreen;
    // public AvatarSelectionScreen avatarSelectionScreen;
    public Text currentAvatar;
    // public Avatar[] avatars { get; private set;}
    public TMP_Dropdown addressDropdown;
    [HideInInspector] public AvatarParameters avatarParameters;
    [HideInInspector] public User user;
    [HideInInspector] public Config config;
    public Button confirmResetButton;
    public TMP_Text resetButtonText;
    public Slider heavyHapticsSlider, lightHapticsSlider;

    private string authcookie;
    private WebSocket ws;
    private readonly string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private string configPath;
    private AvatarParameters newAvatarParameters;
    private bool avatarChanged;

    void Start() {
        Application.targetFrameRate = 30;
        configPath = Path.Combine(Application.persistentDataPath, "config.json");
        LoadSettings();
    }

    private void LoadSettings() {
        if (File.Exists(configPath)) {
            config = JsonUtility.FromJson<Config>(File.ReadAllText(configPath));
        } else {
            config = new Config();
        }

        heavyHapticsSlider.SetValueWithoutNotify(config.heavyHaptics);
        lightHapticsSlider.SetValueWithoutNotify(config.lightHaptics);
    }

    public void OnEnterAuthCookie(string cookie) { // DEBUGGING, refactor
        authcookie = cookie;
        vghComponent.enabled = true;
        loginScreen.SetActive(false);

        user = RequestJson<User>("https://api.vrchat.cloud/api/1/auth/user");
        InitalizeAvatar();

        ws = new WebSocket("wss://pipeline.vrchat.cloud/?authToken="+authcookie);

        ws.OnError += (ignored, msg) => {
            Debug.LogError("Web socket error: "+msg.Message+"\nWeb socket exception: "+msg.Exception.ToString());
        };

        ws.OnMessage += (sender, msg) => {
            var webSocketMessage = JsonUtility.FromJson<WebSocketMessage>(msg.Data);
            if (!webSocketMessage.type.Equals("user-update")) return;

            var newAvatar = JsonUtility.FromJson<UserUpdate>(webSocketMessage.content).user.currentAvatar;
            if (user.currentAvatar.Equals(newAvatar)) return;
            user.currentAvatar = newAvatar;

            InitalizeAvatar();
        };

        ws.Connect();

        //var userJson = RequestJson("https://api.vrchat.cloud/api/1/auth/user");
        //user = JsonUtility.FromJson<User>(userJson);

        //LoadAvatars();
    }

    private void InitalizeAvatar() {
        try {
            string avatarParametersJson = File.ReadAllText(appdata+"\\..\\LocalLow\\VRChat\\VRChat\\OSC\\"+user.id+"\\Avatars\\"+user.currentAvatar+".json");
            newAvatarParameters = JsonUtility.FromJson<AvatarParameters>(avatarParametersJson);
        } catch (FileNotFoundException) {
            Debug.Log("Avatar parameters don't exist for avatar "+user.currentAvatar);
        }
        avatarChanged = true;
    }

    private UnityWebRequest GetWebRequest(string uri, bool auth) {
        var request = UnityWebRequest.Get(uri);
        string cookies = "apiKey=JlE5Jldo5Jibnk5O5hTx6XVqsJu4WJ26";
        if (auth) cookies += "; auth="+authcookie;
        request.SetRequestHeader("Cookie", cookies);
        return request;
    }

    private T RequestJson<T>(string uri) {
        var request = GetWebRequest(uri, true);
        request.SendWebRequest();
        while (!request.isDone) Thread.Sleep(100);
        return JsonUtility.FromJson<T>(request.downloadHandler.text);
    }

    // private JSONArrayWrapper<T> FromJsonArray<T>(string json) {
    //     return JsonUtility.FromJson<JSONArrayWrapper<T>>("{\"array\":"+json+"}");
    // }

    // private void LoadAvatars() {
    //     var avatarsJson = RequestJson("https://api.vrchat.cloud/api/1/avatars/favorites?featured=true");
    //     avatars = FromJsonArray<Avatar>(avatarsJson).array;

    //     string appdata = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    //     foreach (var avatar in avatars) {
    //         try {
    //             string avatarParametersJson = File.ReadAllText(appdata+"\\..\\LocalLow\\VRChat\\VRChat\\OSC\\"+user.id+"\\Avatars\\"+avatar.id+".json");
    //             avatar.avatarParameters = JsonUtility.FromJson<AvatarParameters>(avatarParametersJson);
    //             Debug.Log(avatar.thumbnailImageUrl);
    //         } catch (FileNotFoundException) {}
    //     }

    //     avatarSelectionScreen.Generate();
    // }

    void Update() {
        if (avatarChanged && vgh.IsReady()) {
            avatarChanged = false;
            if (newAvatarParameters == null) {
                currentAvatar.text = "Avatar config does not exist";
                addressDropdown.interactable = false;
                return;
            }
            currentAvatar.text = "Current avatar: "+newAvatarParameters.name;
            avatarParameters = newAvatarParameters;
            newAvatarParameters = null;

            addressDropdown.interactable = true;
            addressDropdown.ClearOptions();

            string storedAddress = config.storedAvatarAddresses.GetValueOrDefault(user.currentAvatar, null);

            var storedAddressIndex = -1;
            var options = new List<TMP_Dropdown.OptionData>();
            options.Add(new TMP_Dropdown.OptionData("Select a parameter"));
            for (int i = 0; i < avatarParameters.parameters.Length; i++) {
                var parameter = avatarParameters.parameters[i];
                options.Add(new TMP_Dropdown.OptionData(parameter.name));
                if (storedAddress != null && storedAddress.Equals(parameter.output.address)) storedAddressIndex = i;
            }

            if (storedAddress != null && storedAddressIndex == -1) {
                config.storedAvatarAddresses.Remove(user.currentAvatar);
                storedAddress = null;
            }

            addressDropdown.AddOptions(options);
            vgh.SetAddress(storedAddress);
            if (storedAddressIndex != -1) addressDropdown.SetValueWithoutNotify(storedAddressIndex + 1);
            addressDropdown.Hide();
        }
    }

    void OnDestroy() {
        if (ws != null) ws.Close();
        Save();
    }

    public void OnReset() {
        if (confirmResetButton.gameObject.activeInHierarchy) {
            confirmResetButton.gameObject.SetActive(false);
            resetButtonText.text = "Reset";
        } else {
            confirmResetButton.gameObject.SetActive(true);
            resetButtonText.text = "No";
        }
    }

    public void OnConfirmReset() {
        OnReset();
        File.Delete(configPath);
        LoadSettings();
        avatarChanged = true;
        newAvatarParameters = avatarParameters;
    }

    private void Save() {
        File.WriteAllText(configPath, JsonUtility.ToJson(config));
    }

    // [System.Serializable]
    // public class JSONArrayWrapper<T> {
    //     public T[] array;
    // }

    // [System.Serializable]
    // public class Avatar {
    //     public string name;
    // }

    [Serializable]
    public class AvatarParameters {
        public string name;
        public AvatarParameter[] parameters;

        [Serializable]
        public class AvatarParameter {
            public string name;
            public Output output;

            [Serializable]
            public class Output {
                public string address;
                public string type;
            }
        }
    }

    [Serializable]
    private class WebSocketMessage {
        public string type;
        public string content;
    }

    [Serializable]
    private class UserUpdate {
        public User user;

        [Serializable]
        public class User {
            public string currentAvatar;
        }
    }

    [Serializable]
    public class User {
        public string currentAvatar;
        public string id;
    }

    [Serializable]
    public class Config {
        public SerializableDictionary<string, string> storedAvatarAddresses = new SerializableDictionary<string, string>();
        public float heavyHaptics, lightHaptics;
    }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();
     
        public void OnBeforeSerialize() {
            keys.Clear();
            values.Clear();

            foreach (var pair in this) {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }
     
        public void OnAfterDeserialize() {
            Clear();
 
            if (keys.Count != values.Count) {
                Debug.LogError("There are "+keys.Count+" keys and "+values.Count+" values. The addresses will be reset.");
                return;
            }

            for (int i = 0; i < keys.Count; i++) Add(keys[i], values[i]);
        }
    }
}
