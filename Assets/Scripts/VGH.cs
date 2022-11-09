using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OscCore;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VGH : MonoBehaviour {
    public GameObject errorDisplay;
    public TextMeshProUGUI errorMsg, vibrationStatus;
    public TMP_InputField addressInput;
    public Slider heavyHapticsSlider, lightHapticsSlider;

    private OscServer server;
    private bool haptics, hapticsChanged, gamepadWasConnected = true, on = true, changingSettings;
    private string lastAddress, address;
    private float heavyHaptics, lightHaptics, settingsChangeDelay;

    void Start() {
        Application.targetFrameRate = 30;

        server = new OscServer(9001);

        if (server == null) {
            SetError("OSC server could not be created.\nPlease try again by restarting the program.");
            Destroy(gameObject);
            return;
        }

        OnChangedAddress(PlayerPrefs.GetString("address"));
        heavyHaptics = PlayerPrefs.GetFloat("heavyHaptics");
        lightHaptics = PlayerPrefs.GetFloat("lightHaptics");

        addressInput.SetTextWithoutNotify(address);
        heavyHapticsSlider.SetValueWithoutNotify(heavyHaptics);
        lightHapticsSlider.SetValueWithoutNotify(lightHaptics);
    }

    private void ReadValues(OscMessageValues values) {
        var newHaptics = values.ReadBooleanElement(0);
        if (newHaptics == haptics) return;
        haptics = newHaptics;
        hapticsChanged = true;
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
            Gamepad.current.SetMotorSpeeds(heavyHaptics, lightHaptics);
            settingsChangeDelay = 0;
        }

        if (!hapticsChanged || changingSettings) return;
        hapticsChanged = false;

        if (haptics && on) {
            Gamepad.current.SetMotorSpeeds(heavyHaptics, lightHaptics);
            vibrationStatus.color = Color.green;
        } else {
            Gamepad.current.PauseHaptics();
            vibrationStatus.color = Color.red;
        }
    }

    void OnDestroy() {
        if (server != null) {
            server.Dispose();
            PlayerPrefs.SetString("address", address);
            PlayerPrefs.SetFloat("lightHaptics", lightHaptics);
            PlayerPrefs.SetFloat("heavyHaptics", heavyHaptics);
            PlayerPrefs.Save();
        }
    }

    public void OnToggled(bool on) {
        this.on = on;
    }

    public void OnChangedAddress(string address) {
        if (lastAddress != null) {
            server.RemoveAddress(lastAddress);
            lastAddress = address;
        }
        if (address == null || address.Equals("")) return;
        server.TryAddMethod(address, ReadValues);
        this.address = address;
    }

    public void OnChangedHeavyHaptics(float value) {
        heavyHaptics = value;
    }

    public void OnChangedLightHaptics(float value) {
        lightHaptics = value;
    }

    public void OnBeginDrag(BaseEventData eventData) {
        changingSettings = true;
    }

    public void OnEndDrag(BaseEventData eventData) {
        changingSettings = false;
        hapticsChanged = true;
    }
}
