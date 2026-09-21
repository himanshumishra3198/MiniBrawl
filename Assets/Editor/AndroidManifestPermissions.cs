using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Adds the Wi-Fi permissions LAN discovery needs to Unity's generated manifest.
///
/// Deliberately not under _Project/Scripts/Editor: scripts inside an assembly definition cannot see
/// UnityEditor.Android, while ones in a plain Editor folder can. Injecting two permissions also
/// leaves the rest of the manifest Unity's to manage, unlike a custom main manifest which would
/// freeze today's template into the project.
/// </summary>
public sealed class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
{
    const string k_AndroidNamespace = "http://schemas.android.com/apk/res/android";

    static readonly string[] k_Permissions =
    {
        "android.permission.ACCESS_WIFI_STATE",           // read the Wi-Fi state to resolve our address
        "android.permission.CHANGE_WIFI_MULTICAST_STATE", // required to hold a MulticastLock
    };

    public int callbackOrder => 1;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[AndroidManifestPermissions] no manifest at {manifestPath}");
            return;
        }

        var document = new XmlDocument();
        document.Load(manifestPath);

        XmlElement manifest = document.DocumentElement;
        if (manifest == null) return;

        int added = 0;
        foreach (string permission in k_Permissions)
            if (AddPermission(document, manifest, permission)) added++;

        if (added == 0) return;

        document.Save(manifestPath);
        Debug.Log($"[AndroidManifestPermissions] added {added} permission(s) for LAN discovery");
    }

    static bool AddPermission(XmlDocument document, XmlElement manifest, string permission)
    {
        foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
            if (node.Attributes?["android:name"]?.Value == permission)
                return false;

        XmlElement element = document.CreateElement("uses-permission");
        element.SetAttribute("name", k_AndroidNamespace, permission);
        manifest.AppendChild(element);
        return true;
    }
}
