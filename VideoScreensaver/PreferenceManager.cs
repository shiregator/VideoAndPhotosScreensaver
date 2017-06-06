using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;

namespace VideoScreensaver {
    // Manages persistent storage for the screensaver.
    // Can't use IsolatedStorage because of a Windows bug (tries to use 8-char filename when screensaver runs on its own, not in preview mode).
    // Can't use Settings for the same reason.
    // Using the registry directly.
    static class PreferenceManager {

        public const string BASE_KEY = "VideoScreensaver";
        public const string VIDEO_PREFS_FILE = "Media";
        public const string VOLUME_PREFS_FILE = "Volume";
        public const string INTERVAL_PREFS_FILE = "Interval";
        public const string ALGORITHM_PREFS_FILE = "Algorithm";

        public const int ALGORITHM_SEQUENTIAL = 0;
        public const int ALGORITHM_RANDOM = 1;
        public const int ALGORITHM_RANDOM_NO_REPEAT = 2;

        public static List<String> ReadVideoSettings() {
            List<String> videos = new List<String>();
            string videoStr = ReadStringValue(VIDEO_PREFS_FILE);
            if (videoStr.Length > 0) {
                videos.AddRange(videoStr.Split('\n'));
            }
            return videos;
        }

        public static void WriteVideoSettings(List<String> videoPaths) {
            WriteStringValue(VIDEO_PREFS_FILE, String.Join<object>("\n", videoPaths));
        }

        public static double ReadVolumeSetting() {
            try {
                return Convert.ToDouble(ReadStringValue(VOLUME_PREFS_FILE));
            }
            catch (System.FormatException) { }
            catch (System.OverflowException) { }
            return 0;
        }

        public static void WriteVolumeSetting(double volume) {
            WriteStringValue(VOLUME_PREFS_FILE, volume.ToString());
        }

        public static int ReadIntervalSetting()
        {
            try
            {
                return Convert.ToInt32(ReadStringValue(INTERVAL_PREFS_FILE));
            }
            catch (System.FormatException) { }
            catch (System.OverflowException) { }
            return 0;
        }

        public static void WriteIntervalSetting(int interval)
        {
            WriteStringValue(INTERVAL_PREFS_FILE, interval.ToString());
        }

        public static int ReadAlgorithmSetting()
        {
            try
            {
                return Convert.ToInt32(ReadStringValue(ALGORITHM_PREFS_FILE));
            }
            catch (System.FormatException) { }
            catch (System.OverflowException) { }
            return 0;
        }

        public static void WriteAlgorithmSetting(int alg)
        {
            WriteStringValue(ALGORITHM_PREFS_FILE, alg.ToString());
        }

        private static Tuple<RegistryKey, RegistryKey> OpenRegistryKey() {
            RegistryKey software = Registry.CurrentUser.CreateSubKey("Software");
            return new Tuple<RegistryKey, RegistryKey>(software.CreateSubKey(BASE_KEY), software);
        }

        private static string ReadStringValue(string valueName) {
            Tuple<RegistryKey, RegistryKey> appKey = OpenRegistryKey();
            try {
                return appKey.Item1.GetValue(valueName, "").ToString();
            }
            finally {
                appKey.Item1.Close();
                appKey.Item2.Close();
            }
        }

        private static void WriteStringValue(string valueName, string valueData) {
            Tuple<RegistryKey, RegistryKey> appKey = OpenRegistryKey();
            try {
                appKey.Item1.SetValue(valueName, valueData);
            }
            finally {
                appKey.Item1.Close();
                appKey.Item2.Close();
            }
        }
    }
}
