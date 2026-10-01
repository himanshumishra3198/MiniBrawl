using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using MiniBrawl.Config;
using MiniBrawl.Core;
using MiniBrawl.Networking.Discovery;
using MiniBrawl.Networking.Identity;
using MiniBrawl.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Host/join screen. Games on the network list themselves through the LAN beacon (§4.1); the
    /// typed address stays as the fallback §4.2 asks for, because discovery is the part most likely
    /// to be blocked by a router or an Android power setting.
    ///
    /// Built in code rather than in the scene so the whole flow reads in one file.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        const string k_AddressKey = "minibrawl.lastAddress";
        const int k_MaxRows = 4;

        NetworkBootstrap m_Bootstrap;
        NetworkManager m_Manager;
        LanBeaconListener m_Listener;
        SessionRecovery m_Recovery;

        GameObject m_Panel;
        Text m_Status;
        GameObject m_Settings;
        Text m_VolumeLabel;
        Text m_EffectsLabel;
        Text m_DebugLabel;
        Text m_DeviceLabel;
        Text m_RoomsHeader;
        InputField m_AddressField;
        InputField m_NameField;
        readonly List<(Button button, Text label)> m_Rows = new();

        bool m_WasConnected;
        float m_IpAge;

        void Start()
        {
            m_Bootstrap = FindFirstObjectByType<NetworkBootstrap>();
            m_Listener = FindFirstObjectByType<LanBeaconListener>();
            m_Recovery = FindFirstObjectByType<SessionRecovery>();
            m_Manager = InstanceFinder.NetworkManager;

            BuildUi();

            // Only listen while the menu is up: the multicast lock costs battery.
            m_Listener?.StartListening();
        }

        void Update()
        {
            if (m_Manager == null || m_Panel == null) return;

            bool connected = m_Manager.IsServerStarted || m_Manager.IsClientStarted;
            if (connected != m_WasConnected)
            {
                m_WasConnected = connected;
                m_Panel.SetActive(!connected);

                if (connected) m_Listener?.StopListening();
                else m_Listener?.StartListening();
            }

            if (connected) return;

            // Recovery owns the status line while it is trying, so the player sees why they are
            // looking at a menu instead of the match.
            if (m_Recovery != null && !string.IsNullOrEmpty(m_Recovery.Status))
                m_Status.text = m_Recovery.Status;

            RefreshRooms();

            // Turning on a hotspot changes this device's address while the menu is open, so keep
            // it live rather than resolving once at startup.
            m_IpAge -= Time.unscaledDeltaTime;
            if (m_IpAge > 0f) return;

            m_IpAge = 2f;
            m_DeviceLabel.text = $"this device: {LocalIpResolver.Resolve()}";
        }

        void RefreshRooms()
        {
            IReadOnlyList<RoomInfo> rooms = m_Listener != null ? m_Listener.Rooms : null;
            int count = rooms?.Count ?? 0;

            m_RoomsHeader.text = count == 0
                ? (m_Listener != null && m_Listener.IsListening ? "searching for games…" : "discovery unavailable")
                : "games found — tap to join";

            for (int i = 0; i < m_Rows.Count; i++)
            {
                bool used = i < count;
                m_Rows[i].button.gameObject.SetActive(used);
                if (!used) continue;

                RoomInfo room = rooms[i];
                m_Rows[i].label.text = room.Describe();
                m_Rows[i].button.interactable = room.Compatible && !room.IsFull;
            }
        }

        void OnHost()
        {
            // The address is also shown in the HUD, because this panel hides once hosting starts.
            m_Recovery?.Reset();
            m_Status.text = $"hosting on {LocalIpResolver.Resolve()}:{NetworkConstants.GamePort}";
            m_Bootstrap.StartHost();
        }

        void OnJoinTyped()
        {
            string address = string.IsNullOrWhiteSpace(m_AddressField.text)
                ? "127.0.0.1"
                : m_AddressField.text.Trim();

            // Typed addresses carry no port, so fall back to the well-known one.
            Join(address, NetworkConstants.GamePort);
        }

        void OnJoinRoom(int index)
        {
            IReadOnlyList<RoomInfo> rooms = m_Listener?.Rooms;
            if (rooms == null || index >= rooms.Count) return;

            RoomInfo room = rooms[index];
            Join(room.Address, room.Port);   // the beacon says which port to dial
        }

        void Join(string address, ushort port)
        {
            // The player has chosen, so a later drop is worth recovering from again.
            m_Recovery?.Reset();

            PlayerPrefs.SetString(k_AddressKey, address);
            m_Status.text = $"connecting to {address}:{port}…";
            m_Bootstrap.StartClient(address, port);
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
            Transform panel = m_Panel.transform;

            Label(panel, "MINIBRAWL", 60, new Vector2(0f, 420f), new Vector2(900f, 80f));

            Button(panel, "HOST A GAME", new Vector2(-560f, 300f), new Vector2(560f, 110f), OnHost);

            m_RoomsHeader = Label(panel, "searching for games…", 30, new Vector2(0f, 190f), new Vector2(1200f, 40f));
            for (int i = 0; i < k_MaxRows; i++)
            {
                int index = i;   // captured per row
                Button row = Button(panel, "", new Vector2(0f, 110f - i * 95f), new Vector2(1100f, 85f),
                    () => OnJoinRoom(index));
                row.gameObject.SetActive(false);
                m_Rows.Add((row, row.GetComponentInChildren<Text>()));
            }

            m_NameField = NameField(panel, new Vector2(0f, -230f), new Vector2(620f, 84f));
            Label(panel, "your name", 24, new Vector2(0f, -180f), new Vector2(620f, 32f));

            Label(panel, "or type the host's address", 26, new Vector2(0f, -290f), new Vector2(900f, 36f));
            m_AddressField = AddressField(panel, new Vector2(-180f, -360f), new Vector2(620f, 90f));
            Button(panel, "JOIN", new Vector2(350f, -360f), new Vector2(380f, 90f), OnJoinTyped);

            m_DeviceLabel = Label(panel, "this device: …", 30, new Vector2(0f, -440f), new Vector2(1200f, 50f));
            m_Status = Label(panel, "", 26, new Vector2(0f, -490f), new Vector2(1400f, 50f));

            Button(panel, "SETTINGS", new Vector2(560f, 300f), new Vector2(560f, 110f),
                () => ShowSettings(true));

            BuildSettings(canvasGo.transform);
        }

        /// <summary>
        /// Volume and the developer readout, over the menu.
        ///
        /// Stepped buttons rather than sliders. A slider on a phone is a small target that wants a
        /// precise drag, and nobody needs to set the volume to 43 percent — five steps covers the
        /// decision anyone is actually making, including off.
        /// </summary>
        void BuildSettings(Transform canvas)
        {
            m_Settings = Panel(canvas);
            m_Settings.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.985f);
            Transform panel = m_Settings.transform;

            Label(panel, "SETTINGS", 56, new Vector2(0f, 360f), new Vector2(900f, 76f));

            Label(panel, "volume", 32, new Vector2(-380f, 180f), new Vector2(420f, 44f));
            Button(panel, "–", new Vector2(60f, 180f), new Vector2(110f, 90f),
                () => StepVolume(false));
            m_VolumeLabel = Label(panel, "", 36, new Vector2(230f, 180f), new Vector2(220f, 50f));
            Button(panel, "+", new Vector2(400f, 180f), new Vector2(110f, 90f),
                () => StepVolume(true));

            Label(panel, "effects", 32, new Vector2(-380f, 60f), new Vector2(420f, 44f));
            Button(panel, "–", new Vector2(60f, 60f), new Vector2(110f, 90f),
                () => StepEffects(false));
            m_EffectsLabel = Label(panel, "", 36, new Vector2(230f, 60f), new Vector2(220f, 50f));
            Button(panel, "+", new Vector2(400f, 60f), new Vector2(110f, 90f),
                () => StepEffects(true));

            Label(panel, "developer readout", 32, new Vector2(-330f, -60f), new Vector2(540f, 44f));
            m_DebugLabel = Label(panel, "", 36, new Vector2(300f, -60f), new Vector2(380f, 50f));
            Button(panel, "TOGGLE", new Vector2(300f, -150f), new Vector2(380f, 90f), ToggleDebug);

            Button(panel, "BACK", new Vector2(0f, -340f), new Vector2(460f, 110f),
                () => ShowSettings(false));

            m_Settings.SetActive(false);
            RefreshSettings();
        }

        void ShowSettings(bool open)
        {
            if (m_Settings == null) return;
            m_Settings.SetActive(open);
            if (open) RefreshSettings();
        }

        void StepVolume(bool up) =>
            Nudge(v => GameSettings.MasterVolume = v, GameSettings.MasterVolume, up);

        void StepEffects(bool up) =>
            Nudge(v => GameSettings.SfxVolume = v, GameSettings.SfxVolume, up);

        /// <summary>Moves a 0-1 setting by a fifth, clamped. Five steps including silence.</summary>
        static void Nudge(System.Action<float> set, float current, bool up)
        {
            set(Mathf.Clamp01(Mathf.Round((current + (up ? 0.25f : -0.25f)) * 4f) / 4f));
        }

        void ToggleDebug()
        {
            GameSettings.ShowDebugOverlay = !GameSettings.ShowDebugOverlay;
            RefreshSettings();
        }

        void RefreshSettings()
        {
            if (m_VolumeLabel != null)
                m_VolumeLabel.text = Percent(GameSettings.MasterVolume);
            if (m_EffectsLabel != null)
                m_EffectsLabel.text = Percent(GameSettings.SfxVolume);
            if (m_DebugLabel != null)
                m_DebugLabel.text = GameSettings.ShowDebugOverlay ? "ON" : "OFF";
        }

        static string Percent(float value) => value <= 0f ? "OFF" : $"{Mathf.RoundToInt(value * 100f)}%";

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

        static Button Button(Transform parent, string text, Vector2 position, Vector2 dimensions,
                             UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Button_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);

            go.GetComponent<Image>().color = new Color(0.35f, 0.85f, 1f, 0.3f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            Text label = Label(go.transform, text, 38, Vector2.zero, dimensions);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>Saved as it changes, so the name is already set by the time a slot is claimed.</summary>
        static InputField NameField(Transform parent, Vector2 position, Vector2 dimensions)
        {
            InputField field = BuildField(parent, position, dimensions);
            field.characterLimit = 16;
            field.text = PlayerIdentity.Name;
            field.onEndEdit.AddListener(value => PlayerIdentity.Name = value);
            return field;
        }

        static InputField AddressField(Transform parent, Vector2 position, Vector2 dimensions)
        {
            InputField field = BuildField(parent, position, dimensions);
            field.text = PlayerPrefs.GetString(k_AddressKey, "192.168.43.1");
            return field;
        }

        static InputField BuildField(Transform parent, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            Text text = Label(go.transform, "", 36, Vector2.zero, dimensions);
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
