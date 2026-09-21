using FishNet;
using FishNet.Managing;
using MiniBrawl.Config;
using MiniBrawl.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Minimal host/join screen so two phones can play over a hotspot. The host shows its address
    /// and the joiner types it in — LAN discovery replaces this typing in Phase 3 (§4.1), which is
    /// why this deliberately stays a stopgap rather than growing into a lobby.
    ///
    /// Built in code rather than in the scene so the whole flow reads in one file.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        const string k_AddressKey = "minibrawl.lastAddress";

        NetworkBootstrap m_Bootstrap;
        NetworkManager m_Manager;
        GameObject m_Panel;
        Text m_Status;
        Text m_DeviceLabel;
        InputField m_AddressField;
        bool m_WasConnected;
        float m_IpAge;

        void Start()
        {
            m_Bootstrap = FindFirstObjectByType<NetworkBootstrap>();
            m_Manager = InstanceFinder.NetworkManager;
            BuildUi();
        }

        void Update()
        {
            if (m_Manager == null || m_Panel == null) return;

            bool connected = m_Manager.IsServerStarted || m_Manager.IsClientStarted;
            if (connected != m_WasConnected)
            {
                m_WasConnected = connected;
                m_Panel.SetActive(!connected);
            }

            // Turning on a hotspot changes this device's address while the menu is open, so keep
            // it live rather than resolving once at startup.
            if (connected) return;
            m_IpAge -= Time.unscaledDeltaTime;
            if (m_IpAge > 0f) return;

            m_IpAge = 2f;
            m_DeviceLabel.text = $"this device: {LocalIpResolver.Resolve()}";
        }

        void OnHost()
        {
            // The address is also shown in the HUD, because this panel hides once hosting starts.
            m_Status.text = $"hosting on {LocalIpResolver.Resolve()}:{NetworkConstants.GamePort}";
            m_Bootstrap.StartHost();
        }

        void OnJoin()
        {
            string address = string.IsNullOrWhiteSpace(m_AddressField.text)
                ? "127.0.0.1"
                : m_AddressField.text.Trim();

            PlayerPrefs.SetString(k_AddressKey, address);
            m_Status.text = $"connecting to {address}:{NetworkConstants.GamePort}...";
            m_Bootstrap.StartClient(address);
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("SessionMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // above the HUD

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            m_Panel = Panel(canvasGo.transform);

            Label(m_Panel.transform, "MINIBRAWL", 64, new Vector2(0f, 300f), new Vector2(900f, 90f));

            Button(m_Panel.transform, "HOST", new Vector2(-260f, 120f), new Vector2(420f, 130f), OnHost);
            Button(m_Panel.transform, "JOIN", new Vector2(260f, 120f), new Vector2(420f, 130f), OnJoin);

            m_AddressField = AddressField(m_Panel.transform, new Vector2(0f, -40f), new Vector2(700f, 100f));
            Label(m_Panel.transform, "host's address", 28, new Vector2(0f, 30f), new Vector2(700f, 40f));

            m_DeviceLabel = Label(m_Panel.transform, "this device: …", 36,
                new Vector2(0f, -170f), new Vector2(1200f, 60f));
            m_Status = Label(m_Panel.transform, "", 28,
                new Vector2(0f, -240f), new Vector2(1200f, 80f));
        }

        static GameObject Panel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.97f);
            return go;
        }

        static Text Label(Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        static void Button(Transform parent, string text, Vector2 position, Vector2 dimensions,
                           UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Button_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);

            go.GetComponent<Image>().color = new Color(0.35f, 0.85f, 1f, 0.3f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            Text label = Label(go.transform, text, 44, Vector2.zero, dimensions);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static InputField AddressField(Transform parent, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("AddressField", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            Text text = Label(go.transform, "", 40, Vector2.zero, dimensions);
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            field.text = PlayerPrefs.GetString(k_AddressKey, "192.168.43.1");
            return field;
        }

        static void Place(RectTransform rt, Vector2 position, Vector2 dimensions)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = dimensions;
        }
    }
}
