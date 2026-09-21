using System;
using UnityEngine;

namespace MiniBrawl.Platform
{
    /// <summary>
    /// Android filters broadcast and multicast packets away from apps unless a WifiManager
    /// MulticastLock is held (§14). Without it LAN discovery looks like it works in the Editor and
    /// silently finds nothing on a phone.
    ///
    /// Holding the lock costs battery, so it is taken only while actively browsing for games.
    /// </summary>
    public static class AndroidMulticastLock
    {
        /// <summary>Returns a handle to dispose when done, or null where the lock does not apply.</summary>
        public static IDisposable Acquire()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return new Handle();
            }
            catch (Exception e)
            {
                // Missing permission, or a device that does not implement it: discovery may still
                // work, so this is a warning rather than a failure.
                Debug.LogWarning($"[AndroidMulticastLock] not acquired: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class Handle : IDisposable
        {
            readonly AndroidJavaObject m_Lock;

            public Handle()
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using AndroidJavaObject wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");

                m_Lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "MiniBrawlDiscovery");
                m_Lock.Call("setReferenceCounted", true);
                m_Lock.Call("acquire");
                Debug.Log("[AndroidMulticastLock] acquired");
            }

            public void Dispose()
            {
                try
                {
                    if (m_Lock.Call<bool>("isHeld")) m_Lock.Call("release");
                    m_Lock.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AndroidMulticastLock] release failed: {e.Message}");
                }
            }
        }
#endif
    }
}
