using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

public class AvatarSelectionScreen : MonoBehaviour {
    public RectTransform content;
    public RectTransform avatarButton;
    public VRChatAPI vrchatApi;

    private List<QueuedImage> imageQueue = new List<QueuedImage>();
    private Coroutine imagesDownloader;

    void Start() {
        
    }

    void Update() {
        
    }

    public void Generate() {
        StopCoroutine(imagesDownloader);
        foreach (GameObject child in content) Destroy(child);

        // foreach (var avatar in vrchatApi.avatars) {
        //     var avatarButtonClone = Instantiate(avatarButton, content);

        //     avatarButtonClone.GetChild(0).GetChild(0).GetComponent<Text>().text = avatar.name;

        //     imageQueue.Add(new QueuedImage(avatarButtonClone.GetChild(0).GetChild(1).GetComponent<Image>(), avatar.thumbnailImageUrl));
        // }

        imagesDownloader = StartCoroutine(GetText());
    }

    private IEnumerator GetText() {
        for (int i = imageQueue.Count - 1; i >= 0; i--) {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture("https://www.my-server.com/myimage.png")) {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.Log("Failed to download thumbnail: "+request.error);
                } else {
                    var texture = DownloadHandlerTexture.GetContent(request);
                }
            }

            imageQueue.RemoveAt(i);
        }
    }

    private class QueuedImage {
        public readonly Image image;
        public readonly string imageUrl;

        public QueuedImage(Image image, string imageUrl) {
            this.image = image;
            this.imageUrl = imageUrl;
        }
    }
}
