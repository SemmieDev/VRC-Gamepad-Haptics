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
using System.Text.RegularExpressions;
using System.Text;

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
    public TMP_Text resetButtonText, errorResponse;
    public Slider heavyHapticsSlider, lightHapticsSlider;
    public TMP_InputField usernameInput, passwordInput, input2fa;

    private Auth auth;
    private WebSocket ws;
    private readonly string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private string configPath, authCachePath;
    private AvatarParameters newAvatarParameters;
    private bool avatarChanged, twoFactorAuth;

    void Start() {
        Application.targetFrameRate = 30;
        configPath = Path.Combine(Application.persistentDataPath, "config.json");
        authCachePath = Path.Combine(Application.persistentDataPath, "auth.json");
        LoadSettings();
        UnityWebRequest.ClearCookieCache();

        if (File.Exists(authCachePath)) {
            auth = JsonUtility.FromJson<Auth>(File.ReadAllText(authCachePath));
            SetCurrentUser();
            if (user != null) ContinueLogin();
        } else {
            auth = new Auth();
        }
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

    public void OnLogin() {
        UnityWebRequest request;
        if (twoFactorAuth) {
            var code = input2fa.text;

            // Fuck you unity (unity url encodes the data so I have to do this)
            request = new UnityWebRequest("https://api.vrchat.cloud/api/1/auth/twofactorauth/totp/verify");
            request.uploadHandler = new UploadHandlerRaw(("{\"code\":\""+code+"\"}").GetUTF8EncodedBytes()); // {"code":"string"}
            request.downloadHandler = new DownloadHandlerBuffer();
            request.method = UnityWebRequest.kHttpVerbPOST;
            request.SetRequestHeader("User-Agent", "VRChatGamepadHaptics/"+Application.version+" semvdmeij124@gmail.com");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Cookie", "auth="+auth.authcookie);
            request.SendWebRequest();
            while (!request.isDone) Thread.Sleep(100);

            if (request.result == UnityWebRequest.Result.ProtocolError) {
                var errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                SetErrorResponse(errorResponse.error.message);
                return;
            }

            SetCurrentUser();
            ContinueLogin();

            return;
        }

        var encodedUsername = UnityWebRequest.EscapeURL(usernameInput.text);
        var encodedPassword = UnityWebRequest.EscapeURL(passwordInput.text);

        request = UnityWebRequest.Get("https://api.vrchat.cloud/api/1/auth/user");
        request.SetRequestHeader("User-Agent", "VRChatGamepadHaptics/"+Application.version+" semvdmeij124@gmail.com");
        request.SetRequestHeader("Authorization", "Basic "+Convert.ToBase64String(Encoding.UTF8.GetBytes(encodedUsername+":"+encodedPassword)));
        request.SendWebRequest();
        while (!request.isDone) Thread.Sleep(100);

        if (request.result == UnityWebRequest.Result.ProtocolError) {
            var errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            SetErrorResponse(errorResponse.error.message);
            return;
        }

        user = JsonUtility.FromJson<User>(request.downloadHandler.text);

        auth.authcookie = Regex.Match(request.GetResponseHeader("Set-Cookie"), "authcookie_[0-9a-z]{8}-[0-9a-z]{4}-[0-9a-z]{4}-[0-9a-z]{4}-[0-9a-z]{12}").Value;

        if (user.requiresTwoFactorAuth != null) {
            usernameInput.gameObject.SetActive(false);
            passwordInput.gameObject.SetActive(false);
            input2fa.gameObject.SetActive(true);
            twoFactorAuth = true;
            SetErrorResponse("");
            return;
        }

        ContinueLogin();
    }

    private void SetCurrentUser() {
        var request = UnityWebRequest.Get("https://api.vrchat.cloud/api/1/auth/user");
        request.SetRequestHeader("User-Agent", "VRChatGamepadHaptics/"+Application.version+" semvdmeij124@gmail.com");
        request.SetRequestHeader("Cookie", "apiKey=JlE5Jldo5Jibnk5O5hTx6XVqsJu4WJ26; auth="+auth.authcookie);
        request.SendWebRequest();
        while (!request.isDone) Thread.Sleep(100);
        if (request.result == UnityWebRequest.Result.ProtocolError) {
            user = null;
            return;
        }
        user = JsonUtility.FromJson<User>(request.downloadHandler.text);
    }

    private void ContinueLogin() {
        vghComponent.enabled = true;
        loginScreen.SetActive(false);

        InitalizeAvatar();

        //TODO: VRChat removed this feature, add a refresh button
        /*ws = new WebSocket("wss://vrchat.com/?authToken="+auth.authcookie);

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

        ws.Connect();*/

        //var userJson = RequestJson("https://api.vrchat.cloud/api/1/auth/user");
        //user = JsonUtility.FromJson<User>(userJson);

        //LoadAvatars();
    }

    private void SetErrorResponse(string error) {
        errorResponse.gameObject.SetActive(true);
        errorResponse.text = error;
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

    // private UnityWebRequest GetWebRequest(string uri, bool auth) {
    //     var request = UnityWebRequest.Get(uri);
    //     string cookies = "apiKey=JlE5Jldo5Jibnk5O5hTx6XVqsJu4WJ26";
    //     if (auth) cookies += "; auth="+authcookie;
    //     request.SetRequestHeader("Cookie", cookies);
    //     return request;
    // }

    // private T RequestJson<T>(string uri) {
    //     var request = GetWebRequest(uri, true);
    //     request.SendWebRequest();
    //     while (!request.isDone) Thread.Sleep(100);
    //     return JsonUtility.FromJson<T>(request.downloadHandler.text);
    // }

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

    public void OnInfoClick() {
        Application.OpenURL("https://github.com/SemmieDev/VRC-Gamepad-Haptics/wiki/How-your-account-is-used");
    }

    private void Save() {
        File.WriteAllText(configPath, JsonUtility.ToJson(config));
        File.WriteAllText(authCachePath, JsonUtility.ToJson(auth));
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
        public string[] requiresTwoFactorAuth;
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

    [Serializable]
    public class ErrorResponse {
        public Error error;

        [Serializable]
        public class Error {
            public string message;
        }
    }
    
    [Serializable]
    public class Auth {
        public string authcookie;
    }
}
