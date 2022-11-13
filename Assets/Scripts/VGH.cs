using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OscCore;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;

public class VGH : MonoBehaviour {
    public GameObject errorDisplay;
    public TextMeshProUGUI errorMsg, vibrationStatus;
    public Button updateButton;
    public VRChatAPI vrchatApi;

    private OscServer server;
    private bool hapticsChanged, gamepadWasConnected = true, on = true, changingSettings;
    private string address;
    private float settingsChangeDelay, haptics;

    void Start() {
        server = new OscServer(9001);

        if (server == null) {
            SetError("OSC server could not be created.\nPlease try again by restarting the program.");
            Destroy(gameObject);
            return;
        }

        StartCoroutine(CheckForUpdate());
    }

    public bool IsReady() {
        return server != null;;
    }

    private IEnumerator CheckForUpdate() {
        using (UnityWebRequest request = UnityWebRequest.Get("https://api.github.com/repos/SemmieDev/VRC-Gamepad-Haptics/releases/latest")) {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to request latest release: "+request.error);
                yield break;
            }

            var latestRelease = JsonUtility.FromJson<LatestRelease>(request.downloadHandler.text);

            if (!latestRelease.tag_name.Equals(Application.version)) updateButton.gameObject.SetActive(true);
        }
    }

    private void ReadValues(OscMessageValues values) {
        values.ForEachElement((index, typeTag) => {
            float newHaptics;

            if (typeTag == TypeTag.True) {
                newHaptics = 1.0f;
            } else if (typeTag == TypeTag.False) {
                newHaptics = 0;
            } else if (typeTag == TypeTag.Int32) {
                newHaptics = values.ReadIntElementUnchecked(index) / 100f;
            } else if (typeTag == TypeTag.Float32) {
                newHaptics = values.ReadFloatElementUnchecked(index);
            } else {
                return;
            }

            newHaptics = Mathf.Min(1, Mathf.Max(0, newHaptics));

            if (newHaptics == haptics) return;
            haptics = newHaptics;
            hapticsChanged = true;
        });
    }

    private void SetError(string msg) {
        if (msg == null) {
            errorDisplay.SetActive(false);
            return;
        }
        errorMsg.text = msg;
        errorDisplay.SetActive(true);
    }

    void Update() {
        server.Update();

        if (Gamepad.current == null) {
            if (gamepadWasConnected) {
                gamepadWasConnected = false;
                SetError("No gamepad connected!\nPlease connect one now.");
            }
            return;
        } else if (!gamepadWasConnected) {
            gamepadWasConnected = true;
            SetError(null);
        }

        if (changingSettings && (settingsChangeDelay += Time.deltaTime) >= 0.1) {
            Gamepad.current.SetMotorSpeeds(vrchatApi.config.heavyHaptics, vrchatApi.config.lightHaptics);
            settingsChangeDelay = 0;
        }

        if (!hapticsChanged || changingSettings) return;
        hapticsChanged = false;

        if (haptics > 0 && on) {
            Gamepad.current.SetMotorSpeeds(vrchatApi.config.heavyHaptics * haptics, vrchatApi.config.lightHaptics * haptics);
            vibrationStatus.color = Color.green;
        } else {
            Gamepad.current.PauseHaptics();
            vibrationStatus.color = Color.red;
        }
    }

    void OnDestroy() {
        if (server != null) server.Dispose();
    }

    public void SetAddress(string newAddress) {
        if (address != null) server.RemoveMethod(address, ReadValues);
        address = newAddress;
        if (newAddress != null) server.TryAddMethod(newAddress, ReadValues);
        haptics = 0;
        hapticsChanged = true;
    }

    public void OnToggled(bool on) {
        this.on = on;
    }

    public void OnChangedAddress(int index) {
        index = index - 1;
        var address = index < 0 ? null : vrchatApi.avatarParameters.parameters[index].output.address;
        vrchatApi.config.storedAvatarAddresses.Remove(vrchatApi.user.currentAvatar);
        vrchatApi.config.storedAvatarAddresses.Add(vrchatApi.user.currentAvatar, address);
        SetAddress(address);
    }

    public void OnChangedHeavyHaptics(float value) {
        vrchatApi.config.heavyHaptics = value;
    }

    public void OnChangedLightHaptics(float value) {
        vrchatApi.config.lightHaptics = value;
    }

    public void OnBeginDrag(BaseEventData eventData) {
        changingSettings = true;
    }

    public void OnEndDrag(BaseEventData eventData) {
        changingSettings = false;
        hapticsChanged = true;
    }

    public void OnUpdateClick() {
        Application.OpenURL("https://github.com/SemmieDev/VRC-Gamepad-Haptics/releases/latest");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    [Serializable]
    private class LatestRelease {
        public string tag_name;
    }
}
